using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class LiveInterruptionIntegrationService : ILiveInterruptionIntegrationService
{
    private readonly IConversationalInterruptionCandidateFactory _candidateFactory;
    private readonly IConversationalInterruptionClassifier _classifier;
    private readonly IInterruptionOrchestrator _orchestrator;
    private readonly IInterruptionPlaybackPort _playbackPort;
    private readonly IInterruptionFeedbackPort _feedbackPort;
    private readonly IInterruptionModelPort? _modelPort;
    private readonly IInterruptionSpeechOutputPort? _speechOutputPort;
    private readonly ILiveSpokenAnswerTrackingService? _spokenAnswerTracking;
    private readonly IStopConfirmationPhraseSelector? _stopConfirmationPhraseSelector;
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<LiveInterruptionIntegrationService> _logger;

    public LiveInterruptionIntegrationService(
        IConversationalInterruptionCandidateFactory candidateFactory,
        IConversationalInterruptionClassifier classifier,
        IInterruptionOrchestrator orchestrator,
        IInterruptionPlaybackPort playbackPort,
        IInterruptionFeedbackPort feedbackPort,
        IInterruptionRequestRouterPort requestRouterPort,
        IOptions<InterruptionHandlingOptions> options,
        ILogger<LiveInterruptionIntegrationService> logger,
        ILiveSpokenAnswerTrackingService? spokenAnswerTracking = null,
        IInterruptionModelPort? modelPort = null,
        IInterruptionSpeechOutputPort? speechOutputPort = null,
        IStopConfirmationPhraseSelector? stopConfirmationPhraseSelector = null)
    {
        _candidateFactory = candidateFactory;
        _classifier = classifier;
        _orchestrator = orchestrator;
        _playbackPort = playbackPort;
        _feedbackPort = feedbackPort;
        _modelPort = modelPort;
        _speechOutputPort = speechOutputPort;
        _stopConfirmationPhraseSelector = stopConfirmationPhraseSelector;
        _spokenAnswerTracking = spokenAnswerTracking;
        _ = requestRouterPort;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InterruptionHandlingResult?> TryHandleLiveInterruptionAsync(
        LiveInterruptionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outcome = await TryHandleYieldedInterruptionAsync(
            new YieldedInterruptionUtterance
            {
                Transcript = context.Transcript,
                YieldedByLayer1 = context.IsLikelyUserSpeech && !context.IsLikelySelfEcho,
                YieldReason = "legacy_live_interruption_context",
                ActiveTurnId = context.ActiveTurnId,
                CorrelationId = context.CorrelationId,
                Layer1Confidence = context.TranscriptConfidence,
                AssistantWasSpeakingOriginal = context.AssistantWasSpeaking,
                AssistantWasSpeakingResolved = context.AssistantWasSpeaking,
                OriginalUserQuestion = context.OriginalUserQuestion,
                CurrentAssistantSentence = context.CurrentAssistantSentence,
                LastCompletedAssistantSentence = context.LastCompletedAssistantSentence,
                StartedAtUtc = context.StartedAtUtc,
                EndedAtUtc = context.EndedAtUtc
            },
            cancellationToken);
        return outcome?.Result;
    }

    public async Task<LiveInterruptionHandlingOutcome?> TryHandleYieldedInterruptionAsync(
        YieldedInterruptionUtterance utterance,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        if (!_options.Enabled || !_options.EnableLiveBargeInIntegration)
        {
            return RuntimeOutcome(
                utterance.ActiveTurnId,
                utterance.CorrelationId,
                evaluated: false,
                handled: false,
                allowLegacyCleanup: true,
                allowLegacySemanticRouting: true,
                result: null,
                reason: "Conversational interruption live integration is disabled.");
        }

        if (!utterance.YieldedByLayer1)
        {
            _logger.LogInformation(
                "conversational_interruption_not_yielded_skipped TurnId: {TurnId}. CorrelationId: {CorrelationId}. Layer1Decision: {Layer1Decision}.",
                utterance.ActiveTurnId,
                utterance.CorrelationId,
                utterance.Layer1Decision);
            return null;
        }

        if (!HasInterruptionSpeechContext(utterance))
        {
            return IdleRequestSkippedOutcome(utterance);
        }

        var candidate = _candidateFactory.CreateFromYieldedInterruption(utterance);
        LogSpokenAnswerCheckpointDiagnostics(utterance);
        _logger.LogInformation(
            "conversational_interruption_yielded_utterance_observed TurnId: {TurnId}. CorrelationId: {CorrelationId}. yieldedObservedTurnId: {YieldedObservedTurnId}. resolvedActiveTurnId: {ResolvedActiveTurnId}. turnBindingSource: {TurnBindingSource}. activePlaybackCorrelationId: {ActivePlaybackCorrelationId}. activePlaybackSpeechType: {ActivePlaybackSpeechType}. provisionalAudioHoldId: {ProvisionalAudioHoldId}. wasHeldByProvisionalAudioHold: {WasHeldByProvisionalAudioHold}. recentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. recentlyYieldedSnapshotAgeMs: {RecentlyYieldedSnapshotAgeMs}. assistantWasSpeakingOriginal: {AssistantWasSpeakingOriginal}. assistantWasSpeakingResolved: {AssistantWasSpeakingResolved}. TranscriptLength: {TranscriptLength}. YieldReason: {YieldReason}. CaptureKind: {CaptureKind}. RouteKind: {RouteKind}. Layer1Confidence: {Layer1Confidence}. Layer1Decision: {Layer1Decision}. ShadowMode: {ShadowMode}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            utterance.OriginalObservedTurnId,
            utterance.ActiveTurnId,
            utterance.TurnBindingSource,
            utterance.ActivePlaybackCorrelationId,
            utterance.ActivePlaybackSpeechType,
            utterance.ProvisionalAudioHoldId,
            utterance.WasHeldByProvisionalAudioHold,
            utterance.RecentlyYieldedSnapshotFound,
            utterance.RecentlyYieldedSnapshotAgeMs,
            utterance.AssistantWasSpeakingOriginal,
            utterance.AssistantWasSpeakingResolved,
            candidate.Transcript?.Length ?? 0,
            utterance.YieldReason,
            utterance.CaptureKind,
            utterance.RouteKind,
            utterance.Layer1Confidence,
            utterance.Layer1Decision,
            _options.EnableLiveShadowMode);

        if (_options.EnableLiveShadowMode)
        {
            var shadowDecision = _classifier.Classify(candidate);
            _logger.LogInformation(
                "conversational_interruption_yielded_utterance_shadow_classified TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. ResultType: {ResultType}. SideEffectsSuppressed: {SideEffectsSuppressed}.",
                candidate.ActiveTurnId,
                candidate.CorrelationId,
                shadowDecision.Type,
                shadowDecision.Strategy,
                InterruptionHandlingResultType.Ignored,
                true);

            return new LiveInterruptionHandlingOutcome
            {
                WasEvaluatedByConversationalInterruption = true,
                WasHandledByConversationalInterruption = false,
                AllowLegacyCleanup = true,
                AllowLegacySemanticRouting = true,
                Result = new InterruptionHandlingResult
                {
                    Type = InterruptionHandlingResultType.Ignored,
                    Decision = shadowDecision,
                    Reason = "Yielded utterance shadow mode classified conversational meaning without orchestration side effects."
                },
                InterruptionType = shadowDecision.Type,
                Strategy = shadowDecision.Strategy,
                Reason = "Shadow mode classified meaning only."
            };
        }

        if (!_options.EnableLiveMinimalBehavior)
        {
            return DeferToOldPath(candidate, new ConversationalInterruptionDecision(), "Live minimal behavior is disabled.");
        }

        var decision = _classifier.Classify(candidate);
        return decision.Strategy switch
        {
            ConversationalInterruptionHandlingStrategy.IgnoreAndContinue =>
                await HandleContinueAsync(candidate, utterance, decision, InterruptionHandlingResultType.Ignored, "Yielded utterance is conversationally useless.", cancellationToken),
            ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse =>
                await HandleContinueAsync(candidate, utterance, decision, InterruptionHandlingResultType.Continued, "Yielded utterance is a backchannel/passive agreement.", cancellationToken),
            ConversationalInterruptionHandlingStrategy.StopPlayback =>
                await HandleStopAsync(candidate, utterance, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.CancelAndRedirect =>
                await HandleCorrectionRedirectAsync(candidate, utterance, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption =>
                DeferToOldPath(candidate, decision, "Ask-user-to-clarify is not executed live in PR7."),
            ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint =>
                await HandleSequentialClarificationRecompositionAsync(candidate, utterance, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint
                or ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent =>
                DeferToOldPath(candidate, decision, $"Strategy {decision.Strategy} is deferred until recomposition PR."),
            _ => DeferToOldPath(candidate, decision, $"Strategy {decision.Strategy} is not supported by PR8.")
        };
    }

    private LiveInterruptionHandlingOutcome IdleRequestSkippedOutcome(YieldedInterruptionUtterance utterance)
    {
        const string reason = "No active or recently-yielded final-answer speech context; treat as normal request.";
        _logger.LogInformation(
            "conversational_interruption_skipped_idle_request ObservedTurnId: {ObservedTurnId}. ResolvedTurnId: {ResolvedTurnId}. AssistantWasSpeakingOriginal: {AssistantWasSpeakingOriginal}. AssistantWasSpeakingResolved: {AssistantWasSpeakingResolved}. RecentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. RecentlyYieldedSnapshotAgeMs: {RecentlyYieldedSnapshotAgeMs}. TurnBindingSource: {TurnBindingSource}. TranscriptLength: {TranscriptLength}. Reason: {Reason}.",
            utterance.OriginalObservedTurnId,
            utterance.ActiveTurnId,
            utterance.AssistantWasSpeakingOriginal,
            utterance.AssistantWasSpeakingResolved,
            utterance.RecentlyYieldedSnapshotFound,
            utterance.RecentlyYieldedSnapshotAgeMs,
            utterance.TurnBindingSource,
            utterance.Transcript?.Length ?? 0,
            reason);

        return RuntimeOutcome(
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            evaluated: false,
            handled: false,
            allowLegacyCleanup: true,
            allowLegacySemanticRouting: true,
            result: null,
            reason: reason);
    }

    private static bool HasInterruptionSpeechContext(YieldedInterruptionUtterance utterance)
    {
        return utterance.AssistantWasSpeakingResolved == true
            || utterance.RecentlyYieldedSnapshotFound;
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleContinueAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        InterruptionHandlingResultType resultType,
        string reason,
        CancellationToken cancellationToken)
    {
        await ResolveProvisionalAudioHoldAsync(
            candidate,
            utterance,
            decision,
            ProvisionalAudioHoldResolutionAction.Resume,
            reason,
            cancellationToken);

        _logger.LogInformation(
            "conversational_interruption_runtime_outcome TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Handled: {Handled}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. ResultType: {ResultType}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            true,
            true,
            false,
            resultType,
            reason);
        _logger.LogInformation(
            "conversational_interruption_legacy_semantic_routing_suppressed TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = true,
            AllowLegacyCleanup = true,
            AllowLegacySemanticRouting = false,
            ShouldResumeOrContinuePlaybackIfPossible = true,
            Result = Result(resultType, decision, reason),
            InterruptionType = decision.Type,
            Strategy = decision.Strategy,
            Reason = reason
        };
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleStopAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableLivePlaybackActions)
        {
            return DeferToOldPath(candidate, decision, "Live playback actions are disabled.");
        }

        await ResolveProvisionalAudioHoldAsync(
            candidate,
            utterance,
            decision,
            ProvisionalAudioHoldResolutionAction.Flush,
            "conversational_interruption_stop",
            cancellationToken);
        await _playbackPort.FlushFinalAnswerSpeechForTurnAsync(
            candidate.ActiveTurnId,
            "conversational_interruption_stop",
            cancellationToken);
        if (decision.Type is ConversationalInterruptionType.CancelRequest)
        {
            await _playbackPort.CancelCurrentAsync(candidate.ActiveTurnId, decision.Reason, cancellationToken);
        }
        else
        {
            await _playbackPort.StopCurrentAsync(candidate.ActiveTurnId, decision.Reason, cancellationToken);
        }

        var stopConfirmation = _stopConfirmationPhraseSelector?.SelectPhrase()
            ?? StopConfirmationPhraseSelector.DefaultPhrases[0];
        _logger.LogInformation(
            "stop_confirmation_selected TurnId: {TurnId}. CorrelationId: {CorrelationId}. Phrase: {Phrase}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            stopConfirmation);
        if (_speechOutputPort is not null)
        {
            await _speechOutputPort.SpeakInterruptionContentAsync(
                candidate.ActiveTurnId,
                candidate.CorrelationId,
                stopConfirmation,
                "stop_confirmation",
                cancellationToken);
            _logger.LogInformation(
                "stop_confirmation_spoken_or_queued TurnId: {TurnId}. CorrelationId: {CorrelationId}. TextLength: {TextLength}.",
                candidate.ActiveTurnId,
                candidate.CorrelationId,
                stopConfirmation.Length);
        }

        _logger.LogInformation(
            "conversational_interruption_live_stop_executed TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = true,
            AllowLegacyCleanup = false,
            AllowLegacySemanticRouting = false,
            ShouldCancelPlayback = true,
            ShouldCancelCurrentTurn = true,
            Result = Result(
                InterruptionHandlingResultType.Stopped,
                decision,
                "Stop/cancel handled with local stop confirmation.",
                playbackCancelled: true,
                originalTurnCancelled: true),
            InterruptionType = decision.Type,
            Strategy = decision.Strategy,
            Reason = "Stop/cancel handled with local stop confirmation."
        };
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleCorrectionRedirectAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableLivePlaybackActions)
        {
            return DeferToOldPath(candidate, decision, "Live playback actions are disabled.");
        }

        if (!_options.EnableLiveRedirectRouting)
        {
            return DeferToOldPath(candidate, decision, "Live redirect routing is disabled.");
        }

        if (string.IsNullOrWhiteSpace(decision.RewrittenUserRequest))
        {
            return DeferToOldPath(candidate, decision, "Correction did not include a rewritten request.");
        }

        await ResolveProvisionalAudioHoldAsync(
            candidate,
            utterance,
            decision,
            ProvisionalAudioHoldResolutionAction.Flush,
            "conversational_interruption_redirect",
            cancellationToken);
        await _playbackPort.FlushFinalAnswerSpeechForTurnAsync(
            candidate.ActiveTurnId,
            "conversational_interruption_redirect",
            cancellationToken);
        await _playbackPort.CancelCurrentAsync(candidate.ActiveTurnId, decision.Reason, cancellationToken);

        var focusAction = new ConversationFocusAction
        {
            Type = ConversationFocusActionType.CancelAndReplaceMainTurn,
            ActiveTurnId = candidate.ActiveTurnId,
            RewrittenRequest = decision.RewrittenUserRequest,
            ShouldCancelPlayback = true,
            ShouldCancelOriginalTurn = true,
            RequiresBridgeFeedback = decision.RequiresBridgeFeedback,
            Reason = decision.Reason
        };

        await _feedbackPort.SuppressNormalProgressAsync(candidate.ActiveTurnId, cancellationToken);
        if (decision.RequiresBridgeFeedback && _options.EnableLiveResponsiveFeedbackBridge)
        {
            await _feedbackPort.RequestBridgeFeedbackAsync(candidate, decision, focusAction, cancellationToken);
        }

        _logger.LogInformation(
            "conversational_interruption_live_correction_redirect_executed TurnId: {TurnId}. CorrelationId: {CorrelationId}. RewrittenLength: {RewrittenLength}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.RewrittenUserRequest.Length);
        _logger.LogInformation(
            "conversational_interruption_replacement_routing_requested TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. RewrittenLength: {RewrittenLength}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            decision.RewrittenUserRequest.Length);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = true,
            AllowLegacyCleanup = false,
            AllowLegacySemanticRouting = false,
            ShouldCancelPlayback = true,
            ShouldCancelCurrentTurn = true,
            ShouldRouteReplacementRequest = true,
            RewrittenRequest = decision.RewrittenUserRequest,
            Result = Result(
                InterruptionHandlingResultType.CancelledAndRedirected,
                decision,
                "Correction/redirect handled by PR8 single yielded-utterance runtime path.",
                redirectedRequest: decision.RewrittenUserRequest,
                playbackCancelled: true,
                originalTurnCancelled: true,
                bridgeFeedbackRequested: decision.RequiresBridgeFeedback && _options.EnableLiveResponsiveFeedbackBridge,
                normalProgressSuppressed: _options.EnableLiveResponsiveFeedbackBridge),
            InterruptionType = decision.Type,
            Strategy = decision.Strategy,
            Reason = "Correction/redirect handled by PR8 single yielded-utterance runtime path."
        };
    }

    private LiveInterruptionHandlingOutcome DeferToOldPath(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        string reason)
    {
        _logger.LogInformation(
            "conversational_interruption_strategy_deferred TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            true,
            true,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = false,
            AllowLegacyCleanup = true,
            AllowLegacySemanticRouting = true,
            Result = Result(InterruptionHandlingResultType.Ignored, decision, reason),
            InterruptionType = decision.Type,
            Strategy = decision.Strategy,
            Reason = reason
        };
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleSequentialClarificationRecompositionAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        CancellationToken cancellationToken)
    {
        var disabledReason = GetSequentialRecompositionDisabledReason();
        if (disabledReason is not null)
        {
            return DeferToOldPath(candidate, decision, disabledReason);
        }

        var checkpoint = TryCreateLiveCheckpoint(utterance);
        if (checkpoint is null)
        {
            return FailedHandledOutcome(
                candidate,
                decision,
                "Sequential recomposition could not create a checkpoint or fallback with original question.");
        }

        await ResolveProvisionalAudioHoldAsync(
            candidate,
            utterance,
            decision,
            ProvisionalAudioHoldResolutionAction.Flush,
            "conversational_interruption_recomposition",
            cancellationToken);
        await _playbackPort.FlushFinalAnswerSpeechForTurnAsync(
            candidate.ActiveTurnId,
            "conversational_interruption_recomposition",
            cancellationToken);
        await _feedbackPort.SuppressNormalProgressAsync(candidate.ActiveTurnId, cancellationToken);
        await _playbackPort.CancelCurrentAsync(candidate.ActiveTurnId, decision.Reason, cancellationToken);

        try
        {
            var clarificationRequest = BuildClarificationRequest(candidate, checkpoint);
            var clarification = await _modelPort!.GenerateClarificationAsync(clarificationRequest, cancellationToken);
            await _speechOutputPort!.SpeakInterruptionContentAsync(
                candidate.ActiveTurnId,
                candidate.CorrelationId,
                clarification.ReplyText,
                "clarification",
                cancellationToken);

            ContinuationRecompositionRequest? continuationRequest = null;
            ContinuationRecompositionResult? continuation = null;
            if (clarification.ShouldRecomposeContinuation)
            {
                continuationRequest = BuildContinuationRequest(candidate, checkpoint, clarification);
                continuation = await _modelPort.GenerateContinuationAsync(continuationRequest, cancellationToken);
                await _speechOutputPort.SpeakInterruptionContentAsync(
                    candidate.ActiveTurnId,
                    candidate.CorrelationId,
                    continuation.ContinuationText,
                    "recomposed_continuation",
                    cancellationToken);
            }

            var resultType = continuation is null
                ? InterruptionHandlingResultType.ClarificationPrepared
                : InterruptionHandlingResultType.ClarificationAndRecompositionPrepared;
            return new LiveInterruptionHandlingOutcome
            {
                WasEvaluatedByConversationalInterruption = true,
                WasHandledByConversationalInterruption = true,
                AllowLegacyCleanup = false,
                AllowLegacySemanticRouting = false,
                ShouldCancelPlayback = true,
                ShouldCancelCurrentTurn = false,
                Result = Result(
                    resultType,
                    decision,
                    continuation is null
                        ? "Sequential clarification handled without continuation."
                        : "Sequential clarification and recomposition handled.",
                    playbackCancelled: true,
                    normalProgressSuppressed: true,
                    checkpoint: checkpoint,
                    clarificationRequest: clarificationRequest,
                    clarificationResult: clarification,
                    continuationRequest: continuationRequest,
                    continuationResult: continuation),
                InterruptionType = decision.Type,
                Strategy = decision.Strategy,
                Reason = continuation is null
                    ? "Sequential clarification handled without continuation."
                    : "Sequential clarification and recomposition handled."
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "conversational_interruption_sequential_recomposition_failed TurnId: {TurnId}. CorrelationId: {CorrelationId}.", candidate.ActiveTurnId, candidate.CorrelationId);
            return FailedHandledOutcome(candidate, decision, $"Sequential recomposition failed: {exception.Message}", playbackCancelled: true);
        }
    }

    private async Task<ProvisionalAudioHoldResult?> ResolveProvisionalAudioHoldAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        ProvisionalAudioHoldResolutionAction action,
        string reason,
        CancellationToken cancellationToken)
    {
        var holdId = utterance.ProvisionalAudioHoldId;
        var eventName = action is ProvisionalAudioHoldResolutionAction.Resume
            ? "conversational_interruption_hold_resumed"
            : "conversational_interruption_hold_flushed";
        var unavailableEventName = action is ProvisionalAudioHoldResolutionAction.Resume
            ? "conversational_interruption_hold_resume_unavailable"
            : "conversational_interruption_hold_flush_unavailable";

        if (string.IsNullOrWhiteSpace(holdId))
        {
            LogHoldResolution(
                unavailableEventName,
                candidate,
                utterance,
                decision,
                action,
                holdId,
                success: false,
                failureReason: "No provisional audio hold id was available.",
                reason);
            return null;
        }

        var result = action is ProvisionalAudioHoldResolutionAction.Resume
            ? await _playbackPort.ResumeProvisionalAudioHoldAsync(holdId, reason, cancellationToken)
            : await _playbackPort.FlushProvisionalAudioHoldAsync(holdId, reason, cancellationToken);

        LogHoldResolution(
            result.Success ? eventName : unavailableEventName,
            candidate,
            utterance,
            decision,
            action,
            result.HoldId ?? holdId,
            result.Success,
            result.FailureReason,
            reason);
        return result;
    }

    private void LogHoldResolution(
        string eventName,
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        ProvisionalAudioHoldResolutionAction action,
        string? holdId,
        bool success,
        string? failureReason,
        string reason)
    {
        _logger.LogInformation(
            "{EventName} TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Action: {Action}. Reason: {Reason}. WasHeld: {WasHeld}. RecentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. TurnBindingSource: {TurnBindingSource}. Success: {Success}. FailureReason: {FailureReason}.",
            eventName,
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            holdId,
            decision.Type,
            decision.Strategy,
            action.ToString().ToLowerInvariant(),
            reason,
            utterance.WasHeldByProvisionalAudioHold,
            utterance.RecentlyYieldedSnapshotFound,
            utterance.TurnBindingSource,
            success,
            failureReason);
    }

    private string? GetSequentialRecompositionDisabledReason()
    {
        if (!_options.EnableSequentialRecomposition)
        {
            return "Sequential recomposition is disabled.";
        }

        if (!_options.EnableLivePlaybackActions)
        {
            return "Live playback actions are disabled.";
        }

        if (!_options.EnableLiveSpokenAnswerTracking)
        {
            return "Live spoken answer tracking is disabled.";
        }

        if (!_options.EnableLiveModelCalls)
        {
            return "Live model calls are disabled.";
        }

        if (!_options.EnableClarificationCalls)
        {
            return "Clarification calls are disabled.";
        }

        if (!_options.EnableContinuationRecomposition)
        {
            return "Continuation recomposition is disabled.";
        }

        if (_modelPort is null)
        {
            return "No interruption model port is configured.";
        }

        if (_speechOutputPort is null)
        {
            return "No interruption speech output port is configured.";
        }

        return null;
    }

    private SpokenAnswerCheckpoint? TryCreateLiveCheckpoint(YieldedInterruptionUtterance utterance)
    {
        var checkpoint = _spokenAnswerTracking?.TryCreateCheckpoint(
            utterance.ActiveTurnId,
            discardCurrentPartialSentence: true);
        if (HasMinimumCheckpointInputs(checkpoint))
        {
            return checkpoint;
        }

        if (string.IsNullOrWhiteSpace(utterance.OriginalUserQuestion)
            || string.IsNullOrWhiteSpace(utterance.Transcript))
        {
            return null;
        }

        return new SpokenAnswerCheckpoint
        {
            TurnId = utterance.ActiveTurnId,
            CorrelationId = utterance.CorrelationId,
            OriginalUserQuestion = utterance.OriginalUserQuestion,
            SafeSpokenPrefix = utterance.LastCompletedAssistantSentence ?? string.Empty,
            LastCompletedSentence = utterance.LastCompletedAssistantSentence ?? string.Empty,
            DiscardedPartialSentence = utterance.CurrentAssistantSentence ?? string.Empty,
            OriginalPlanOrIntent = utterance.CurrentAssistantSentence
        };
    }

    private static bool HasMinimumCheckpointInputs(SpokenAnswerCheckpoint? checkpoint)
    {
        return checkpoint is not null
            && !string.IsNullOrWhiteSpace(checkpoint.OriginalUserQuestion);
    }

    private ClarificationRequest BuildClarificationRequest(
        ConversationalInterruptionCandidate candidate,
        SpokenAnswerCheckpoint checkpoint)
    {
        return new ClarificationRequest
        {
            OriginalUserQuestion = checkpoint.OriginalUserQuestion,
            SpokenAnswerSoFar = checkpoint.SafeSpokenPrefix,
            LastCompletedSentence = checkpoint.LastCompletedSentence,
            DiscardedPartialSentence = checkpoint.DiscardedPartialSentence,
            UserInterruption = candidate.Transcript ?? string.Empty,
            CurrentTopicLabel = checkpoint.CurrentTopicLabel,
            MaxTokens = _options.ClarificationMaxTokens
        };
    }

    private ContinuationRecompositionRequest BuildContinuationRequest(
        ConversationalInterruptionCandidate candidate,
        SpokenAnswerCheckpoint checkpoint,
        ClarificationResult clarification)
    {
        return new ContinuationRecompositionRequest
        {
            OriginalUserQuestion = checkpoint.OriginalUserQuestion,
            SpokenAnswerSoFar = checkpoint.SafeSpokenPrefix,
            LastCompletedSentence = checkpoint.LastCompletedSentence,
            DiscardedPartialSentence = checkpoint.DiscardedPartialSentence,
            UserInterruption = candidate.Transcript ?? string.Empty,
            ClarificationReply = clarification.ReplyText,
            ClarificationContext = clarification.ClarificationContext,
            CurrentTopicLabel = checkpoint.CurrentTopicLabel,
            OriginalPlanOrIntent = checkpoint.OriginalPlanOrIntent,
            MaxTokens = _options.ContinuationMaxTokens
        };
    }

    private LiveInterruptionHandlingOutcome FailedHandledOutcome(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        string reason,
        bool playbackCancelled = false)
    {
        _logger.LogInformation(
            "conversational_interruption_sequential_recomposition_failed_outcome TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = true,
            AllowLegacyCleanup = true,
            AllowLegacySemanticRouting = false,
            ShouldCancelPlayback = playbackCancelled,
            ShouldCancelCurrentTurn = false,
            Result = Result(
                InterruptionHandlingResultType.Failed,
                decision,
                reason,
                playbackCancelled: playbackCancelled),
            InterruptionType = decision.Type,
            Strategy = decision.Strategy,
            Reason = reason
        };
    }

    private LiveInterruptionHandlingOutcome RuntimeOutcome(
        string turnId,
        string correlationId,
        bool evaluated,
        bool handled,
        bool allowLegacyCleanup,
        bool allowLegacySemanticRouting,
        InterruptionHandlingResult? result,
        string reason)
    {
        _logger.LogInformation(
            "conversational_interruption_runtime_outcome TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Handled: {Handled}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. ResultType: {ResultType}. Reason: {Reason}.",
            turnId,
            correlationId,
            result?.Decision.Type,
            result?.Decision.Strategy,
            handled,
            allowLegacyCleanup,
            allowLegacySemanticRouting,
            result?.Type,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = evaluated,
            WasHandledByConversationalInterruption = handled,
            AllowLegacyCleanup = allowLegacyCleanup,
            AllowLegacySemanticRouting = allowLegacySemanticRouting,
            Result = result,
            InterruptionType = result?.Decision.Type,
            Strategy = result?.Decision.Strategy,
            Reason = reason
        };
    }

    private static InterruptionHandlingResult Result(
        InterruptionHandlingResultType type,
        ConversationalInterruptionDecision decision,
        string reason,
        string? redirectedRequest = null,
        bool playbackCancelled = false,
        bool originalTurnCancelled = false,
        bool bridgeFeedbackRequested = false,
        bool normalProgressSuppressed = false,
        SpokenAnswerCheckpoint? checkpoint = null,
        ClarificationRequest? clarificationRequest = null,
        ClarificationResult? clarificationResult = null,
        ContinuationRecompositionRequest? continuationRequest = null,
        ContinuationRecompositionResult? continuationResult = null) =>
        new()
        {
            Type = type,
            Decision = decision,
            RedirectedRequest = redirectedRequest,
            Checkpoint = checkpoint,
            ClarificationRequest = clarificationRequest,
            ClarificationResult = clarificationResult,
            ContinuationRequest = continuationRequest,
            ContinuationResult = continuationResult,
            PlaybackCancelled = playbackCancelled,
            OriginalTurnCancelled = originalTurnCancelled,
            BridgeFeedbackRequested = bridgeFeedbackRequested,
            NormalProgressSuppressed = normalProgressSuppressed,
            Reason = reason
        };

    private void LogSpokenAnswerCheckpointDiagnostics(YieldedInterruptionUtterance utterance)
    {
        if (!_options.EnableSpokenAnswerTrackingDiagnostics)
        {
            return;
        }

        if (!_options.EnableLiveSpokenAnswerTracking)
        {
            _logger.LogInformation(
                "spoken_answer_checkpoint_diagnostic TurnId: {TurnId}. CorrelationId: {CorrelationId}. TrackingEnabled: {TrackingEnabled}. HasCheckpoint: {HasCheckpoint}. Reason: {Reason}.",
                utterance.ActiveTurnId,
                utterance.CorrelationId,
                false,
                false,
                "tracking_disabled");
            return;
        }

        var checkpoint = _spokenAnswerTracking?.TryCreateCheckpoint(
            utterance.ActiveTurnId,
            discardCurrentPartialSentence: true);
        var missingReason = checkpoint is null
            ? "no_tracker_state_for_resolved_turn"
            : null;
        _logger.LogInformation(
            "spoken_answer_checkpoint_diagnostic TurnId: {TurnId}. CorrelationId: {CorrelationId}. checkpointTurnId: {CheckpointTurnId}. checkpointFound: {CheckpointFound}. TrackingEnabled: {TrackingEnabled}. HasCheckpoint: {HasCheckpoint}. LastCompletedSentenceLength: {LastCompletedSentenceLength}. CurrentPartialSentenceLength: {CurrentPartialSentenceLength}. SafeSpokenPrefixLength: {SafeSpokenPrefixLength}. observedTurnId: {ObservedTurnId}. resolvedTurnId: {ResolvedTurnId}. turnBindingSource: {TurnBindingSource}. recentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. recentlyYieldedSnapshotAgeMs: {RecentlyYieldedSnapshotAgeMs}. checkpointMissingReason: {CheckpointMissingReason}.",
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            utterance.ActiveTurnId,
            checkpoint is not null,
            true,
            checkpoint is not null,
            checkpoint?.LastCompletedSentence.Length ?? 0,
            checkpoint?.DiscardedPartialSentence.Length ?? 0,
            checkpoint?.SafeSpokenPrefix.Length ?? 0,
            utterance.OriginalObservedTurnId,
            utterance.ActiveTurnId,
            utterance.TurnBindingSource,
            utterance.RecentlyYieldedSnapshotFound,
            utterance.RecentlyYieldedSnapshotAgeMs,
            missingReason);
    }
}

internal enum ProvisionalAudioHoldResolutionAction
{
    Resume,
    Flush
}
