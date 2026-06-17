using System.Diagnostics;
using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;
using NAudio.Wave;

namespace Merlin.Backend.Services;

public sealed class AssistantSpeechPlaybackService : IAssistantSpeechPlaybackService
{
    private const int EnergyEventIntervalMs = 80;
    private const int OutputDrainTailMs = 350;
    private const int DrainPollMs = 20;
    private const int BufferSpacePollMs = 10;
    private const int PlaybackBufferSeconds = 30;
    private readonly IVoiceSynthesisService _voiceSynthesisService;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly ISpeakerDuckingService _speakerDuckingService;
    private readonly ILogger<AssistantSpeechPlaybackService> _logger;
    private readonly SemaphoreSlim _speechGate = new(1, 1);
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _activeSpeechCancellation;
    private long _queueGeneration;

    public AssistantSpeechPlaybackService(
        IVoiceSynthesisService voiceSynthesisService,
        IPlaybackReferenceTap playbackReferenceTap,
        ISpeakerDuckingService speakerDuckingService,
        ILogger<AssistantSpeechPlaybackService> logger)
    {
        _voiceSynthesisService = voiceSynthesisService;
        _playbackReferenceTap = playbackReferenceTap;
        _speakerDuckingService = speakerDuckingService;
        _logger = logger;
    }

    public Task EnqueueAsync(
        string text,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? speechCacheKey,
        bool? isReplayableSpeech,
        CancellationToken cancellationToken,
        SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
        bool cancelOnlyBeforePlayback = false)
    {
        var spokenText = text.Trim();
        if (string.IsNullOrWhiteSpace(spokenText))
        {
            return Task.CompletedTask;
        }

        var queueGeneration = Volatile.Read(ref _queueGeneration);
        _ = Task.Run(
            () => SpeakQueuedAsync(
                spokenText,
                correlationId,
                queueGeneration,
                sendEventAsync,
                speechCacheKey,
                isReplayableSpeech,
                itemType,
                cancelOnlyBeforePlayback,
                cancellationToken),
            CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopCurrentAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cancellation;
        lock (_syncRoot)
        {
            cancellation = _activeSpeechCancellation;
        }

        cancellation?.Cancel();
        return Task.CompletedTask;
    }

    public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Speech playback soft pause requested. Current backend uses speaker ducking as the safe pause fallback.");
        return Task.CompletedTask;
    }

    public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Speech playback resume requested. Current backend restores speaker ducking volume and keeps the active queue alive.");
        return Task.CompletedTask;
    }

    public Task ClearQueueAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _queueGeneration);
        return StopCurrentAsync(cancellationToken);
    }

    private async Task SpeakQueuedAsync(
        string text,
        string? correlationId,
        long queueGeneration,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? speechCacheKey,
        bool? isReplayableSpeech,
        SpeechPlaybackItemType itemType,
        bool cancelOnlyBeforePlayback,
        CancellationToken cancellationToken)
    {
        try
        {
            if (itemType is SpeechPlaybackItemType.FinalAnswer && _speechGate.CurrentCount == 0)
            {
                _logger.LogInformation(
                    "Final answer waiting for active acknowledgement/progress playback to finish. CorrelationId: {CorrelationId}.",
                    correlationId);
            }

            await _speechGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Speech playback item cancelled before start. CorrelationId: {CorrelationId}. ItemType: {ItemType}.",
                correlationId,
                itemType);
            return;
        }

        using var speechCancellation = cancelOnlyBeforePlayback
            ? new CancellationTokenSource()
            : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_syncRoot)
        {
            _activeSpeechCancellation = speechCancellation;
        }

        try
        {
            if (queueGeneration != Volatile.Read(ref _queueGeneration))
            {
                _logger.LogInformation(
                    "Speech playback item skipped because queue generation changed. CorrelationId: {CorrelationId}. ItemType: {ItemType}.",
                    correlationId,
                    itemType);
                return;
            }

            if (cancelOnlyBeforePlayback && cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Speech playback item cancelled before active playback. CorrelationId: {CorrelationId}. ItemType: {ItemType}.",
                    correlationId,
                    itemType);
                return;
            }

            if (itemType is SpeechPlaybackItemType.FinalAnswer)
            {
                _logger.LogInformation(
                    "Final answer playback allowed to start. CorrelationId: {CorrelationId}.",
                    correlationId);
            }
            else
            {
                _logger.LogInformation(
                    "Speech playback item active. CorrelationId: {CorrelationId}. ItemType: {ItemType}. Active playback will not be interrupted by pending-work cancellation.",
                    correlationId,
                    itemType);
                if (itemType is SpeechPlaybackItemType.Progress)
                {
                    _logger.LogInformation(
                        "Progress playback not interrupted because it was already active. CorrelationId: {CorrelationId}.",
                        correlationId);
                }
            }

            await SpeakAsync(
                text,
                correlationId,
                sendEventAsync,
                speechCacheKey,
                isReplayableSpeech,
                itemType,
                speechCancellation.Token);

            if (itemType is SpeechPlaybackItemType.Acknowledgement or SpeechPlaybackItemType.Progress)
            {
                _logger.LogInformation(
                    "Active acknowledgement/progress playback completed. CorrelationId: {CorrelationId}. ItemType: {ItemType}.",
                    correlationId,
                    itemType);
            }
        }
        catch (OperationCanceledException)
        {
            await TrySendEventAsync(sendEventAsync, "SPEAKING_CANCELLED", correlationId, null, cancellationToken);
            _logger.LogInformation("Backend speech playback cancelled. CorrelationId: {CorrelationId}.", correlationId);
        }
        catch (Exception exception)
        {
            await TrySendEventAsync(sendEventAsync, "SPEAKING_CANCELLED", correlationId, exception.Message, cancellationToken);
            _logger.LogError(exception, "Backend speech playback failed. CorrelationId: {CorrelationId}.", correlationId);
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeSpeechCancellation, speechCancellation))
                {
                    _activeSpeechCancellation = null;
                }
            }

            _speechGate.Release();
        }
    }

    private async Task SpeakAsync(
        string text,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? speechCacheKey,
        bool? isReplayableSpeech,
        SpeechPlaybackItemType itemType,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        WaveOutEvent? waveOut = null;
        BufferedWaveProvider? bufferedWaveProvider = null;
        var playbackStarted = false;
        var playbackStopwatch = new Stopwatch();
        var lastEnergyEvent = Stopwatch.StartNew();
        var totalBytes = 0;
        var sampleRate = 0;
        var channels = 0;
        var speechContext = new BargeInSpeechContext
        {
            AssistantTurnId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            CorrelationId = correlationId,
            SpeechType = itemType,
            SpokenText = text
        };

        try
        {
            using var logContext = SpeechSynthesisLogContext.Push(speechCacheKey, isReplayableSpeech);
            await _voiceSynthesisService.StreamSynthesizeAsync(
                text,
                (metadata, token) =>
                {
                    if (!string.Equals(metadata.Format, "s16le", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException($"Unsupported backend playback format: {metadata.Format}");
                    }

                    var waveFormat = new WaveFormat(metadata.SampleRate, 16, metadata.Channels);
                    sampleRate = metadata.SampleRate;
                    channels = metadata.Channels;
                    bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
                    {
                        BufferDuration = TimeSpan.FromSeconds(PlaybackBufferSeconds),
                        DiscardOnBufferOverflow = false
                    };
                    waveOut = new WaveOutEvent
                    {
                        DesiredLatency = 100
                    };
                    waveOut.Init(bufferedWaveProvider);
                    return Task.CompletedTask;
                },
                async (audio, token) =>
                {
                    if (bufferedWaveProvider is null || waveOut is null)
                    {
                        return;
                    }

                    var buffer = audio.ToArray();
                    await WaitForPlaybackBufferSpaceAsync(bufferedWaveProvider, buffer.Length, token);
                    _playbackReferenceTap.PushPcm16Reference(buffer, sampleRate, channels, correlationId);
                    bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);
                    totalBytes += buffer.Length;

                    if (!playbackStarted)
                    {
                        playbackStarted = true;
                        playbackStopwatch.Start();
                        waveOut.Volume = _speakerDuckingService.CurrentVolumeMultiplier;
                        waveOut.Play();
                        _playbackReferenceTap.NotifySpeechStarted(speechContext);
                        await TrySendEventAsync(sendEventAsync, "SPEAKING_START", correlationId, null, token);
                        _logger.LogInformation(
                            "Voice timing: backend playback started. CorrelationId: {CorrelationId}. ElapsedMs: {ElapsedMs}.",
                            correlationId,
                            stopwatch.Elapsed.TotalMilliseconds);
                    }

                    if (lastEnergyEvent.ElapsedMilliseconds >= EnergyEventIntervalMs)
                    {
                        lastEnergyEvent.Restart();
                        waveOut.Volume = _speakerDuckingService.CurrentVolumeMultiplier;
                        await TrySendEnergyAsync(sendEventAsync, correlationId, buffer, token);
                    }
                },
                cancellationToken);

            if (waveOut is not null && bufferedWaveProvider is not null)
            {
                var drainStartedMs = stopwatch.Elapsed.TotalMilliseconds;
                await WaitForPlaybackDrainAsync(
                    bufferedWaveProvider,
                    playbackStopwatch,
                    totalBytes,
                    sampleRate,
                    channels,
                    cancellationToken);
                _logger.LogInformation(
                    "Voice timing: backend playback drain complete. CorrelationId: {CorrelationId}. DrainMs: {DrainMs}. PlaybackElapsedMs: {PlaybackElapsedMs}. BufferedBytes: {BufferedBytes}.",
                    correlationId,
                    stopwatch.Elapsed.TotalMilliseconds - drainStartedMs,
                    playbackStopwatch.Elapsed.TotalMilliseconds,
                    bufferedWaveProvider.BufferedBytes);

                waveOut.Stop();
            }

            if (playbackStarted)
            {
                await TrySendEventAsync(sendEventAsync, "SPEAKING_END", correlationId, null, cancellationToken);
            }

            _logger.LogInformation(
                "Voice timing: backend playback complete. CorrelationId: {CorrelationId}. Bytes: {Bytes}. ElapsedMs: {ElapsedMs}.",
                correlationId,
                totalBytes,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            if (playbackStarted)
            {
                _playbackReferenceTap.NotifySpeechStopped(speechContext);
            }

            waveOut?.Dispose();
        }
    }

    private static async Task WaitForPlaybackBufferSpaceAsync(
        BufferedWaveProvider bufferedWaveProvider,
        int incomingBytes,
        CancellationToken cancellationToken)
    {
        while (bufferedWaveProvider.BufferedBytes + incomingBytes > bufferedWaveProvider.BufferLength)
        {
            await Task.Delay(BufferSpacePollMs, cancellationToken);
        }
    }

    private async Task WaitForPlaybackDrainAsync(
        BufferedWaveProvider bufferedWaveProvider,
        Stopwatch playbackStopwatch,
        int totalBytes,
        int sampleRate,
        int channels,
        CancellationToken cancellationToken)
    {
        var expectedPlaybackMs = CalculateExpectedPlaybackMs(totalBytes, sampleRate, channels);
        var targetElapsedMs = expectedPlaybackMs + OutputDrainTailMs;

        while (!cancellationToken.IsCancellationRequested)
        {
            var providerDrained = bufferedWaveProvider.BufferedBytes <= 0;
            var deviceElapsed = playbackStopwatch.IsRunning
                ? playbackStopwatch.ElapsedMilliseconds >= targetElapsedMs
                : expectedPlaybackMs <= 0;

            if (providerDrained && deviceElapsed)
            {
                return;
            }

            await Task.Delay(DrainPollMs, cancellationToken);
        }
    }

    private static long CalculateExpectedPlaybackMs(int totalBytes, int sampleRate, int channels)
    {
        if (totalBytes <= 0 || sampleRate <= 0 || channels <= 0)
        {
            return 0;
        }

        var bytesPerFrame = channels * sizeof(short);
        if (bytesPerFrame <= 0)
        {
            return 0;
        }

        var totalFrames = totalBytes / bytesPerFrame;
        return (long)Math.Ceiling(totalFrames * 1000.0 / sampleRate);
    }

    private static async Task TrySendEnergyAsync(
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? correlationId,
        byte[] pcm,
        CancellationToken cancellationToken)
    {
        try
        {
            var energy = CalculatePcm16Energy(pcm);
            await sendEventAsync(
                new AssistantVisualEvent
                {
                    Event = "SPEECH_ENERGY",
                    Value = energy,
                    CorrelationId = correlationId
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.Net.WebSockets.WebSocketException)
        {
        }
    }

    private static async Task TrySendEventAsync(
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string eventName,
        string? correlationId,
        string? detail,
        CancellationToken cancellationToken)
    {
        try
        {
            await sendEventAsync(
                new AssistantVisualEvent
                {
                    Event = eventName,
                    CorrelationId = correlationId,
                    Detail = detail
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.Net.WebSockets.WebSocketException)
        {
        }
    }

    private static double CalculatePcm16Energy(byte[] pcm)
    {
        if (pcm.Length < 2)
        {
            return 0.0;
        }

        double sumSquares = 0.0;
        var sampleCount = 0;
        for (var index = 0; index + 1 < pcm.Length; index += 2)
        {
            var sample = (short)(pcm[index] | (pcm[index + 1] << 8));
            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
            sampleCount++;
        }

        if (sampleCount <= 0)
        {
            return 0.0;
        }

        var rms = Math.Sqrt(sumSquares / sampleCount);
        return Math.Clamp(rms * 4.0, 0.0, 1.0);
    }
}
