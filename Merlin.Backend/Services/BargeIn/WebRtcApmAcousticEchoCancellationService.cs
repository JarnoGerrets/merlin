using SoundFlow.Extensions.WebRtc.Apm;

namespace Merlin.Backend.Services.BargeIn;

public sealed class WebRtcApmAcousticEchoCancellationService : IAcousticEchoCancellationService
{
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly ILogger<WebRtcApmAcousticEchoCancellationService> _logger;
    private readonly object _syncRoot = new();
    private AudioProcessingModule? _apm;
    private StreamConfig? _streamConfig;
    private int _sampleRate = 48000;
    private int _channels = 1;
    private int _frameMs = 10;
    private int _frameSize = 480;
    private bool _active;
    private string _initializationError = "";
    private long _farEndFramesReceived;
    private long _nearEndFramesReceived;
    private long _echoReducedFramesProduced;

    public WebRtcApmAcousticEchoCancellationService(
        IBargeInDiagnosticsLogger diagnostics,
        ILogger<WebRtcApmAcousticEchoCancellationService> logger)
    {
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            DisposeNative();
            _sampleRate = NormalizeSampleRate(config.SampleRate);
            _channels = 1;
            _frameMs = Math.Clamp(config.FrameMs, 10, 10);
            _frameSize = AudioProcessingModule.GetFrameSize(_sampleRate);
            _farEndFramesReceived = 0;
            _nearEndFramesReceived = 0;
            _echoReducedFramesProduced = 0;
            _initializationError = "";

            try
            {
                _apm = new AudioProcessingModule();
                using var apmConfig = new ApmConfig();
                apmConfig.SetPipeline(_sampleRate, false, false, DownmixMethod.AverageChannels);
                apmConfig.SetEchoCanceller(enabled: true, mobileMode: false);
                apmConfig.SetHighPassFilter(true);
                apmConfig.SetNoiseSuppression(enabled: true, NoiseSuppressionLevel.Moderate);
                apmConfig.SetGainController1(false, GainControlMode.AdaptiveDigital, -3, 9, true);
                apmConfig.SetGainController2(false);
                apmConfig.SetPreAmplifier(false, 1.0f);

                ThrowIfError(_apm.ApplyConfig(apmConfig), "ApplyConfig");
                ThrowIfError(_apm.Initialize(), "Initialize");
                _apm.SetStreamDelayMs(100);
                _streamConfig = new StreamConfig(_sampleRate, _channels);
                _active = true;
                var reason = $"WebRTC APM active. ProviderName: WebRtcApm. IsRealAec: True. NativeLocalApmLoaded: True. SampleRate: {_sampleRate}. Channels: {_channels}. FrameMs: {_frameMs}. FrameSize: {_frameSize}.";
                _diagnostics.AecInitialized(AecMode.Active, reason);
                _logger.LogInformation(
                    "WebRTC APM initialized. ProviderName: WebRtcApm. IsRealAec: True. IsActive: True. IsDegraded: False. NativeLocalApmLoaded: True. SampleRate: {SampleRate}. Channels: {Channels}. FrameMs: {FrameMs}. FrameSize: {FrameSize}.",
                    _sampleRate,
                    _channels,
                    _frameMs,
                    _frameSize);
            }
            catch (Exception exception)
            {
                _active = false;
                _initializationError = exception.Message;
                DisposeNative();
                var reason = $"WebRTC APM initialization failed. ProviderName: WebRtcApm. IsRealAec: True. IsActive: False. IsDegraded: False. NativeLocalApmLoaded: False. InitializationError: {_initializationError}";
                _diagnostics.AecInitialized(AecMode.Unavailable, reason);
                _logger.LogError(exception, "WebRTC APM initialization failed. Natural barge-in remains disabled.");
            }
        }

        return Task.CompletedTask;
    }

    public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
    {
        lock (_syncRoot)
        {
            if (!_active || _apm is null || _streamConfig is null)
            {
                return new AecProcessResult
                {
                    EchoReducedFrame = microphoneFrame,
                    Mode = AecMode.Unavailable,
                    IsEchoCancellationActive = false,
                    Reason = string.IsNullOrWhiteSpace(_initializationError)
                        ? "WebRTC APM is not initialized."
                        : $"WebRTC APM initialization failed: {_initializationError}"
                };
            }

            var nearEnd = NormalizeFrame(microphoneFrame.Span);
            var farEnd = NormalizeFrame(playbackReferenceFrame.Span);
            var farEndChannels = new[] { farEnd };
            var nearEndChannels = new[] { nearEnd };
            var outputChannels = new[] { new float[_frameSize] };

            var farEndCount = Interlocked.Increment(ref _farEndFramesReceived);
            var nearEndCount = Interlocked.Increment(ref _nearEndFramesReceived);
            var reverseResult = _apm.AnalyzeReverseStream(farEndChannels, _streamConfig);
            if (reverseResult != ApmError.NoError)
            {
                _logger.LogWarning(
                    "WebRTC APM far-end AnalyzeReverseStream failed. Error: {Error}. FarEndFramesReceived: {FarEndFramesReceived}.",
                    reverseResult,
                    farEndCount);
            }

            var processResult = _apm.ProcessStream(nearEndChannels, _streamConfig, _streamConfig, outputChannels);
            if (processResult != ApmError.NoError)
            {
                return new AecProcessResult
                {
                    EchoReducedFrame = microphoneFrame,
                    Mode = AecMode.Unavailable,
                    IsEchoCancellationActive = false,
                    Reason = $"WebRTC APM ProcessStream failed: {processResult}."
                };
            }

            var echoReducedCount = Interlocked.Increment(ref _echoReducedFramesProduced);
            _logger.LogDebug(
                "WebRTC APM frame processed. FarEndFramesReceived: {FarEndFramesReceived}. NearEndFramesReceived: {NearEndFramesReceived}. EchoReducedFramesProduced: {EchoReducedFramesProduced}. VadConsumesEchoReduced: True.",
                farEndCount,
                nearEndCount,
                echoReducedCount);
            return new AecProcessResult
            {
                EchoReducedFrame = outputChannels[0],
                Mode = AecMode.Active,
                IsEchoCancellationActive = true,
                Reason = "WebRTC APM produced echo-reduced near-end audio."
            };
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            DisposeNative();
        }

        return ValueTask.CompletedTask;
    }

    private float[] NormalizeFrame(ReadOnlySpan<float> samples)
    {
        var output = new float[_frameSize];
        var count = Math.Min(samples.Length, output.Length);
        samples[..count].CopyTo(output);
        if (count < output.Length)
        {
            _logger.LogDebug(
                "WebRTC APM frame padded. InputSamples: {InputSamples}. RequiredSamples: {RequiredSamples}.",
                samples.Length,
                output.Length);
        }
        else if (samples.Length > output.Length)
        {
            _logger.LogDebug(
                "WebRTC APM frame truncated. InputSamples: {InputSamples}. RequiredSamples: {RequiredSamples}.",
                samples.Length,
                output.Length);
        }

        return output;
    }

    private void DisposeNative()
    {
        _active = false;
        _streamConfig?.Dispose();
        _streamConfig = null;
        _apm?.Dispose();
        _apm = null;
    }

    private static int NormalizeSampleRate(int sampleRate)
    {
        return sampleRate is 8000 or 16000 or 32000 or 48000 ? sampleRate : 48000;
    }

    private static void ThrowIfError(ApmError error, string stage)
    {
        if (error != ApmError.NoError)
        {
            throw new InvalidOperationException($"WebRTC APM {stage} failed: {error}.");
        }
    }
}
