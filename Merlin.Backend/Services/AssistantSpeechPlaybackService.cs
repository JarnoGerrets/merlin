using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Services.StreamingResponses;
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
    private readonly AssistantUiStateBroadcaster? _assistantUiStateBroadcaster;
    private readonly IGpuWorkScheduler? _gpuWorkScheduler;
    private readonly ITtsTextSanitizer _ttsTextSanitizer;
    private readonly InterruptionHandlingOptions _interruptionOptions;
    private readonly StreamingResponseOptions _streamingOptions;
    private readonly ILogger<AssistantSpeechPlaybackService> _logger;
    private readonly Func<IWavePlayer> _wavePlayerFactory;
    private readonly SemaphoreSlim _speechGate = new(1, 1);
    private readonly ConcurrentDictionary<string, long> _finalAnswerGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _playbackControlLock = new();
    private readonly object _volumeSyncRoot = new();
    private CancellationTokenSource? _activeSpeechCancellation;
    private ActiveSpeechPlaybackSnapshot? _activePlaybackSnapshot;
    private ActivePlaybackControlState? _activePlaybackState;
    private StreamingFinalAnswerPlaybackSession? _activeStreamingSession;
    private Action<float>? _activeVolumeSetter;
    private long _queueGeneration;

    public AssistantSpeechPlaybackService(
        IVoiceSynthesisService voiceSynthesisService,
        IPlaybackReferenceTap playbackReferenceTap,
        ISpeakerDuckingService speakerDuckingService,
        ILogger<AssistantSpeechPlaybackService> logger,
        ILiveSpokenAnswerTrackingService? spokenAnswerTracking = null,
        IOptions<InterruptionHandlingOptions>? interruptionOptions = null,
        Func<IWavePlayer>? wavePlayerFactory = null,
        AssistantUiStateBroadcaster? assistantUiStateBroadcaster = null,
        IGpuWorkScheduler? gpuWorkScheduler = null,
        ITtsTextSanitizer? ttsTextSanitizer = null,
        IOptions<StreamingResponseOptions>? streamingOptions = null)
    {
        _voiceSynthesisService = voiceSynthesisService;
        _playbackReferenceTap = playbackReferenceTap;
        _speakerDuckingService = speakerDuckingService;
        _spokenAnswerTracking = spokenAnswerTracking;
        _assistantUiStateBroadcaster = assistantUiStateBroadcaster;
        _gpuWorkScheduler = gpuWorkScheduler;
        _ttsTextSanitizer = ttsTextSanitizer ?? new TtsTextSanitizer();
        _interruptionOptions = interruptionOptions?.Value ?? new InterruptionHandlingOptions();
        _streamingOptions = streamingOptions?.Value ?? new StreamingResponseOptions();
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

        if (itemType is SpeechPlaybackItemType.FinalAnswer)
        {
            var sanitization = _ttsTextSanitizer.Sanitize(spokenText);
            _logger.LogInformation(
                "tts_text_sanitized RawChars: {RawChars}. SanitizedChars: {SanitizedChars}. MarkdownRemoved: {MarkdownRemoved}. ListMarkersConverted: {ListMarkersConverted}. CodeBlocksRemoved: {CodeBlocksRemoved}. UrlsRemoved: {UrlsRemoved}. PreviewBefore: {PreviewBefore}. PreviewAfter: {PreviewAfter}. CorrelationId: {CorrelationId}. TurnId: {TurnId}.",
                sanitization.RawChars,
                sanitization.SanitizedChars,
                sanitization.MarkdownRemoved,
                sanitization.ListMarkersConverted,
                sanitization.CodeBlocksRemoved,
                sanitization.UrlsRemoved,
                PreviewText(spokenText),
                PreviewText(sanitization.Text),
                correlationId,
                correlationId);
            spokenText = sanitization.Text;
            if (string.IsNullOrWhiteSpace(spokenText))
            {
                return Task.CompletedTask;
            }
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
        StreamingFinalAnswerPlaybackSession? streamingSession;
        lock (_playbackControlLock)
        {
            cancellation = _activeSpeechCancellation;
            streamingSession = _activeStreamingSession;
        }

        _ = streamingSession?.CancelAsync("stop_current_requested", cancellationToken);
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
        StreamingFinalAnswerPlaybackSession? streamingSession;
        lock (_playbackControlLock)
        {
            streamingSession = _activeStreamingSession;
        }

        _ = streamingSession?.CancelAsync("queue_cleared", cancellationToken);
        return StopCurrentAsync(cancellationToken);
    }

    public Task<IStreamingFinalAnswerPlaybackSession> BeginStreamingFinalAnswerAsync(
        string turnId,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? originalUserQuestion = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new ArgumentException("Turn id is required.", nameof(turnId));
        }

        var normalizedTurnId = turnId.Trim();
        var normalizedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? normalizedTurnId
            : correlationId.Trim();
        var generationId = IncrementFinalAnswerGeneration(normalizedTurnId);
        var session = new StreamingFinalAnswerPlaybackSession(
            this,
            normalizedTurnId,
            normalizedCorrelationId,
            generationId,
            sendEventAsync,
            originalUserQuestion,
            _streamingOptions,
            cancellationToken);

        StreamingFinalAnswerPlaybackSession? previousSession;
        lock (_playbackControlLock)
        {
            previousSession = _activeStreamingSession;
            _activeStreamingSession = session;
        }

        if (previousSession is not null)
        {
            _ = previousSession.CancelAsync("superseded_by_new_streaming_final_answer", CancellationToken.None);
        }

        session.Start();
        _logger.LogInformation(
            "StreamingFinalAnswerSessionStarted TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SessionId: {SessionId}.",
            normalizedTurnId,
            normalizedCorrelationId,
            generationId,
            session.SessionId);
        return Task.FromResult<IStreamingFinalAnswerPlaybackSession>(session);
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

            _ = EmitAssistantUiStateImmediateAsync(
                AssistantUiStateEvent.Create(
                    "listening",
                    "provisional_audio_hold_started",
                    state.CorrelationId,
                    state.TurnId,
                    speechItemType: MapSpeechItemType(state.ItemType),
                    audiblePlaybackActive: false,
                    interruptionState: "held_for_user_speech"),
                nameof(AssistantSpeechPlaybackService),
                cancellationToken);

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
        StreamingFinalAnswerPlaybackSession? streamingSession = null;
        lock (_playbackControlLock)
        {
            snapshot = _activePlaybackSnapshot;
            if (_activeStreamingSession is not null
                && string.Equals(_activeStreamingSession.TurnId, normalizedTurnId, StringComparison.OrdinalIgnoreCase))
            {
                streamingSession = _activeStreamingSession;
            }

            if (snapshot is { IsActive: true }
                && string.Equals(snapshot.AssistantTurnId, normalizedTurnId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.SpeechType, SpeechPlaybackItemType.FinalAnswer.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                cancellation = _activeSpeechCancellation;
            }
        }

        _ = streamingSession?.CancelAsync(reason, cancellationToken);
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
                var currentQueueGeneration = Volatile.Read(ref _queueGeneration);
                if (itemType is SpeechPlaybackItemType.StopConfirmation)
                {
                    _logger.LogInformation(
                        "stop_confirmation_generation_mismatch_ignored TurnId: {TurnId}. CorrelationId: {CorrelationId}. ExpectedGenerationId: {ExpectedGenerationId}. CurrentGenerationId: {CurrentGenerationId}. ItemType: {ItemType}. WasStopConfirmation: {WasStopConfirmation}.",
                        correlationId,
                        correlationId,
                        queueGeneration,
                        currentQueueGeneration,
                        itemType,
                        true);
                }
                else
                {
                    LogQueueGenerationSkipped(
                        correlationId,
                        itemType,
                        queueGeneration,
                        currentQueueGeneration,
                        "queue_generation_changed");
                    return;
                }
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
                if (itemType is SpeechPlaybackItemType.StopConfirmation)
                {
                    _logger.LogInformation(
                        "stop_confirmation_playback_started TurnId: {TurnId}. CorrelationId: {CorrelationId}. ItemType: {ItemType}. ExpectedGenerationId: {ExpectedGenerationId}. CurrentGenerationId: {CurrentGenerationId}.",
                        correlationId,
                        correlationId,
                        itemType,
                        queueGeneration,
                        Volatile.Read(ref _queueGeneration));
                }
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
            else if (itemType is SpeechPlaybackItemType.StopConfirmation)
            {
                _logger.LogInformation(
                    "stop_confirmation_playback_completed TurnId: {TurnId}. CorrelationId: {CorrelationId}. ItemType: {ItemType}.",
                    correlationId,
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
        var audioWriteStarted = false;
        var finalAnswerChunkSequence = 0;
        var ttsCompleted = 0;
        var finalAnswerInChunkGap = 0;
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
            if (itemType is SpeechPlaybackItemType.StopConfirmation)
            {
                _logger.LogInformation(
                    "stop_confirmation_tts_started TurnId: {TurnId}. CorrelationId: {CorrelationId}. Text: {Text}. TextChars: {TextChars}. CancellationRequested: {CancellationRequested}.",
                    correlationId,
                    correlationId,
                    text,
                    text.Length,
                    cancellationToken.IsCancellationRequested);
            }

            await _voiceSynthesisService.StreamSynthesizeChunksAsync(
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
                    if (itemType is SpeechPlaybackItemType.StopConfirmation)
                    {
                        _logger.LogInformation(
                            "stop_confirmation_output_opened TurnId: {TurnId}. CorrelationId: {CorrelationId}. SampleRate: {SampleRate}. Channels: {Channels}. WaveOutPlaybackState: {WaveOutPlaybackState}. Volume: {Volume}. CancellationRequested: {CancellationRequested}.",
                            correlationId,
                            correlationId,
                            sampleRate,
                            channels,
                            waveOut.PlaybackState,
                            waveOut.Volume,
                            token.IsCancellationRequested);
                    }

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
                async (chunk, token) =>
                {
                    if (itemType is SpeechPlaybackItemType.FinalAnswer)
                    {
                        await WaitWhilePlaybackHeldAsync(activeState, token);
                    }

                    if (IsObsoleteFinalAnswer(correlationId, itemType, turnGeneration, out var currentGeneration))
                    {
                        LogObsoleteSpeechDiscarded(correlationId, itemType, turnGeneration, currentGeneration, "obsolete_during_tts_stream");
                        throw new OperationCanceledException(token);
                    }

                    if (bufferedWaveProvider is null || waveOut is null)
                    {
                        return;
                    }

                    var buffer = chunk.Audio.ToArray();
                    if (itemType is SpeechPlaybackItemType.StopConfirmation && !audioWriteStarted)
                    {
                        audioWriteStarted = true;
                        _logger.LogInformation(
                            "stop_confirmation_audio_write_started TurnId: {TurnId}. CorrelationId: {CorrelationId}. AudioBytes: {AudioBytes}. BufferedBytes: {BufferedBytes}. WaveOutPlaybackState: {WaveOutPlaybackState}. Volume: {Volume}. WasCancellationRequested: {WasCancellationRequested}.",
                            correlationId,
                            correlationId,
                            buffer.Length,
                            bufferedWaveProvider.BufferedBytes,
                            waveOut.PlaybackState,
                            waveOut.Volume,
                            token.IsCancellationRequested);
                    }

                    await WaitForPlaybackBufferSpaceAsync(bufferedWaveProvider, buffer.Length, token);
                    bufferedWaveProvider.AddSamples(buffer, 0, buffer.Length);
                    totalBytes += buffer.Length;
                    var finalAnswerChunkSequenceForThisAudio = itemType is SpeechPlaybackItemType.FinalAnswer
                        ? Interlocked.Increment(ref finalAnswerChunkSequence)
                        : 0;
                    if (itemType is SpeechPlaybackItemType.FinalAnswer && !chunk.IsFinalChunk)
                    {
                        ScheduleFinalAnswerChunkGapAsync(
                            activeState,
                            bufferedWaveProvider,
                            playbackStopwatch,
                            totalBytes,
                            sampleRate,
                            channels,
                            correlationId,
                            finalAnswerChunkSequenceForThisAudio,
                            () => Volatile.Read(ref finalAnswerChunkSequence),
                            () => Volatile.Read(ref ttsCompleted) == 1,
                            () => Volatile.Write(ref finalAnswerInChunkGap, 1),
                            cancellationToken);
                    }

                    if (itemType is SpeechPlaybackItemType.StopConfirmation)
                    {
                        _logger.LogInformation(
                            "stop_confirmation_audio_write_completed TurnId: {TurnId}. CorrelationId: {CorrelationId}. AudioBytes: {AudioBytes}. TotalAudioBytes: {TotalAudioBytes}. BufferedBytes: {BufferedBytes}. WaveOutPlaybackState: {WaveOutPlaybackState}. Volume: {Volume}. WasCancellationRequested: {WasCancellationRequested}.",
                            correlationId,
                            correlationId,
                            buffer.Length,
                            totalBytes,
                            bufferedWaveProvider.BufferedBytes,
                            waveOut.PlaybackState,
                            waveOut.Volume,
                            token.IsCancellationRequested);
                    }

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
                        await EmitAssistantUiStateImmediateAsync(
                            AssistantUiStateEvent.Create(
                                itemType is SpeechPlaybackItemType.SleepAcknowledgement
                                    ? "sleeping"
                                    : "speaking",
                                itemType is SpeechPlaybackItemType.StopConfirmation
                                    ? "stop_confirmation_playback_started"
                                    : itemType is SpeechPlaybackItemType.SleepAcknowledgement
                                        ? "sleep_acknowledgement_playback_started"
                                    : "audio_playback_started",
                                correlationId,
                                correlationId,
                                speechItemType: MapSpeechItemType(itemType),
                                audiblePlaybackActive: true),
                            nameof(AssistantSpeechPlaybackService),
                            token);
                        _logger.LogInformation(
                            "Voice timing: backend playback started. CorrelationId: {CorrelationId}. ElapsedMs: {ElapsedMs}.",
                            correlationId,
                            stopwatch.Elapsed.TotalMilliseconds);
                        if (itemType is SpeechPlaybackItemType.StopConfirmation)
                        {
                            _logger.LogInformation(
                                "stop_confirmation_output_playback_started TurnId: {TurnId}. CorrelationId: {CorrelationId}. AudioBytes: {AudioBytes}. BufferedBytes: {BufferedBytes}. WaveOutPlaybackState: {WaveOutPlaybackState}. Volume: {Volume}. WasCancellationRequested: {WasCancellationRequested}.",
                                correlationId,
                                correlationId,
                                totalBytes,
                                bufferedWaveProvider.BufferedBytes,
                                waveOut.PlaybackState,
                                waveOut.Volume,
                                token.IsCancellationRequested);
                        }
                    }
                    else if (itemType is SpeechPlaybackItemType.FinalAnswer
                             && Volatile.Read(ref finalAnswerInChunkGap) == 1
                             && Interlocked.Exchange(ref finalAnswerInChunkGap, 0) == 1)
                    {
                        await EmitAssistantUiStateImmediateAsync(
                            AssistantUiStateEvent.Create(
                                "speaking",
                                "audio_playback_started",
                                correlationId,
                                correlationId,
                                speechItemType: "final_answer",
                                audiblePlaybackActive: true),
                            nameof(AssistantSpeechPlaybackService),
                            token);
                    }

                    if (lastEnergyEvent.ElapsedMilliseconds >= EnergyEventIntervalMs)
                    {
                        lastEnergyEvent.Restart();
                        await TrySendEnergyAsync(sendEventAsync, correlationId, buffer, token);
                    }
                },
                cancellationToken);
            Volatile.Write(ref ttsCompleted, 1);
            if (itemType is SpeechPlaybackItemType.StopConfirmation)
            {
                _logger.LogInformation(
                    "stop_confirmation_tts_completed TurnId: {TurnId}. CorrelationId: {CorrelationId}. Text: {Text}. TextChars: {TextChars}. AudioBytes: {AudioBytes}. AudioDurationSeconds: {AudioDurationSeconds}. CancellationRequested: {CancellationRequested}.",
                    correlationId,
                    correlationId,
                    text,
                    text.Length,
                    totalBytes,
                    CalculateExpectedPlaybackMs(totalBytes, sampleRate, channels) / 1000.0,
                    cancellationToken.IsCancellationRequested);
            }

            if (waveOut is not null && bufferedWaveProvider is not null)
            {
                var drainStartedMs = stopwatch.Elapsed.TotalMilliseconds;
                if (itemType is SpeechPlaybackItemType.StopConfirmation)
                {
                    _logger.LogInformation(
                        "stop_confirmation_output_drain_started TurnId: {TurnId}. CorrelationId: {CorrelationId}. AudioBytes: {AudioBytes}. BufferedBytes: {BufferedBytes}. PlaybackElapsedMs: {PlaybackElapsedMs}. WaveOutPlaybackState: {WaveOutPlaybackState}. Volume: {Volume}. WasCancellationRequested: {WasCancellationRequested}.",
                        correlationId,
                        correlationId,
                        totalBytes,
                        bufferedWaveProvider.BufferedBytes,
                        playbackStopwatch.Elapsed.TotalMilliseconds,
                        waveOut.PlaybackState,
                        waveOut.Volume,
                        cancellationToken.IsCancellationRequested);
                }

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
                if (itemType is SpeechPlaybackItemType.StopConfirmation)
                {
                    _logger.LogInformation(
                        "stop_confirmation_output_drain_completed TurnId: {TurnId}. CorrelationId: {CorrelationId}. AudioBytes: {AudioBytes}. BufferedBytes: {BufferedBytes}. PlaybackElapsedMs: {PlaybackElapsedMs}. DrainMs: {DrainMs}. WaveOutPlaybackState: {WaveOutPlaybackState}. Volume: {Volume}. WasCancellationRequested: {WasCancellationRequested}.",
                        correlationId,
                        correlationId,
                        totalBytes,
                        bufferedWaveProvider.BufferedBytes,
                        playbackStopwatch.Elapsed.TotalMilliseconds,
                        stopwatch.Elapsed.TotalMilliseconds - drainStartedMs,
                        waveOut.PlaybackState,
                        waveOut.Volume,
                        cancellationToken.IsCancellationRequested);
                }

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
                await EmitPlaybackCompletionUiStateAsync(
                    AssistantUiStateEvent.Create(
                        CompletionBaseState(itemType),
                        CompletionReason(itemType),
                        correlationId,
                        correlationId,
                        speechItemType: MapSpeechItemType(itemType),
                        audiblePlaybackActive: false),
                    itemType,
                    nameof(AssistantSpeechPlaybackService),
                    cancellationToken);
            }

            _logger.LogInformation(
                "Voice timing: backend playback complete. CorrelationId: {CorrelationId}. Bytes: {Bytes}. ElapsedMs: {ElapsedMs}.",
                correlationId,
                totalBytes,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException exception)
        {
            if (itemType is SpeechPlaybackItemType.StopConfirmation)
            {
                if (totalBytes <= 0)
                {
                    _logger.LogInformation(
                        exception,
                        "stop_confirmation_tts_cancelled TurnId: {TurnId}. CorrelationId: {CorrelationId}. Text: {Text}. TextChars: {TextChars}. AudioBytes: {AudioBytes}. CancellationRequested: {CancellationRequested}. ExceptionType: {ExceptionType}. ExceptionMessage: {ExceptionMessage}.",
                        correlationId,
                        correlationId,
                        text,
                        text.Length,
                        totalBytes,
                        cancellationToken.IsCancellationRequested,
                        exception.GetType().Name,
                        exception.Message);
                }

                _logger.LogInformation(
                    exception,
                    "stop_confirmation_playback_cancelled_after_start TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}. CancellationSource: {CancellationSource}. CurrentItemType: {CurrentItemType}. PreviousItemType: {PreviousItemType}. ExpectedGenerationId: {ExpectedGenerationId}. CurrentGenerationId: {CurrentGenerationId}. WasActivePlaybackReplaced: {WasActivePlaybackReplaced}. WasClearQueueCalled: {WasClearQueueCalled}. WasCancelCurrentSpeechCalled: {WasCancelCurrentSpeechCalled}. WasBackendSpeechPlaybackCancelledRaised: {WasBackendSpeechPlaybackCancelledRaised}. AudioBytes: {AudioBytes}. PlaybackStarted: {PlaybackStarted}. WasCancellationRequested: {WasCancellationRequested}. ExceptionType: {ExceptionType}. ExceptionMessage: {ExceptionMessage}.",
                    correlationId,
                    correlationId,
                    "stop_confirmation_playback_cancelled",
                    cancellationToken.IsCancellationRequested ? "playback_cancellation_token" : "unknown",
                    itemType,
                    null,
                    turnGeneration,
                    Volatile.Read(ref _queueGeneration),
                    !ReferenceEquals(_activePlaybackState, activeState),
                    cancellationToken.IsCancellationRequested,
                    cancellationToken.IsCancellationRequested,
                    true,
                    totalBytes,
                    playbackStarted,
                    cancellationToken.IsCancellationRequested,
                    exception.GetType().Name,
                    exception.Message);
            }

            throw;
        }
        catch (Exception exception)
        {
            if (itemType is SpeechPlaybackItemType.StopConfirmation)
            {
                if (totalBytes <= 0)
                {
                    _logger.LogError(
                        exception,
                        "stop_confirmation_tts_failed TurnId: {TurnId}. CorrelationId: {CorrelationId}. Text: {Text}. TextChars: {TextChars}. AudioBytes: {AudioBytes}. CancellationRequested: {CancellationRequested}. ExceptionType: {ExceptionType}. ExceptionMessage: {ExceptionMessage}.",
                        correlationId,
                        correlationId,
                        text,
                        text.Length,
                        totalBytes,
                        cancellationToken.IsCancellationRequested,
                        exception.GetType().Name,
                        exception.Message);
                }

                _logger.LogError(
                    exception,
                    "stop_confirmation_playback_failed TurnId: {TurnId}. CorrelationId: {CorrelationId}. AudioBytes: {AudioBytes}. PlaybackStarted: {PlaybackStarted}. WasCancellationRequested: {WasCancellationRequested}. ExceptionType: {ExceptionType}. ExceptionMessage: {ExceptionMessage}.",
                    correlationId,
                    correlationId,
                    totalBytes,
                    playbackStarted,
                    cancellationToken.IsCancellationRequested,
                    exception.GetType().Name,
                    exception.Message);
            }

            throw;
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

    private void ScheduleFinalAnswerChunkGapAsync(
        ActivePlaybackControlState activeState,
        BufferedWaveProvider bufferedWaveProvider,
        Stopwatch playbackStopwatch,
        int bytesAfterChunk,
        int sampleRate,
        int channels,
        string? correlationId,
        int chunkSequence,
        Func<int> getLatestChunkSequence,
        Func<bool> isTtsCompleted,
        Action markChunkGap,
        CancellationToken cancellationToken)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await WaitForPlaybackDrainAsync(
                        bufferedWaveProvider,
                        playbackStopwatch,
                        activeState,
                        bytesAfterChunk,
                        sampleRate,
                        channels,
                        cancellationToken);

                    if (cancellationToken.IsCancellationRequested
                        || isTtsCompleted()
                        || getLatestChunkSequence() != chunkSequence)
                    {
                        return;
                    }

                    markChunkGap();
                    await EmitAssistantUiStateCoalescedAsync(
                        AssistantUiStateEvent.Create(
                            "idle",
                            "tts_chunk_gap",
                            correlationId,
                            correlationId,
                            speechItemType: "final_answer",
                            audiblePlaybackActive: false),
                        nameof(AssistantSpeechPlaybackService),
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            },
            CancellationToken.None);
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

    private async Task WaitWhilePlaybackHeldAsync(
        ActivePlaybackControlState state,
        CancellationToken cancellationToken)
    {
        var logged = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsPlaybackHeld(state))
            {
                return;
            }

            if (!logged)
            {
                logged = true;
                _logger.LogInformation(
                    "future_tts_chunk_suspended_during_interruption TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. ItemType: {ItemType}. InterruptionSttPending: {InterruptionSttPending}.",
                    state.TurnId,
                    state.CorrelationId,
                    state.HoldId,
                    state.ItemType,
                    _gpuWorkScheduler?.HasPendingInterruptionStt == true);
            }

            await Task.Delay(DrainPollMs, cancellationToken);
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

        _ = EmitAssistantUiStateImmediateAsync(
            AssistantUiStateEvent.Create(
                "speaking",
                eventName,
                state.CorrelationId,
                state.TurnId,
                speechItemType: MapSpeechItemType(state.ItemType),
                audiblePlaybackActive: true,
                interruptionState: "none"),
            nameof(AssistantSpeechPlaybackService),
            CancellationToken.None);
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
                var safetyTimeoutMs = Math.Max(timeoutMs, 30000);
                var safetyDeadline = DateTimeOffset.UtcNow.AddMilliseconds(safetyTimeoutMs);
                var loggedSttPendingDeferral = false;
                while (_gpuWorkScheduler?.HasPendingInterruptionStt == true
                       && DateTimeOffset.UtcNow < safetyDeadline)
                {
                    if (!loggedSttPendingDeferral)
                    {
                        loggedSttPendingDeferral = true;
                        _logger.LogInformation(
                            "provisional_audio_hold_timeout_resume_blocked_stt_pending HoldId: {HoldId}. TimeoutMs: {TimeoutMs}. SafetyTimeoutMs: {SafetyTimeoutMs}.",
                            holdId,
                            timeoutMs,
                            safetyTimeoutMs);
                    }

                    await Task.Delay(Math.Min(250, timeoutMs), timeoutCancellation.Token);
                }

                lock (_playbackControlLock)
                {
                    if (IsMatchingHeldPlayback(_activePlaybackState, holdId))
                    {
                        var sttStillPending = _gpuWorkScheduler?.HasPendingInterruptionStt == true;
                        ResumeHeldPlaybackLocked(
                            _activePlaybackState!,
                            sttStillPending
                                ? "provisional audio hold safety timeout expired while interruption STT was still pending"
                                : "provisional audio hold timed out",
                            sttStillPending
                                ? "provisional_audio_hold_safety_timeout_resumed"
                                : "provisional_audio_hold_timeout_resumed");
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

    private void LogQueueGenerationSkipped(
        string? turnId,
        SpeechPlaybackItemType itemType,
        long expectedGenerationId,
        long currentGenerationId,
        string reason)
    {
        var wasStopConfirmation = itemType is SpeechPlaybackItemType.StopConfirmation;
        if (wasStopConfirmation)
        {
            _logger.LogInformation(
                "stop_confirmation_playback_skipped TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}. ExpectedGenerationId: {ExpectedGenerationId}. CurrentGenerationId: {CurrentGenerationId}. ItemType: {ItemType}. WasStopConfirmation: {WasStopConfirmation}.",
                turnId,
                turnId,
                reason,
                expectedGenerationId,
                currentGenerationId,
                itemType,
                wasStopConfirmation);
            return;
        }

        _logger.LogInformation(
            "Speech playback item skipped because queue generation changed. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}. ExpectedGenerationId: {ExpectedGenerationId}. CurrentGenerationId: {CurrentGenerationId}. ItemType: {ItemType}. WasStopConfirmation: {WasStopConfirmation}.",
            turnId,
            turnId,
            reason,
            expectedGenerationId,
            currentGenerationId,
            itemType,
            wasStopConfirmation);
    }

    private static string PreviewText(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 160
            ? normalized
            : $"{normalized[..157]}...";
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

    private Task EmitAssistantUiStateImmediateAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        return _assistantUiStateBroadcaster?.EmitImmediateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    private Task EmitAssistantUiStateCoalescedAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        return _assistantUiStateBroadcaster?.RequestCoalescedStateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    private Task EmitPlaybackCompletionUiStateAsync(
        AssistantUiStateEvent uiState,
        SpeechPlaybackItemType itemType,
        string source,
        CancellationToken cancellationToken)
    {
        if (itemType is SpeechPlaybackItemType.FinalAnswer
            or SpeechPlaybackItemType.StopConfirmation
            or SpeechPlaybackItemType.SleepAcknowledgement)
        {
            return _assistantUiStateBroadcaster?.EmitTerminalAsync(uiState, source, cancellationToken)
                ?? Task.CompletedTask;
        }

        return _assistantUiStateBroadcaster?.RequestCoalescedStateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    private static string CompletionBaseState(SpeechPlaybackItemType itemType)
    {
        if (itemType is SpeechPlaybackItemType.SleepAcknowledgement)
        {
            return "sleeping";
        }

        return itemType is SpeechPlaybackItemType.Acknowledgement or SpeechPlaybackItemType.Progress
            ? "thinking"
            : "idle";
    }

    private static string CompletionReason(SpeechPlaybackItemType itemType)
    {
        return itemType switch
        {
            SpeechPlaybackItemType.FinalAnswer => "final_answer_completed",
            SpeechPlaybackItemType.StopConfirmation => "stop_confirmation_playback_completed",
            SpeechPlaybackItemType.SleepAcknowledgement => "sleep_acknowledgement_completed",
            SpeechPlaybackItemType.Acknowledgement => "acknowledgement_playback_completed",
            SpeechPlaybackItemType.Progress => "progress_playback_completed",
            _ => "audio_playback_completed"
        };
    }

    internal static string MapSpeechItemType(SpeechPlaybackItemType itemType)
    {
        return itemType switch
        {
            SpeechPlaybackItemType.Acknowledgement => "acknowledgement",
            SpeechPlaybackItemType.Progress => "progress",
            SpeechPlaybackItemType.SleepAcknowledgement => "sleep_acknowledgement",
            SpeechPlaybackItemType.FinalAnswer => "final_answer",
            SpeechPlaybackItemType.StopConfirmation => "stop_confirmation",
            SpeechPlaybackItemType.InterruptionClarification => "clarification",
            SpeechPlaybackItemType.InterruptionContinuation => "continuation",
            _ => "none"
        };
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

    private Task<StreamingFinalAnswerAudioSegment> SynthesizeStreamingSegmentAsync(
        StreamingFinalAnswerTextSegment segment,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            var stopwatch = Stopwatch.StartNew();
            VoiceSynthesisStreamMetadata? metadata = null;
            var chunks = new List<byte[]>();
            var audioDurationMs = 0L;
            await _voiceSynthesisService.StreamSynthesizeChunksAsync(
                segment.Text,
                (streamMetadata, _) =>
                {
                    metadata = streamMetadata;
                    return Task.CompletedTask;
                },
                (chunk, _) =>
                {
                    var audio = chunk.Audio.ToArray();
                    chunks.Add(audio);
                    if (metadata is not null)
                    {
                        audioDurationMs += CalculateExpectedPlaybackMs(
                            audio.Length,
                            metadata.SampleRate,
                            metadata.Channels);
                    }

                    return Task.CompletedTask;
                },
                cancellationToken);

            stopwatch.Stop();
            if (metadata is null)
            {
                throw new InvalidOperationException("Streaming TTS produced no metadata.");
            }

            var totalBytes = chunks.Sum(chunk => chunk.Length);
            var audio = new byte[totalBytes];
            var offset = 0;
            foreach (var chunk in chunks)
            {
                Buffer.BlockCopy(chunk, 0, audio, offset, chunk.Length);
                offset += chunk.Length;
            }

            return new StreamingFinalAnswerAudioSegment(
                segment,
                metadata,
                audio,
                audioDurationMs,
                stopwatch.Elapsed);
        }, CancellationToken.None);
    }

    private bool IsCurrentStreamingSession(StreamingFinalAnswerPlaybackSession session)
    {
        lock (_playbackControlLock)
        {
            return ReferenceEquals(_activeStreamingSession, session)
                && _activeStreamingSession.GenerationId == session.GenerationId;
        }
    }

    private void ClearCurrentStreamingSession(StreamingFinalAnswerPlaybackSession session)
    {
        lock (_playbackControlLock)
        {
            if (ReferenceEquals(_activeStreamingSession, session))
            {
                _activeStreamingSession = null;
            }
        }
    }

    private sealed record StreamingFinalAnswerAudioSegment(
        StreamingFinalAnswerTextSegment TextSegment,
        VoiceSynthesisStreamMetadata Metadata,
        byte[] Audio,
        long AudioDurationMs,
        TimeSpan TtsDuration);

    private sealed class StreamingFinalAnswerPlaybackSession : IStreamingFinalAnswerPlaybackSession
    {
        private readonly AssistantSpeechPlaybackService _owner;
        private readonly Func<AssistantVisualEvent, CancellationToken, Task> _sendEventAsync;
        private readonly string? _originalUserQuestion;
        private readonly Channel<StreamingFinalAnswerTextSegment> _textSegments;
        private readonly Channel<StreamingFinalAnswerAudioSegment> _readyAudio;
        private readonly CancellationTokenSource _cancellation;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private Task? _ttsTask;
        private Task? _playbackTask;
        private int _acceptedSegments;
        private int _audioSegments;
        private int _playedSegments;
        private double _totalTtsMs;
        private double _totalInterSegmentGapMs;
        private long? _firstSegmentAcceptedMs;
        private long? _firstAudioReadyMs;
        private long? _firstPlaybackStartedMs;
        private string? _cancellationReason;

        public StreamingFinalAnswerPlaybackSession(
            AssistantSpeechPlaybackService owner,
            string turnId,
            string correlationId,
            long generationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? originalUserQuestion,
            StreamingResponseOptions options,
            CancellationToken cancellationToken)
        {
            _owner = owner;
            TurnId = turnId;
            CorrelationId = correlationId;
            GenerationId = generationId;
            _sendEventAsync = sendEventAsync;
            _originalUserQuestion = originalUserQuestion;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            SessionId = Guid.NewGuid().ToString("N");

            _textSegments = Channel.CreateBounded<StreamingFinalAnswerTextSegment>(new BoundedChannelOptions(Math.Max(1, options.MaxPendingTtsSegments))
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            var readyAudioCapacity = Math.Max(1, Math.Min(
                Math.Max(1, options.MaxReadyAudioSegments),
                Math.Max(1, options.PrebufferSegments + 1)));
            _readyAudio = Channel.CreateBounded<StreamingFinalAnswerAudioSegment>(new BoundedChannelOptions(readyAudioCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });
        }

        public string SessionId { get; }

        public string TurnId { get; }

        public string CorrelationId { get; }

        public long GenerationId { get; }

        public void Start()
        {
            _ttsTask = Task.Run(RunTtsProducerAsync, CancellationToken.None);
            _playbackTask = Task.Run(RunPlaybackConsumerAsync, CancellationToken.None);
        }

        public async Task EnqueueTextSegmentAsync(
            StreamingFinalAnswerTextSegment segment,
            CancellationToken cancellationToken = default)
        {
            if (!_owner.IsCurrentStreamingSession(this) || segment.GenerationId != GenerationId)
            {
                _owner._logger.LogInformation(
                    "StreamingSegmentDroppedBecauseStale TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}.",
                    segment.TurnId,
                    segment.CorrelationId,
                    segment.GenerationId,
                    segment.SegmentIndex,
                    SessionId);
                return;
            }

            _firstSegmentAcceptedMs ??= _stopwatch.ElapsedMilliseconds;
            Interlocked.Increment(ref _acceptedSegments);
            _owner._logger.LogInformation(
                "StreamingFinalAnswerFirstSegmentAccepted TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}. ElapsedMs: {ElapsedMs}. TextChars: {TextChars}. BoundaryKind: {BoundaryKind}.",
                TurnId,
                CorrelationId,
                GenerationId,
                segment.SegmentIndex,
                SessionId,
                _stopwatch.ElapsedMilliseconds,
                segment.Text.Length,
                segment.BoundaryKind);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, cancellationToken);
            await _textSegments.Writer.WriteAsync(segment, linkedCancellation.Token);
        }

        public async Task CompleteInputAsync(CancellationToken cancellationToken = default)
        {
            _textSegments.Writer.TryComplete();
            await Task.CompletedTask;
        }

        public async Task CancelAsync(string reason, CancellationToken cancellationToken = default)
        {
            _cancellationReason = reason;
            _owner._logger.LogInformation(
                "StreamingFinalAnswerSessionCancelled TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SessionId: {SessionId}. Reason: {Reason}.",
                TurnId,
                CorrelationId,
                GenerationId,
                SessionId,
                reason);
            await _cancellation.CancelAsync();
            _textSegments.Writer.TryComplete();
            _readyAudio.Writer.TryComplete();
        }

        public async ValueTask DisposeAsync()
        {
            await CancelAsync("disposed", CancellationToken.None);
            _cancellation.Dispose();
        }

        private async Task RunTtsProducerAsync()
        {
            try
            {
                await foreach (var segment in _textSegments.Reader.ReadAllAsync(_cancellation.Token))
                {
                    if (!_owner.IsCurrentStreamingSession(this) || segment.GenerationId != GenerationId)
                    {
                        _owner._logger.LogInformation(
                            "StreamingSegmentDroppedBecauseStale TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}.",
                            segment.TurnId,
                            segment.CorrelationId,
                            segment.GenerationId,
                            segment.SegmentIndex,
                            SessionId);
                        continue;
                    }

                    var queuedMs = _stopwatch.ElapsedMilliseconds;
                    _owner._logger.LogInformation(
                        "StreamingFinalAnswerTtsQueued TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}. ElapsedMs: {ElapsedMs}. TextChars: {TextChars}.",
                        TurnId,
                        CorrelationId,
                        GenerationId,
                        segment.SegmentIndex,
                        SessionId,
                        queuedMs,
                        segment.Text.Length);
                    var audio = await _owner.SynthesizeStreamingSegmentAsync(segment, _cancellation.Token);
                    if (!_owner.IsCurrentStreamingSession(this) || segment.GenerationId != GenerationId)
                    {
                        _owner._logger.LogInformation(
                            "StreamingTtsResultDroppedBecauseStale TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}.",
                            segment.TurnId,
                            segment.CorrelationId,
                            segment.GenerationId,
                            segment.SegmentIndex,
                            SessionId);
                        continue;
                    }

                    _firstAudioReadyMs ??= _stopwatch.ElapsedMilliseconds;
                    Interlocked.Increment(ref _audioSegments);
                    _totalTtsMs += audio.TtsDuration.TotalMilliseconds;
                    _owner._logger.LogInformation(
                        "StreamingFinalAnswerFirstAudioReady TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}. ElapsedMs: {ElapsedMs}. TtsMs: {TtsMs}. AudioDurationMs: {AudioDurationMs}. AudioBytes: {AudioBytes}.",
                        TurnId,
                        CorrelationId,
                        GenerationId,
                        segment.SegmentIndex,
                        SessionId,
                        _stopwatch.ElapsedMilliseconds,
                        audio.TtsDuration.TotalMilliseconds,
                        audio.AudioDurationMs,
                        audio.Audio.Length);
                    await _readyAudio.Writer.WriteAsync(audio, _cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _readyAudio.Writer.TryComplete();
            }
        }

        private async Task RunPlaybackConsumerAsync()
        {
            try
            {
                await _owner._speechGate.WaitAsync(_cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            using var speechCancellation = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
            var activeState = new ActivePlaybackControlState
            {
                TurnId = TurnId,
                CorrelationId = CorrelationId,
                ItemType = SpeechPlaybackItemType.FinalAnswer,
                TurnGeneration = GenerationId,
                Cancellation = speechCancellation,
                StartedAtUtc = DateTimeOffset.UtcNow
            };
            lock (_owner._playbackControlLock)
            {
                _owner._activeSpeechCancellation = speechCancellation;
                _owner._activePlaybackState = activeState;
                _owner.UpdateActivePlaybackSnapshotLocked(activeState);
            }

            IWavePlayer? waveOut = null;
            BufferedWaveProvider? bufferedWaveProvider = null;
            BargeInSpeechContext? speechContext = null;
            var playbackStarted = false;
            var playbackStopwatch = new Stopwatch();
            var totalBytes = 0;
            var segmentCompletionTasks = new List<Task>();
            long cumulativeAudioMs = 0;
            var spokenTrackingStarted = false;

            try
            {
                await foreach (var audio in _readyAudio.Reader.ReadAllAsync(speechCancellation.Token))
                {
                    if (!_owner.IsCurrentStreamingSession(this) || audio.TextSegment.GenerationId != GenerationId)
                    {
                        _owner._logger.LogInformation(
                            "StreamingAudioDroppedBecauseStale TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}.",
                            audio.TextSegment.TurnId,
                            audio.TextSegment.CorrelationId,
                            audio.TextSegment.GenerationId,
                            audio.TextSegment.SegmentIndex,
                            SessionId);
                        continue;
                    }

                    if (bufferedWaveProvider is null || waveOut is null)
                    {
                        var waveFormat = new WaveFormat(audio.Metadata.SampleRate, 16, audio.Metadata.Channels);
                        bufferedWaveProvider = new BufferedWaveProvider(waveFormat)
                        {
                            BufferDuration = TimeSpan.FromSeconds(PlaybackBufferSeconds),
                            DiscardOnBufferOverflow = false
                        };
                        waveOut = _owner._wavePlayerFactory();
                        waveOut.Init(new PlaybackReferenceWaveProvider(
                            bufferedWaveProvider,
                            _owner._playbackReferenceTap,
                            CorrelationId));
                        lock (_owner._playbackControlLock)
                        {
                            if (ReferenceEquals(_owner._activePlaybackState, activeState))
                            {
                                activeState.WavePlayer = waveOut;
                                activeState.BufferedWaveProvider = bufferedWaveProvider;
                                _owner.UpdateActivePlaybackSnapshotLocked(activeState);
                            }
                        }
                    }

                    await _owner.WaitWhilePlaybackHeldAsync(activeState, speechCancellation.Token);
                    var waitedForTtsWithEmptyPlaybackBuffer = playbackStarted && bufferedWaveProvider.BufferedBytes <= 0;
                    await WaitForStreamingBufferSpaceAsync(bufferedWaveProvider, audio.Audio.Length, speechCancellation.Token);
                    bufferedWaveProvider.AddSamples(audio.Audio, 0, audio.Audio.Length);
                    totalBytes += audio.Audio.Length;
                    var segmentPlaybackStart = TimeSpan.FromMilliseconds(cumulativeAudioMs);
                    cumulativeAudioMs += audio.AudioDurationMs;
                    if (!spokenTrackingStarted)
                    {
                        spokenTrackingStarted = true;
                        _owner._spokenAnswerTracking?.StartAnswer(
                            TurnId,
                            CorrelationId,
                            _originalUserQuestion ?? string.Empty);
                    }

                    _owner._spokenAnswerTracking?.MarkChunkStarted(TurnId, audio.TextSegment.Text, segmentPlaybackStart);

                    if (!playbackStarted)
                    {
                        playbackStarted = true;
                        playbackStopwatch.Start();
                        speechContext = new BargeInSpeechContext
                        {
                            AssistantTurnId = TurnId,
                            CorrelationId = CorrelationId,
                            SpeechType = SpeechPlaybackItemType.FinalAnswer,
                            SpokenText = audio.TextSegment.Text
                        };
                        if (_owner._speakerDuckingService.IsDucked)
                        {
                            _owner._speakerDuckingService.Restore(speechContext, "streaming_playback_start_reset_stale_ducking");
                        }

                        _owner.SetActiveVolumeSetter(volume => waveOut.Volume = volume);
                        _owner.ApplyOutputVolume(_owner._speakerDuckingService.CurrentVolumeMultiplier, "streaming_playback_start");
                        waveOut.Play();
                        _owner._playbackReferenceTap.NotifySpeechStarted(speechContext);
                        _firstPlaybackStartedMs ??= _stopwatch.ElapsedMilliseconds;
                        await TrySendEventAsync(_sendEventAsync, "SPEAKING_START", CorrelationId, null, speechCancellation.Token);
                        await _owner.EmitAssistantUiStateImmediateAsync(
                            AssistantUiStateEvent.Create(
                                "speaking",
                                "streaming_final_answer_playback_started",
                                CorrelationId,
                                TurnId,
                                speechItemType: "final_answer",
                                audiblePlaybackActive: true),
                            nameof(AssistantSpeechPlaybackService),
                            speechCancellation.Token);
                        _owner._logger.LogInformation(
                            "StreamingFinalAnswerFirstPlaybackStarted TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}. ElapsedMs: {ElapsedMs}.",
                            TurnId,
                            CorrelationId,
                            GenerationId,
                            audio.TextSegment.SegmentIndex,
                            SessionId,
                            _stopwatch.ElapsedMilliseconds);
                    }

                    if (_playedSegments > 0 && waitedForTtsWithEmptyPlaybackBuffer)
                    {
                        _totalInterSegmentGapMs += DrainPollMs;
                    }

                    var completionDueMs = cumulativeAudioMs;
                    segmentCompletionTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            while (!speechCancellation.Token.IsCancellationRequested)
                            {
                                if (playbackStopwatch.ElapsedMilliseconds >= completionDueMs)
                                {
                                    break;
                                }

                                await Task.Delay(DrainPollMs, speechCancellation.Token);
                            }

                            Interlocked.Increment(ref _playedSegments);
                            _owner._spokenAnswerTracking?.MarkChunkCompleted(TurnId, audio.TextSegment.Text, playbackStopwatch.Elapsed);
                            _owner._logger.LogInformation(
                                "StreamingFinalAnswerSegmentPlaybackCompleted TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SegmentIndex: {SegmentIndex}. SessionId: {SessionId}. PlaybackElapsedMs: {PlaybackElapsedMs}. WasTtsReadyBeforePreviousEnded: {WasTtsReadyBeforePreviousEnded}.",
                                TurnId,
                                CorrelationId,
                                GenerationId,
                                audio.TextSegment.SegmentIndex,
                                SessionId,
                                playbackStopwatch.Elapsed.TotalMilliseconds,
                                !waitedForTtsWithEmptyPlaybackBuffer);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }, CancellationToken.None));
                }

                if (bufferedWaveProvider is not null && waveOut is not null)
                {
                    await _owner.WaitForPlaybackDrainAsync(
                        bufferedWaveProvider,
                        playbackStopwatch,
                        activeState,
                        totalBytes,
                        bufferedWaveProvider.WaveFormat.SampleRate,
                        bufferedWaveProvider.WaveFormat.Channels,
                        speechCancellation.Token);
                }

                await Task.WhenAll(segmentCompletionTasks);

                if (waveOut is not null)
                {
                    waveOut.Stop();
                }

                if (playbackStarted)
                {
                    _owner._spokenAnswerTracking?.CompleteAnswer(TurnId);
                    await TrySendEventAsync(_sendEventAsync, "SPEAKING_END", CorrelationId, null, CancellationToken.None);
                    await _owner.EmitPlaybackCompletionUiStateAsync(
                        AssistantUiStateEvent.Create(
                            "idle",
                            "final_answer_completed",
                            CorrelationId,
                            TurnId,
                            speechItemType: "final_answer",
                            audiblePlaybackActive: false),
                        SpeechPlaybackItemType.FinalAnswer,
                        nameof(AssistantSpeechPlaybackService),
                        CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                _owner._spokenAnswerTracking?.MarkPlaybackCancelled(TurnId, _cancellationReason ?? "streaming_final_answer_cancelled");
                await TrySendEventAsync(_sendEventAsync, "SPEAKING_CANCELLED", CorrelationId, null, CancellationToken.None);
            }
            finally
            {
                if (playbackStarted && speechContext is not null)
                {
                    _owner._playbackReferenceTap.NotifySpeechStopped(speechContext);
                }

                _owner.ClearActiveVolumeSetter();
                waveOut?.Dispose();
                lock (_owner._playbackControlLock)
                {
                    if (ReferenceEquals(_owner._activeSpeechCancellation, speechCancellation))
                    {
                        activeState.HoldTimeoutCancellation?.Cancel();
                        activeState.HoldTimeoutCancellation?.Dispose();
                        _owner._activeSpeechCancellation = null;
                        _owner._activePlaybackState = null;
                        _owner._activePlaybackSnapshot = null;
                    }
                }

                _owner.ClearCurrentStreamingSession(this);
                _owner._speechGate.Release();
                _owner._logger.LogInformation(
                    "StreamingFinalAnswerSessionTimingSummary TurnId: {TurnId}. CorrelationId: {CorrelationId}. GenerationId: {GenerationId}. SessionId: {SessionId}. FirstSegmentAcceptedMs: {FirstSegmentAcceptedMs}. FirstAudioReadyMs: {FirstAudioReadyMs}. FirstPlaybackStartMs: {FirstPlaybackStartMs}. TotalSegments: {TotalSegments}. AudioSegments: {AudioSegments}. PlayedSegments: {PlayedSegments}. TotalTtsMs: {TotalTtsMs}. TotalInterSegmentGapMs: {TotalInterSegmentGapMs}. CancellationReason: {CancellationReason}.",
                    TurnId,
                    CorrelationId,
                    GenerationId,
                    SessionId,
                    _firstSegmentAcceptedMs,
                    _firstAudioReadyMs,
                    _firstPlaybackStartedMs,
                    _acceptedSegments,
                    _audioSegments,
                    _playedSegments,
                    _totalTtsMs,
                    _totalInterSegmentGapMs,
                    _cancellationReason);
            }
        }

        private static async Task WaitForStreamingBufferSpaceAsync(
            BufferedWaveProvider bufferedWaveProvider,
            int incomingBytes,
            CancellationToken cancellationToken)
        {
            while (bufferedWaveProvider.BufferedBytes + incomingBytes > bufferedWaveProvider.BufferLength)
            {
                await Task.Delay(BufferSpacePollMs, cancellationToken);
            }
        }
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
