using System.Runtime.InteropServices;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace Merlin.Backend.Services.BargeIn;

public sealed class WindowsWasapiAecAudioCaptureService : IBargeInAudioCaptureService, IHostedService, IDisposable
{
    private static readonly Guid IIdAcousticEchoCancellationControl = new("f4ae25b5-aaa3-437d-b6b3-dbbe2d0e9549");
    private readonly IBargeInCoordinator _coordinator;
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly ILogger<WindowsWasapiAecAudioCaptureService> _logger;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly IWindowsAecStatus _status;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private BargeInSpeechContext? _activeContext;
    private long _micFrameCount;
    private long _echoReducedFrameCount;

    public WindowsWasapiAecAudioCaptureService(
        IPlaybackReferenceTap playbackReferenceTap,
        IBargeInCoordinator coordinator,
        IWindowsAecStatus status,
        IBargeInDiagnosticsLogger diagnostics,
        IOptionsMonitor<BargeInOptions> options,
        ILogger<WindowsWasapiAecAudioCaptureService> logger)
    {
        _playbackReferenceTap = playbackReferenceTap;
        _coordinator = coordinator;
        _status = status;
        _diagnostics = diagnostics;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _playbackReferenceTap.SpeechStarted += OnSpeechStarted;
        _playbackReferenceTap.SpeechStopped += OnSpeechStopped;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _playbackReferenceTap.SpeechStarted -= OnSpeechStarted;
        _playbackReferenceTap.SpeechStopped -= OnSpeechStopped;
        StopCapture("host_stopping");
        return Task.CompletedTask;
    }

    public Task SubmitMicrophoneFrameAsync(BargeInAudioFrame frame, CancellationToken cancellationToken = default)
    {
        return _coordinator.ProcessMicrophoneFrameAsync(frame, cancellationToken);
    }

    private void OnSpeechStarted(object? sender, BargeInSpeechContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return;
        }

        if (!string.Equals(options.AecProvider, "WindowsWasapiAec", StringComparison.OrdinalIgnoreCase))
        {
            _status.MarkUnavailable($"Configured AEC provider is {options.AecProvider}, not WindowsWasapiAec.");
            return;
        }

        lock (_syncRoot)
        {
            if (_captureTask is { IsCompleted: false })
            {
                return;
            }

            _activeContext = context;
            _micFrameCount = 0;
            _echoReducedFrameCount = 0;
            _captureCancellation = new CancellationTokenSource();
            _captureTask = Task.Run(
                () => CaptureLoopAsync(context, _captureCancellation.Token),
                CancellationToken.None);
        }
    }

    private void OnSpeechStopped(object? sender, BargeInSpeechContext context)
    {
        StopCapture("speech_stopped");
    }

    private async Task CaptureLoopAsync(BargeInSpeechContext context, CancellationToken cancellationToken)
    {
        MMDeviceEnumerator? enumerator = null;
        MMDevice? captureDevice = null;
        MMDevice? renderDevice = null;
        AudioClient? audioClient = null;
        EventWaitHandle? frameReady = null;

        try
        {
            var options = _options.CurrentValue;
            enumerator = new MMDeviceEnumerator();
            captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, ParseRole(options.CaptureDeviceRole, Role.Communications));
            renderDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, ParseRole(options.RenderDeviceRole, Role.Multimedia));
            audioClient = captureDevice.AudioClient;
            var clientInterface = GetAudioClientInterface(audioClient);

            SetCommunicationsClientProperties(audioClient, captureDevice, renderDevice, options);
            var format = audioClient.MixFormat;
            var bufferDuration = TimeSpan.FromMilliseconds(Math.Clamp(options.CaptureFrameMs, 10, 100));
            audioClient.Initialize(
                AudioClientShareMode.Shared,
                AudioClientStreamFlags.EventCallback,
                bufferDuration.Ticks,
                0,
                format,
                Guid.Empty);

            ConfigureAecReferenceEndpoint(clientInterface, renderDevice.ID);
            if (IsClientPropertiesDisabledForDiagnostics(options))
            {
                var reason = "Windows WASAPI AEC unavailable: DisabledForDiagnostics skipped SetClientProperties, so communications AEC is not verified.";
                _status.MarkUnavailable(reason);
                _diagnostics.AecInitialized(AecMode.Unavailable, reason);
                _logger.LogWarning(
                    "Windows WASAPI AEC diagnostic Initialize/GetService path succeeded, but real AEC will not be marked active because SetClientProperties was disabled.");
                return;
            }

            _status.MarkActive(
                $"Windows WASAPI AEC active. CaptureDevice: {captureDevice.FriendlyName}. RenderReference: {renderDevice.FriendlyName}. Format: {format.SampleRate} Hz, {format.Channels} channels, {format.BitsPerSample}-bit {format.Encoding}.");
            _diagnostics.AecInitialized(AecMode.Active, _status.StatusReason);

            frameReady = new EventWaitHandle(false, EventResetMode.AutoReset);
            audioClient.SetEventHandle(frameReady.SafeWaitHandle.DangerousGetHandle());
            var captureClient = audioClient.AudioCaptureClient;
            audioClient.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                frameReady.WaitOne(TimeSpan.FromMilliseconds(100));
                while (captureClient.GetNextPacketSize() > 0)
                {
                    var buffer = captureClient.GetBuffer(out var framesAvailable, out var flags);
                    try
                    {
                        if (framesAvailable <= 0)
                        {
                            continue;
                        }

                        var samples = ConvertToMonoFloat(buffer, framesAvailable, format, flags.HasFlag(AudioClientBufferFlags.Silent));
                        var frameNumber = Interlocked.Increment(ref _micFrameCount);
                        _diagnostics.MicFrameProcessed(context, frameNumber);
                        var audioFrame = new BargeInAudioFrame
                        {
                            Samples = samples,
                            SampleRate = format.SampleRate,
                            Timestamp = DateTimeOffset.UtcNow
                        };
                        await _coordinator.ProcessMicrophoneFrameAsync(audioFrame, cancellationToken);
                        var echoFrameNumber = Interlocked.Increment(ref _echoReducedFrameCount);
                        _diagnostics.EchoReducedFrameProcessed(context, echoFrameNumber, AecMode.Active);
                    }
                    finally
                    {
                        captureClient.ReleaseBuffer(framesAvailable);
                    }
                }
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            var reason = $"Windows WASAPI AEC unavailable: {exception.Message}";
            _status.MarkUnavailable(reason);
            _diagnostics.AecInitialized(AecMode.Unavailable, reason);
            if (!options.AllowDegradedAecFallback || options.RequireRealAecForBargeIn)
            {
                _logger.LogError(
                    exception,
                    "Barge-in microphone capture did not start because real Windows WASAPI AEC is unavailable and degraded fallback is disabled.");
                return;
            }

            _logger.LogWarning(
                exception,
                "Barge-in microphone capture failed; degraded fallback is explicitly allowed but no raw capture fallback is started by this provider.");
        }
        finally
        {
            try
            {
                audioClient?.Stop();
            }
            catch
            {
            }

            frameReady?.Dispose();
            captureDevice?.Dispose();
            renderDevice?.Dispose();
            enumerator?.Dispose();
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeContext, context))
                {
                    _activeContext = null;
                }
            }
        }
    }

    private void StopCapture(string reason)
    {
        CancellationTokenSource? cancellation;
        Task? captureTask;
        lock (_syncRoot)
        {
            cancellation = _captureCancellation;
            captureTask = _captureTask;
            _captureCancellation = null;
            _captureTask = null;
        }

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        _logger.LogInformation(
            "Windows WASAPI AEC capture stopping. Reason: {Reason}. MicFrames: {MicFrames}. EchoReducedFrames: {EchoReducedFrames}.",
            reason,
            Interlocked.Read(ref _micFrameCount),
            Interlocked.Read(ref _echoReducedFrameCount));
        _ = captureTask?.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static IAudioClient GetAudioClientInterface(AudioClient audioClient)
    {
        var field = typeof(AudioClient).GetField("audioClientInterface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("NAudio AudioClient internal interface field was not found.");
        return (IAudioClient)(field.GetValue(audioClient)
            ?? throw new InvalidOperationException("NAudio AudioClient internal interface was unavailable."));
    }

    private void SetCommunicationsClientProperties(AudioClient audioClient, MMDevice captureDevice, MMDevice renderDevice, BargeInOptions options)
    {
        var mode = WindowsWasapiAecClientProperties.NormalizeMode(options.WindowsAecSetClientPropertiesMode);
        if (string.Equals(mode, WindowsWasapiAecClientProperties.ModeDisabledForDiagnostics, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Windows WASAPI AEC SetClientProperties skipped for diagnostics. This is not a proper communications AEC setup. CaptureDevice: {CaptureDevice}. CaptureDeviceId: {CaptureDeviceId}. RenderDevice: {RenderDevice}. RenderDeviceId: {RenderDeviceId}. OSBuild: {OSBuild}.",
                captureDevice.FriendlyName,
                captureDevice.ID,
                renderDevice.FriendlyName,
                renderDevice.ID,
                Environment.OSVersion.Version.Build);
            return;
        }

        var field = typeof(AudioClient).GetField("audioClientInterface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("NAudio AudioClient internal interface field was not found.");
        var audioClient2Available = field.GetValue(audioClient) is IAudioClient2;
        var diagnostics = string.Equals(mode, WindowsWasapiAecClientProperties.ModeCustomInterop, StringComparison.Ordinal)
            ? WindowsWasapiAecClientProperties.CreateCustomInteropDiagnostics()
            : WindowsWasapiAecClientProperties.CreateNAudioDiagnostics();

        _logger.LogInformation(
            "Windows WASAPI AEC SetClientProperties starting. Mode: {Mode}. IAudioClient2Available: {IAudioClient2Available}. CbSize: {CbSize}. BIsOffload: {BIsOffload}. ECategory: {ECategory} ({ECategoryName}). Options: {Options}. CaptureDevice: {CaptureDevice}. CaptureDeviceId: {CaptureDeviceId}. RenderDevice: {RenderDevice}. RenderDeviceId: {RenderDeviceId}. OSBuild: {OSBuild}.",
            diagnostics.Mode,
            audioClient2Available,
            diagnostics.CbSize,
            diagnostics.BIsOffload,
            diagnostics.ECategory,
            diagnostics.ECategoryName,
            diagnostics.Options,
            captureDevice.FriendlyName,
            captureDevice.ID,
            renderDevice.FriendlyName,
            renderDevice.ID,
            Environment.OSVersion.Version.Build);

        if (field.GetValue(audioClient) is not IAudioClient2 audioClient2)
        {
            throw new InvalidOperationException("The capture endpoint does not expose IAudioClient2, so a Communications AEC stream cannot be created.");
        }

        var pointer = Marshal.AllocHGlobal(diagnostics.CbSize);
        try
        {
            if (string.Equals(mode, WindowsWasapiAecClientProperties.ModeCustomInterop, StringComparison.Ordinal))
            {
                Marshal.StructureToPtr(WindowsWasapiAecClientProperties.CreateCustomInteropProperties(), pointer, fDeleteOld: false);
            }
            else
            {
                Marshal.StructureToPtr(WindowsWasapiAecClientProperties.CreateNAudioProperties(), pointer, fDeleteOld: false);
            }

            try
            {
                audioClient2.SetClientProperties(pointer);
                _logger.LogInformation(
                    "Windows WASAPI AEC SetClientProperties succeeded. Mode: {Mode}. HRESULT: 0x00000000 ({HResultName}). ContinuingWithoutClientPropertiesAllowed: False.",
                    diagnostics.Mode,
                    WindowsWasapiAecClientProperties.GetHResultName(0));
            }
            catch (COMException exception)
            {
                var reason = WindowsWasapiAecClientProperties.FormatSetClientPropertiesFailureReason(exception.HResult);
                _logger.LogError(
                    exception,
                    "Windows WASAPI AEC SetClientProperties failed. Mode: {Mode}. HRESULT: 0x{HResult:X8} ({HResultName}). ContinuingWithoutClientPropertiesAllowed: False. CaptureDevice: {CaptureDevice}. CaptureDeviceId: {CaptureDeviceId}. RenderDevice: {RenderDevice}. RenderDeviceId: {RenderDeviceId}. OSBuild: {OSBuild}.",
                    diagnostics.Mode,
                    unchecked((uint)exception.HResult),
                    WindowsWasapiAecClientProperties.GetHResultName(exception.HResult),
                    captureDevice.FriendlyName,
                    captureDevice.ID,
                    renderDevice.FriendlyName,
                    renderDevice.ID,
                    Environment.OSVersion.Version.Build);
                throw new InvalidOperationException(reason, exception);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }

    private static bool IsClientPropertiesDisabledForDiagnostics(BargeInOptions options)
    {
        return string.Equals(
            WindowsWasapiAecClientProperties.NormalizeMode(options.WindowsAecSetClientPropertiesMode),
            WindowsWasapiAecClientProperties.ModeDisabledForDiagnostics,
            StringComparison.Ordinal);
    }

    private static void ConfigureAecReferenceEndpoint(IAudioClient audioClient, string renderEndpointId)
    {
        var hresult = audioClient.GetService(IIdAcousticEchoCancellationControl, out var service);
        if (hresult != 0)
        {
            Marshal.ThrowExceptionForHR(hresult);
        }

        if (service is not IAcousticEchoCancellationControl aecControl)
        {
            throw new InvalidOperationException("Windows returned an AEC control service that could not be marshalled.");
        }

        var setResult = aecControl.SetEchoCancellationRenderEndpoint(renderEndpointId);
        if (setResult != 0)
        {
            Marshal.ThrowExceptionForHR(setResult);
        }
    }

    private static float[] ConvertToMonoFloat(IntPtr buffer, int framesAvailable, WaveFormat format, bool isSilent)
    {
        var output = new float[framesAvailable];
        if (isSilent)
        {
            return output;
        }

        var channels = Math.Max(1, format.Channels);
        var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        var bytes = framesAvailable * channels * bytesPerSample;
        var raw = new byte[bytes];
        Marshal.Copy(buffer, raw, 0, raw.Length);

        for (var frame = 0; frame < framesAvailable; frame++)
        {
            double mixed = 0;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = (frame * channels + channel) * bytesPerSample;
                mixed += ReadSample(raw, offset, format);
            }

            output[frame] = (float)(mixed / channels);
        }

        return output;
    }

    private static double ReadSample(byte[] raw, int offset, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return BitConverter.ToSingle(raw, offset);
        }

        return format.BitsPerSample switch
        {
            16 => BitConverter.ToInt16(raw, offset) / 32768.0,
            24 => ReadInt24(raw, offset) / 8388608.0,
            32 => BitConverter.ToInt32(raw, offset) / 2147483648.0,
            _ => 0.0
        };
    }

    private static int ReadInt24(byte[] raw, int offset)
    {
        var value = raw[offset] | (raw[offset + 1] << 8) | (raw[offset + 2] << 16);
        if ((value & 0x800000) != 0)
        {
            value |= unchecked((int)0xff000000);
        }

        return value;
    }

    private static Role ParseRole(string value, Role fallback)
    {
        return Enum.TryParse<Role>(value, ignoreCase: true, out var role) ? role : fallback;
    }

    public void Dispose()
    {
        StopCapture("disposed");
    }

    [ComImport]
    [Guid("f4ae25b5-aaa3-437d-b6b3-dbbe2d0e9549")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAcousticEchoCancellationControl
    {
        [PreserveSig]
        int SetEchoCancellationRenderEndpoint([MarshalAs(UnmanagedType.LPWStr)] string endpointId);
    }
}
