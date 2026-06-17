using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Merlin.Backend.Services.BargeIn;

public sealed class WebRtcApmBargeInAudioCaptureService : IBargeInAudioCaptureService, IHostedService, IDisposable
{
    private readonly IBargeInCoordinator _coordinator;
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly ILogger<WebRtcApmBargeInAudioCaptureService> _logger;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly object _syncRoot = new();
    private readonly List<float> _pendingSamples = [];
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private BargeInSpeechContext? _activeContext;
    private long _nearEndFramesReceived;
    private int _targetSampleRate = 48000;
    private int _frameSize = 480;

    public WebRtcApmBargeInAudioCaptureService(
        IPlaybackReferenceTap playbackReferenceTap,
        IBargeInCoordinator coordinator,
        IBargeInDiagnosticsLogger diagnostics,
        IOptionsMonitor<BargeInOptions> options,
        ILogger<WebRtcApmBargeInAudioCaptureService> logger)
    {
        _playbackReferenceTap = playbackReferenceTap;
        _coordinator = coordinator;
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
        if (!options.Enabled || !string.Equals(options.AecProvider, "WebRtcApm", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_captureTask is { IsCompleted: false })
            {
                return;
            }

            _targetSampleRate = NormalizeSampleRate(options.AecSampleRate);
            _frameSize = _targetSampleRate * Math.Max(10, options.FrameMs) / 1000;
            _pendingSamples.Clear();
            _nearEndFramesReceived = 0;
            _activeContext = context;
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
        AudioClient? audioClient = null;
        EventWaitHandle? frameReady = null;

        try
        {
            var options = _options.CurrentValue;
            enumerator = new MMDeviceEnumerator();
            captureDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, ParseRole(options.CaptureDeviceRole, Role.Communications));
            audioClient = captureDevice.AudioClient;
            var format = audioClient.MixFormat;
            var bufferDuration = TimeSpan.FromMilliseconds(Math.Clamp(options.CaptureFrameMs, 10, 100));
            audioClient.Initialize(
                AudioClientShareMode.Shared,
                AudioClientStreamFlags.EventCallback,
                bufferDuration.Ticks,
                0,
                format,
                Guid.Empty);

            frameReady = new EventWaitHandle(false, EventResetMode.AutoReset);
            audioClient.SetEventHandle(frameReady.SafeWaitHandle.DangerousGetHandle());
            var captureClient = audioClient.AudioCaptureClient;
            audioClient.Start();
            _logger.LogInformation(
                "WebRTC APM microphone capture started. ProviderName: WebRtcApm. CaptureDevice: {CaptureDevice}. CaptureDeviceId: {CaptureDeviceId}. InputFormat: {InputSampleRate} Hz, {InputChannels} channels, {BitsPerSample}-bit {Encoding}. AecFormat: {AecSampleRate} Hz, 1 channel, FrameMs: {FrameMs}, FrameSize: {FrameSize}.",
                captureDevice.FriendlyName,
                captureDevice.ID,
                format.SampleRate,
                format.Channels,
                format.BitsPerSample,
                format.Encoding,
                _targetSampleRate,
                Math.Max(10, options.FrameMs),
                _frameSize);

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

                        var mono = BargeInAudioFrameConverter.ConvertCaptureBufferToMonoFloat(buffer, framesAvailable, format, flags.HasFlag(AudioClientBufferFlags.Silent));
                        var converted = BargeInAudioFrameConverter.ResampleMono(mono, format.SampleRate, _targetSampleRate);
                        if (format.SampleRate != _targetSampleRate || format.Channels != 1)
                        {
                            _logger.LogDebug(
                                "WebRTC APM mic frame converted. InputSampleRate: {InputSampleRate}. InputChannels: {InputChannels}. OutputSampleRate: {OutputSampleRate}. OutputChannels: 1.",
                                format.SampleRate,
                                format.Channels,
                                _targetSampleRate);
                        }

                        await EmitFramesAsync(context, converted, cancellationToken);
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
            _logger.LogError(exception, "WebRTC APM microphone capture failed. No degraded/raw natural barge-in fallback will be started.");
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

    private async Task EmitFramesAsync(BargeInSpeechContext context, float[] samples, CancellationToken cancellationToken)
    {
        lock (_syncRoot)
        {
            _pendingSamples.AddRange(samples);
        }

        while (true)
        {
            float[] frame;
            lock (_syncRoot)
            {
                if (_pendingSamples.Count < _frameSize)
                {
                    return;
                }

                frame = _pendingSamples.Take(_frameSize).ToArray();
                _pendingSamples.RemoveRange(0, _frameSize);
            }

            var frameCount = Interlocked.Increment(ref _nearEndFramesReceived);
            _diagnostics.MicFrameProcessed(context, frameCount);
            await _coordinator.ProcessMicrophoneFrameAsync(
                new BargeInAudioFrame
                {
                    Samples = frame,
                    SampleRate = _targetSampleRate,
                    Timestamp = DateTimeOffset.UtcNow
                },
                cancellationToken);
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
            _pendingSamples.Clear();
        }

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        _logger.LogInformation(
            "WebRTC APM microphone capture stopping. Reason: {Reason}. NearEndFramesReceived: {NearEndFramesReceived}.",
            reason,
            Interlocked.Read(ref _nearEndFramesReceived));
        _ = captureTask?.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static int NormalizeSampleRate(int sampleRate)
    {
        return sampleRate is 8000 or 16000 or 32000 or 48000 ? sampleRate : 48000;
    }

    private static Role ParseRole(string value, Role fallback)
    {
        return Enum.TryParse<Role>(value, ignoreCase: true, out var role) ? role : fallback;
    }

    public void Dispose()
    {
        StopCapture("disposed");
    }
}
