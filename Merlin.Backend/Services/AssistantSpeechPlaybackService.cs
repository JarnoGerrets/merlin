using System.Diagnostics;
using System.Collections.Concurrent;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Options;
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
    private readonly ILiveSpokenAnswerTrackingService? _spokenAnswerTracking;
    private readonly InterruptionHandlingOptions _interruptionOptions;
    private readonly ILogger<AssistantSpeechPlaybackService> _logger;
    private readonly Func<IWavePlayer> _wavePlayerFactory;
    private readonly SemaphoreSlim _speechGate = new(1, 1);
    private readonly ConcurrentDictionary<string, long> _finalAnswerGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _playbackControlLock = new();
    private readonly object _volumeSyncRoot = new();
    private CancellationTokenSource? _activeSpeechCancellation;
    private ActiveSpeechPlaybackSnapshot? _activePlaybackSnapshot;
    private ActivePlaybackControlState? _activePlaybackState;
    private Action<float>? _activeVolumeSetter;
    private long _queueGeneration;

    public AssistantSpeechPlaybackService(
        IVoiceSynthesisService voiceSynthesisService,
        IPlaybackReferenceTap playbackReferenceTap,
        ISpeakerDuckingService speakerDuckingService,
        ILogger<AssistantSpeechPlaybackService> logger,
        ILiveSpokenAnswerTrackingService? spokenAnswerTracking = null,
        IOptions<InterruptionHandlingOptions>? interruptionOptions = null,
        Func<IWavePlayer>? wavePlayerFactory = null)
    {
        _voiceSynthesisService = voiceSynthesisService;
        _playbackReferenceTap = playbackReferenceTap;
        _speakerDuckingService = speakerDuckingService;
        _spokenAnswerTracking = spokenAnswerTracking;
        _interruptionOptions = interruptionOptions?.Value ?? new InterruptionHandlingOptions();
        _logger = logger;
        _wavePlayerFactory = wavePlayerFactory ?? (() => new WaveOutEvent { DesiredLatency = 100 });
        _speakerDuckingService.DuckingChanged += OnDuckingChanged;
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
        var turnGeneration = itemType is SpeechPlaybackItemType.FinalAnswer
            ? GetFinalAnswerGeneration(correlationId)
            : 0;
        _ = Task.Run(
            () => SpeakQueuedAsync(
                spokenText,
                correlationId,
                queueGeneration,
                turnGeneration,
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
        lock (_playbackControlLock)
        {
            cancellation = _activeSpeechCancellation;
        }

        cancellation?.Cancel();
        return Task.CompletedTask;
    }

    public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cancellation;
        lock (_playbackControlLock)
        {
            cancellation = _activeSpeechCancellation;
        }

        if (cancellation is null)
        {
            _logger.LogInformation("Speech playback soft pause requested, but no active playback was available to pause.");
            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "PlaybackYieldUnavailableFallbackUsed. True resumable playback pause is unavailable; cancelling current audio output while keeping the speech queue generation and active turn intact.");
        cancellation.Cancel();
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

    public Task<ProvisionalAudioHoldResult> BeginProvisionalAudioHoldAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return Task.FromResult(ProvisionalAudioHoldResult.Failed(turnId, reason, "Turn id is required."));
        }

        var normalizedTurnId = turnId.Trim();
        lock (_playbackControlLock)
        {
            var state = _activePlaybackState;
            if (!IsHoldableActivePlayback(state, normalizedTurnId))
            {
                _logger.LogInformation(
                    "provisional_audio_hold_unavailable TurnId: {TurnId}. Reason: {Reason}. ActiveTurnId: {ActiveTurnId}. ItemType: {ItemType}. HasWaveOut: {HasWaveOut}. IsHeld: {IsHeld}.",
                    normalizedTurnId,
                    reason,
                    state?.TurnId,
                    state?.ItemType,
                    state?.WavePlayer is not null,
                    state?.IsHeld);
                return Task.FromResult(ProvisionalAudioHoldResult.Failed(normalizedTurnId, reason, "No active holdable final-answer playback is available."));
            }

            if (state!.IsHeld)
            {
                RefreshHoldTimeoutLocked(state);
                _logger.LogInformation(
                    "provisional_audio_hold_already_active TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. ItemType: {ItemType}. Reason: {Reason}. BufferedBytes: {BufferedBytes}. BufferDuration: {BufferDuration}. GenerationId: {GenerationId}.",
                    state.TurnId,
                    state.CorrelationId,
                    state.HoldId,
                    state.ItemType,
                    reason,
                    state.BufferedWaveProvider?.BufferedBytes,
                    state.BufferedWaveProvider?.BufferDuration,
                    state.TurnGeneration);
                return Task.FromResult(new ProvisionalAudioHoldResult(
                    Success: true,
                    HoldId: state.HoldId,
                    TurnId: state.TurnId,
                    Reason: reason,
                    WasAlreadyHeld: true));
            }

            var holdId = Guid.NewGuid().ToString("N");
            state.WavePlayer!.Pause();
            state.IsHeld = true;
            state.HoldId = holdId;
            state.HoldStartedAtUtc = DateTimeOffset.UtcNow;
            state.HoldReason = reason;
            UpdateActivePlaybackSnapshotLocked(state);
            RefreshHoldTimeoutLocked(state);

            _logger.LogInformation(
                "audible_playback_state_changed TurnId: {TurnId}. CorrelationId: {CorrelationId}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. AudiblePlaybackActive: {AudiblePlaybackActive}. ActivePlaybackSnapshotIsActive: {ActivePlaybackSnapshotIsActive}. ActivePlaybackSnapshotIsHeld: {ActivePlaybackSnapshotIsHeld}. HoldId: {HoldId}. Reason: {Reason}.",
                state.TurnId,
                state.CorrelationId,
                true,
                false,
                false,
                true,
                true,
                state.HoldId,
                reason);

            _logger.LogInformation(
                "provisional_audio_hold_started TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. ItemType: {ItemType}. Reason: {Reason}. BufferedBytes: {BufferedBytes}. BufferDuration: {BufferDuration}. GenerationId: {GenerationId}.",
                state.TurnId,
                state.CorrelationId,
                state.HoldId,
                state.ItemType,
                reason,
                state.BufferedWaveProvider?.BufferedBytes,
                state.BufferedWaveProvider?.BufferDuration,
                state.TurnGeneration);

            return Task.FromResult(new ProvisionalAudioHoldResult(
                Success: true,
                HoldId: holdId,
                TurnId: state.TurnId,
                Reason: reason));
        }
    }

    public Task<ProvisionalAudioHoldResult> ResumeProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(holdId))
        {
            return Task.FromResult(ProvisionalAudioHoldResult.Failed(null, reason, "Hold id is required.", holdId));
        }

        lock (_playbackControlLock)
        {
            var state = _activePlaybackState;
            if (!IsMatchingHeldPlayback(state, holdId))
            {
                return Task.FromResult(ProvisionalAudioHoldResult.Failed(state?.TurnId, reason, "No active provisional audio hold matches the requested hold id.", holdId));
            }

            ResumeHeldPlaybackLocked(state!, reason, "provisional_audio_hold_resumed");
            return Task.FromResult(new ProvisionalAudioHoldResult(
                Success: true,
                HoldId: holdId,
                TurnId: state!.TurnId,
                Reason: reason,
                WasResumed: true));
        }
    }

    public Task<ProvisionalAudioHoldResult> FlushProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(holdId))
        {
            return Task.FromResult(ProvisionalAudioHoldResult.Failed(null, reason, "Hold id is required.", holdId));
        }

        CancellationTokenSource? cancellation;
        string? turnId;
        lock (_playbackControlLock)
        {
            var state = _activePlaybackState;
            if (!IsMatchingHeldPlayback(state, holdId))
            {
                return Task.FromResult(ProvisionalAudioHoldResult.Failed(state?.TurnId, reason, "No active provisional audio hold matches the requested hold id.", holdId));
            }

            var heldDurationMs = GetHeldDurationMs(state!);
            var generationBeforeFlush = state!.TurnGeneration;
            var newGeneration = IncrementFinalAnswerGeneration(state.TurnId);
            cancellation = state.Cancellation;
            turnId = state.TurnId;
            ClearHoldLocked(state);
            UpdateActivePlaybackSnapshotLocked(state);
            _logger.LogInformation(
                "provisional_audio_hold_flushed TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. Reason: {Reason}. HeldDurationMs: {HeldDurationMs}. GenerationIdBeforeFlush: {GenerationIdBeforeFlush}. CurrentGenerationId: {CurrentGenerationId}.",
                state.TurnId,
                state.CorrelationId,
                holdId,
                reason,
                heldDurationMs,
                generationBeforeFlush,
                newGeneration);
            _logger.LogInformation(
                "conversational_interruption_speech_channel_flushed TurnId: {TurnId}. CorrelationId: {CorrelationId}. SpeechType: {SpeechType}. ItemType: {ItemType}. CurrentGenerationId: {CurrentGenerationId}. ActivePlaybackCancelled: {ActivePlaybackCancelled}. Reason: {Reason}.",
                state.TurnId,
                state.CorrelationId,
                state.ItemType,
                state.ItemType,
                newGeneration,
                true,
                reason);
        }

        cancellation?.Cancel();
        return Task.FromResult(new ProvisionalAudioHoldResult(
            Success: true,
            HoldId: holdId,
            TurnId: turnId,
            Reason: reason,
            WasFlushed: true));
    }

    public Task FlushFinalAnswerSpeechForTurnAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return Task.CompletedTask;
        }

        var normalizedTurnId = turnId.Trim();
        var newGeneration = IncrementFinalAnswerGeneration(normalizedTurnId);

        CancellationTokenSource? cancellation = null;
        ActiveSpeechPlaybackSnapshot? snapshot = null;
        lock (_playbackControlLock)
        {
            snapshot = _activePlaybackSnapshot;
            if (snapshot is { IsActive: true }
                && string.Equals(snapshot.AssistantTurnId, normalizedTurnId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.SpeechType, SpeechPlaybackItemType.FinalAnswer.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                cancellation = _activeSpeechCancellation;
            }
        }

        cancellation?.Cancel();
        _logger.LogInformation(
            "conversational_interruption_speech_channel_flushed TurnId: {TurnId}. CorrelationId: {CorrelationId}. SpeechType: {SpeechType}. ItemType: {ItemType}. CurrentGenerationId: {CurrentGenerationId}. ActivePlaybackCancelled: {ActivePlaybackCancelled}. Reason: {Reason}.",
            normalizedTurnId,
            snapshot?.CorrelationId,
            snapshot?.SpeechType ?? SpeechPlaybackItemType.FinalAnswer.ToString(),
            snapshot?.ItemType ?? SpeechPlaybackItemType.FinalAnswer.ToString(),
            newGeneration,
            cancellation is not null,
            reason);
        return Task.CompletedTask;
    }

    public ActiveSpeechPlaybackSnapshot? GetActivePlaybackSnapshot()
    {
        lock (_playbackControlLock)
        {
            return _activePlaybackSnapshot is null
                ? null
                : new ActiveSpeechPlaybackSnapshot
                {
                    CorrelationId = _activePlaybackSnapshot.CorrelationId,
                    AssistantTurnId = _activePlaybackSnapshot.AssistantTurnId,
                    SpeechType = _activePlaybackSnapshot.SpeechType,
                    ItemType = _activePlaybackSnapshot.ItemType,
                    IsActive = _activePlaybackSnapshot.IsActive,
                    IsHeld = _activePlaybackSnapshot.IsHeld,
                    IsAudiblePlaybackActive = _activePlaybackSnapshot.IsAudiblePlaybackActive,
                    HoldId = _activePlaybackSnapshot.HoldId,
                    StartedAtUtc = _activePlaybackSnapshot.StartedAtUtc
                };
        }
    }

    private async Task SpeakQueuedAsync(
        string text,
        string? correlationId,
        long queueGeneration,
        long turnGeneration,
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
        var activeState = new ActivePlaybackControlState
        {
            TurnId = string.IsNullOrWhiteSpace(correlationId) ? string.Empty : correlationId,
            CorrelationId = correlationId ?? string.Empty,
            ItemType = itemType,
            TurnGeneration = turnGeneration,
            Cancellation = speechCancellation,
            StartedAtUtc = DateTimeOffset.UtcNow
        };
        lock (_playbackControlLock)
        {
            _activeSpeechCancellation = speechCancellation;
            _activePlaybackState = activeState;
            UpdateActivePlaybackSnapshotLocked(activeState);
        }

        try
        {
            if (IsObsoleteFinalAnswer(correlationId, itemType, turnGeneration, out var currentGeneration))
            {
                LogObsoleteSpeechDiscarded(correlationId, itemType, turnGeneration, currentGeneration, "obsolete_before_playback_start");
                return;
            }

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
            else if (itemType is SpeechPlaybackItemType.InterruptionClarification
                     or SpeechPlaybackItemType.InterruptionContinuation
                     or SpeechPlaybackItemType.StopConfirmation)
            {
                _logger.LogInformation(
                    "Interruption-owned speech playback active. CorrelationId: {CorrelationId}. ItemType: {ItemType}.",
                    correlationId,
                    itemType);
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
                turnGeneration,
                activeState,
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
            if (itemType is SpeechPlaybackItemType.FinalAnswer && !string.IsNullOrWhiteSpace(correlationId))
            {
                _spokenAnswerTracking?.MarkPlaybackCancelled(correlationId, "backend_speech_playback_cancelled");
            }

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
            lock (_playbackControlLock)
            {
                if (ReferenceEquals(_activeSpeechCancellation, speechCancellation))
                {
                    activeState.HoldTimeoutCancellation?.Cancel();
                    activeState.HoldTimeoutCancellation?.Dispose();
                    _activeSpeechCancellation = null;
                    _activePlaybackState = null;
                    _activePlaybackSnapshot = null;
                }
            }

            _speechGate.Release();
        }
    }

    private async Task SpeakAsync(
        string text,
        string? correlationId,
        long turnGeneration,
        ActivePlaybackControlState activeState,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? speechCacheKey,
        bool? isReplayableSpeech,
        SpeechPlaybackItemType itemType,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        IWavePlayer? waveOut = null;
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
                    waveOut = _wavePlayerFactory();
                    waveOut.Init(new PlaybackReferenceWaveProvider(
                        bufferedWaveProvider,
                        _playbackReferenceTap,
                        correlationId));
                    lock (_playbackControlLock)
                    {
                        if (ReferenceEquals(_activePlaybackState, activeState))
                        {
                            activeState.WavePlayer = waveOut;
                            activeState.BufferedWaveProvider = bufferedWaveProvider;
                            UpdateActivePlaybackSnapshotLocked(activeState);
                        }
                    }
                    return Task.CompletedTask;
                },
                async (audio, token) =>
                {
                    if (IsObsoleteFinalAnswer(correlationId, itemType, turnGeneration, out var currentGeneration))
                    {
                        LogObsoleteSpeechDiscarded(correlationId, itemType, turnGeneration, currentGeneration, "obsolete_during_tts_stream");
                        throw new OperationCanceledException(token);
                    }

                    if (bufferedWaveProvider is null || waveOut is null)
                    {
                        return;
                    }

                    var buffer = audio.ToArray();
                    await WaitForPlaybackBufferSpaceAsync(bufferedWaveProvider, buffer.Length, token);
                    bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);
                    totalBytes += buffer.Length;

                    if (!playbackStarted)
                    {
                        playbackStarted = true;
                        playbackStopwatch.Start();
                        if (_speakerDuckingService.IsDucked)
                        {
                            _speakerDuckingService.Restore(speechContext, "playback_start_reset_stale_ducking");
                        }

                        SetActiveVolumeSetter(volume => waveOut.Volume = volume);
                        ApplyOutputVolume(_speakerDuckingService.CurrentVolumeMultiplier, "playback_start");
                        waveOut.Play();
                        _playbackReferenceTap.NotifySpeechStarted(speechContext);
                        if (itemType is SpeechPlaybackItemType.FinalAnswer && !string.IsNullOrWhiteSpace(correlationId))
                        {
                            _spokenAnswerTracking?.MarkChunkStarted(correlationId, text, playbackStopwatch.Elapsed);
                        }

                        await TrySendEventAsync(sendEventAsync, "SPEAKING_START", correlationId, null, token);
                        _logger.LogInformation(
                            "Voice timing: backend playback started. CorrelationId: {CorrelationId}. ElapsedMs: {ElapsedMs}.",
                            correlationId,
                            stopwatch.Elapsed.TotalMilliseconds);
                    }

                    if (lastEnergyEvent.ElapsedMilliseconds >= EnergyEventIntervalMs)
                    {
                        lastEnergyEvent.Restart();
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
                    activeState,
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
                if (itemType is SpeechPlaybackItemType.FinalAnswer && !string.IsNullOrWhiteSpace(correlationId))
                {
                    _spokenAnswerTracking?.MarkChunkCompleted(correlationId, text, playbackStopwatch.Elapsed);
                    _spokenAnswerTracking?.CompleteAnswer(correlationId);
                }

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
            else if (itemType is SpeechPlaybackItemType.FinalAnswer && cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(correlationId))
            {
                _spokenAnswerTracking?.MarkPlaybackCancelled(correlationId, "final_answer_playback_cancelled_before_start");
            }

            ClearActiveVolumeSetter();
            waveOut?.Dispose();
        }
    }

    private void OnDuckingChanged(object? sender, SpeakerDuckingChangedEventArgs eventArgs)
    {
        ApplyOutputVolume(eventArgs.VolumeMultiplier, eventArgs.Reason);
    }

    private void SetActiveVolumeSetter(Action<float> setter)
    {
        lock (_volumeSyncRoot)
        {
            _activeVolumeSetter = setter;
        }
    }

    private void ClearActiveVolumeSetter()
    {
        lock (_volumeSyncRoot)
        {
            _activeVolumeSetter = null;
        }
    }

    internal void SetActiveVolumeSetterForTests(Action<float> setter)
    {
        SetActiveVolumeSetter(setter);
    }

    internal void ClearActiveVolumeSetterForTests()
    {
        ClearActiveVolumeSetter();
    }

    private void ApplyOutputVolume(float multiplier, string reason)
    {
        Action<float>? setter;
        lock (_volumeSyncRoot)
        {
            setter = _activeVolumeSetter;
        }

        if (setter is null)
        {
            return;
        }

        var clamped = Math.Clamp(multiplier, 0.0f, 1.0f);
        try
        {
            setter(clamped);
            _logger.LogInformation(
                "Speaker ducking volume applied to active output. Target: {Target}. Reason: {Reason}.",
                clamped,
                reason);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
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
        ActivePlaybackControlState activeState,
        int totalBytes,
        int sampleRate,
        int channels,
        CancellationToken cancellationToken)
    {
        var expectedPlaybackMs = CalculateExpectedPlaybackMs(totalBytes, sampleRate, channels);
        var targetElapsedMs = expectedPlaybackMs + OutputDrainTailMs;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (IsPlaybackHeld(activeState))
            {
                await Task.Delay(DrainPollMs, cancellationToken);
                continue;
            }

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

    private static bool IsHoldableActivePlayback(ActivePlaybackControlState? state, string turnId)
    {
        return state is not null
            && state.ItemType is SpeechPlaybackItemType.FinalAnswer
            && string.Equals(state.TurnId, turnId, StringComparison.OrdinalIgnoreCase)
            && state.WavePlayer is not null
            && state.BufferedWaveProvider is not null
            && !state.Cancellation.IsCancellationRequested;
    }

    private static bool IsMatchingHeldPlayback(ActivePlaybackControlState? state, string holdId)
    {
        return state is { IsHeld: true }
            && !string.IsNullOrWhiteSpace(state.HoldId)
            && string.Equals(state.HoldId, holdId, StringComparison.OrdinalIgnoreCase)
            && state.WavePlayer is not null
            && !state.Cancellation.IsCancellationRequested;
    }

    private bool IsPlaybackHeld(ActivePlaybackControlState state)
    {
        lock (_playbackControlLock)
        {
            return ReferenceEquals(_activePlaybackState, state) && state.IsHeld;
        }
    }

    private void ResumeHeldPlaybackLocked(ActivePlaybackControlState state, string reason, string eventName)
    {
        var holdId = state.HoldId;
        var heldDurationMs = GetHeldDurationMs(state);
        state.WavePlayer!.Play();
        ClearHoldLocked(state);
        UpdateActivePlaybackSnapshotLocked(state);
        _logger.LogInformation(
            "audible_playback_state_changed TurnId: {TurnId}. CorrelationId: {CorrelationId}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. AudiblePlaybackActive: {AudiblePlaybackActive}. ActivePlaybackSnapshotIsActive: {ActivePlaybackSnapshotIsActive}. ActivePlaybackSnapshotIsHeld: {ActivePlaybackSnapshotIsHeld}. HoldId: {HoldId}. Reason: {Reason}.",
            state.TurnId,
            state.CorrelationId,
            true,
            IsAudiblePlaybackActiveLocked(state),
            IsAudiblePlaybackActiveLocked(state),
            true,
            false,
            state.HoldId,
            reason);
        _logger.LogInformation(
            "{EventName} TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. Reason: {Reason}. HeldDurationMs: {HeldDurationMs}. BufferedBytes: {BufferedBytes}. GenerationId: {GenerationId}.",
            eventName,
            state.TurnId,
            state.CorrelationId,
            holdId,
            reason,
            heldDurationMs,
            state.BufferedWaveProvider?.BufferedBytes,
            state.TurnGeneration);
    }

    private void RefreshHoldTimeoutLocked(ActivePlaybackControlState state)
    {
        state.HoldTimeoutCancellation?.Cancel();
        state.HoldTimeoutCancellation?.Dispose();

        var timeoutMs = Math.Max(0, _interruptionOptions.ProvisionalAudioHoldTimeoutMs);
        if (timeoutMs <= 0 || string.IsNullOrWhiteSpace(state.HoldId))
        {
            state.HoldTimeoutCancellation = null;
            return;
        }

        var timeoutCancellation = new CancellationTokenSource();
        state.HoldTimeoutCancellation = timeoutCancellation;
        var holdId = state.HoldId;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeoutMs, timeoutCancellation.Token);
                lock (_playbackControlLock)
                {
                    if (IsMatchingHeldPlayback(_activePlaybackState, holdId))
                    {
                        ResumeHeldPlaybackLocked(_activePlaybackState!, "provisional audio hold timed out", "provisional_audio_hold_timeout_resumed");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);
    }

    private void ClearHoldLocked(ActivePlaybackControlState state)
    {
        state.HoldTimeoutCancellation?.Cancel();
        state.HoldTimeoutCancellation?.Dispose();
        state.HoldTimeoutCancellation = null;
        state.IsHeld = false;
        state.HoldId = null;
        state.HoldStartedAtUtc = null;
        state.HoldReason = null;
    }

    private static double GetHeldDurationMs(ActivePlaybackControlState state)
    {
        return state.HoldStartedAtUtc is null
            ? 0
            : Math.Max(0, (DateTimeOffset.UtcNow - state.HoldStartedAtUtc.Value).TotalMilliseconds);
    }

    private void UpdateActivePlaybackSnapshotLocked(ActivePlaybackControlState state)
    {
        _activePlaybackSnapshot = new ActiveSpeechPlaybackSnapshot
        {
            CorrelationId = state.CorrelationId,
            AssistantTurnId = state.TurnId,
            SpeechType = state.ItemType.ToString(),
            ItemType = state.ItemType.ToString(),
            IsActive = true,
            IsHeld = state.IsHeld,
            IsAudiblePlaybackActive = IsAudiblePlaybackActiveLocked(state),
            HoldId = state.HoldId,
            StartedAtUtc = state.StartedAtUtc
        };
    }

    private static bool IsAudiblePlaybackActiveLocked(ActivePlaybackControlState state)
    {
        return state.WavePlayer is not null
            && !state.IsHeld
            && !state.Cancellation.IsCancellationRequested;
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

    private long GetFinalAnswerGeneration(string? turnId)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return 0;
        }

        return _finalAnswerGenerations.GetOrAdd(turnId.Trim(), 0);
    }

    private long IncrementFinalAnswerGeneration(string turnId)
    {
        return _finalAnswerGenerations.AddOrUpdate(
            turnId.Trim(),
            1,
            (_, current) => current + 1);
    }

    private bool IsObsoleteFinalAnswer(
        string? turnId,
        SpeechPlaybackItemType itemType,
        long itemGeneration,
        out long currentGeneration)
    {
        currentGeneration = itemGeneration;
        if (itemType is not SpeechPlaybackItemType.FinalAnswer || string.IsNullOrWhiteSpace(turnId))
        {
            return false;
        }

        currentGeneration = GetFinalAnswerGeneration(turnId);
        return itemGeneration != currentGeneration;
    }

    private void LogObsoleteSpeechDiscarded(
        string? turnId,
        SpeechPlaybackItemType itemType,
        long oldGenerationId,
        long currentGenerationId,
        string reason)
    {
        _logger.LogInformation(
            "obsolete_speech_chunk_discarded TurnId: {TurnId}. CorrelationId: {CorrelationId}. SpeechType: {SpeechType}. ItemType: {ItemType}. OldGenerationId: {OldGenerationId}. CurrentGenerationId: {CurrentGenerationId}. Reason: {Reason}.",
            turnId,
            turnId,
            itemType,
            itemType,
            oldGenerationId,
            currentGenerationId,
            reason);
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

    private sealed class ActivePlaybackControlState
    {
        public required string TurnId { get; init; }

        public required string CorrelationId { get; init; }

        public required SpeechPlaybackItemType ItemType { get; init; }

        public required long TurnGeneration { get; init; }

        public required CancellationTokenSource Cancellation { get; init; }

        public required DateTimeOffset StartedAtUtc { get; init; }

        public IWavePlayer? WavePlayer { get; set; }

        public BufferedWaveProvider? BufferedWaveProvider { get; set; }

        public bool IsHeld { get; set; }

        public string? HoldId { get; set; }

        public DateTimeOffset? HoldStartedAtUtc { get; set; }

        public string? HoldReason { get; set; }

        public CancellationTokenSource? HoldTimeoutCancellation { get; set; }
    }
}
