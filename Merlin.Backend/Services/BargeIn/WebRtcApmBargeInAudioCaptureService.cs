using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Merlin.Backend.Services.BargeIn;

public sealed class WebRtcApmBargeInAudioCaptureService : IBargeInAudioCaptureService, IHostedService, IDisposable
{
    private readonly IBargeInCoordinator _coordinator;
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly IContinuousMicAudioBuffer _continuousMicAudioBuffer;
    private readonly ILogger<WebRtcApmBargeInAudioCaptureService> _logger;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly object _syncRoot = new();
    private readonly List<float> _pendingSamples = [];
    private CancellationTokenSource? _captureCancellation;
    private BargeInAnalysisFrameQueue? _analysisQueue;
    private Task? _analysisTask;
    private Task? _captureTask;
    private BargeInSpeechContext? _activeContext;
    private long _nearEndFramesReceived;
    private int _targetSampleRate = 48000;
    private int _frameSize = 480;

    public WebRtcApmBargeInAudioCaptureService(
        IPlaybackReferenceTap playbackReferenceTap,
        IBargeInCoordinator coordinator,
        IBargeInDiagnosticsLogger diagnostics,
        IContinuousMicAudioBuffer continuousMicAudioBuffer,
        IOptionsMonitor<BargeInOptions> options,
        ILogger<WebRtcApmBargeInAudioCaptureService> logger)
    {
        _playbackReferenceTap = playbackReferenceTap;
        _coordinator = coordinator;
        _diagnostics = diagnostics;
        _continuousMicAudioBuffer = continuousMicAudioBuffer;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _playbackReferenceTap.SpeechStarted += OnSpeechStarted;
        _playbackReferenceTap.SpeechStopped += OnSpeechStopped;
        OnSpeechStarted(this, CreateLiveMonitorContext());
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
            _analysisQueue = new BargeInAnalysisFrameQueue(Math.Max(1, options.AnalysisQueueCapacityFrames));
            _analysisTask = Task.Run(
                () => AnalysisLoopAsync(context, _analysisQueue, _captureCancellation.Token),
                CancellationToken.None);
            _captureTask = Task.Run(
                () => CaptureLoopAsync(context, _captureCancellation.Token),
                CancellationToken.None);
        }
    }

    private void OnSpeechStopped(object? sender, BargeInSpeechContext context)
    {
        lock (_syncRoot)
        {
            if (_activeContext is not null
                && !string.Equals(_activeContext.AssistantTurnId, context.AssistantTurnId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        StopCapture("speech_stopped");
        OnSpeechStarted(this, CreateLiveMonitorContext());
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

                        EmitFrames(context, converted);
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

    private void EmitFrames(BargeInSpeechContext context, float[] samples)
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
            var rawFrame = new BargeInAudioFrame
            {
                Samples = frame,
                SampleRate = _targetSampleRate,
                Timestamp = DateTimeOffset.UtcNow,
                DurationMs = Math.Max(1, (int)Math.Round(frame.Length * 1000.0 / _targetSampleRate))
            };
            var recordedFrame = _continuousMicAudioBuffer.Append(rawFrame, _options.CurrentValue);
            BargeInAnalysisFrameQueue? queue;
            lock (_syncRoot)
            {
                queue = _analysisQueue;
            }

            queue?.Enqueue(recordedFrame);
        }
    }

    private async Task AnalysisLoopAsync(
        BargeInSpeechContext context,
        BargeInAnalysisFrameQueue queue,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var frame = await queue.DequeueAsync(cancellationToken);
                if (frame is null)
                {
                    continue;
                }

                await _coordinator.ProcessMicrophoneFrameAsync(frame, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "WebRTC APM barge-in analysis frame processing failed. Capture will continue.");
            }
        }
    }

    private void StopCapture(string reason)
    {
        CancellationTokenSource? cancellation;
        Task? captureTask;
        Task? analysisTask;
        BargeInAnalysisFrameQueue? analysisQueue;
        lock (_syncRoot)
        {
            cancellation = _captureCancellation;
            captureTask = _captureTask;
            analysisTask = _analysisTask;
            analysisQueue = _analysisQueue;
            _captureCancellation = null;
            _captureTask = null;
            _analysisTask = null;
            _analysisQueue = null;
            _pendingSamples.Clear();
        }

        if (cancellation is null)
        {
            return;
        }

        cancellation.Cancel();
        _logger.LogInformation(
            "WebRTC APM microphone capture stopping. Reason: {Reason}. NearEndFramesReceived: {NearEndFramesReceived}. AnalysisFramesDropped: {AnalysisFramesDropped}.",
            reason,
            Interlocked.Read(ref _nearEndFramesReceived),
            analysisQueue?.DroppedFrames ?? 0);
        _ = captureTask?.ContinueWith(
            _ => cancellation.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        _ = analysisTask;
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

    private static BargeInSpeechContext CreateLiveMonitorContext()
    {
        return new BargeInSpeechContext
        {
            AssistantTurnId = "live-utterance-monitor",
            CorrelationId = null,
            SpeechType = Merlin.Backend.Models.SpeechPlaybackItemType.FinalAnswer,
            SpokenText = string.Empty
        };
    }
}
