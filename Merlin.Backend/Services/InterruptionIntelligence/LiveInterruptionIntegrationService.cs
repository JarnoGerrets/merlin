using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
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
    private readonly IPendingInterruptionClarificationService? _pendingClarifications;
    private readonly AssistantUiStateBroadcaster? _assistantUiStateBroadcaster;
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
        IStopConfirmationPhraseSelector? stopConfirmationPhraseSelector = null,
        IPendingInterruptionClarificationService? pendingClarifications = null,
        AssistantUiStateBroadcaster? assistantUiStateBroadcaster = null)
    {
        _candidateFactory = candidateFactory;
        _classifier = classifier;
        _orchestrator = orchestrator;
        _playbackPort = playbackPort;
        _feedbackPort = feedbackPort;
        _modelPort = modelPort;
        _speechOutputPort = speechOutputPort;
        _stopConfirmationPhraseSelector = stopConfirmationPhraseSelector;
        _pendingClarifications = pendingClarifications;
        _assistantUiStateBroadcaster = assistantUiStateBroadcaster;
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
                "conversational_interruption_not_yielded_skipped CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Layer1Decision: {Layer1Decision}.",
                utterance.CaptureId,
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
            "conversational_interruption_yielded_utterance_observed CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. yieldedObservedTurnId: {YieldedObservedTurnId}. resolvedActiveTurnId: {ResolvedActiveTurnId}. turnBindingSource: {TurnBindingSource}. activePlaybackCorrelationId: {ActivePlaybackCorrelationId}. activePlaybackSpeechType: {ActivePlaybackSpeechType}. provisionalAudioHoldId: {ProvisionalAudioHoldId}. wasHeldByProvisionalAudioHold: {WasHeldByProvisionalAudioHold}. recentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. recentlyYieldedSnapshotAgeMs: {RecentlyYieldedSnapshotAgeMs}. assistantWasSpeakingOriginal: {AssistantWasSpeakingOriginal}. assistantWasSpeakingResolved: {AssistantWasSpeakingResolved}. TranscriptLength: {TranscriptLength}. YieldReason: {YieldReason}. CaptureKind: {CaptureKind}. RouteKind: {RouteKind}. RouteAction: {RouteAction}. Layer1Confidence: {Layer1Confidence}. Layer1Decision: {Layer1Decision}. ShadowMode: {ShadowMode}.",
            utterance.CaptureId,
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
            utterance.RouteAction,
            utterance.Layer1Confidence,
            utterance.Layer1Decision,
            _options.EnableLiveShadowMode);

        if (_options.EnableLiveShadowMode)
        {
            var shadowDecision = _classifier.Classify(candidate);
            _logger.LogInformation(
                "conversational_interruption_yielded_utterance_shadow_classified CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. ResultType: {ResultType}. SideEffectsSuppressed: {SideEffectsSuppressed}.",
                utterance.CaptureId,
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

        var decision = TryMapPlaybackControlStopToDecision(utterance, candidate)
            ?? _classifier.Classify(candidate);
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
                await HandleAskUserToClarifyInterruptionAsync(candidate, utterance, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint =>
                await HandleSequentialClarificationRecompositionAsync(candidate, utterance, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint
                or ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent =>
                await HandleTerminalFallbackAsync(
                    candidate,
                    utterance,
                    decision,
                    $"Strategy {decision.Strategy} has no executable live recomposition owner; resumed held playback instead of deferring.",
                    cancellationToken),
            _ => await HandleTerminalFallbackAsync(
                candidate,
                utterance,
                decision,
                $"Strategy {decision.Strategy} is not supported by the live interruption runtime; resumed held playback instead of deferring.",
                cancellationToken)
        };
    }

    public async Task<LiveInterruptionHandlingOutcome?> TryHandlePendingClarificationResponseAsync(
        PendingInterruptionClarificationResponse response,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

        var pending = response.Pending;
        var decision = PendingClarificationDecision();
        var candidate = new ConversationalInterruptionCandidate
        {
            Transcript = BuildResolvedPendingInterruptionText(pending, response),
            ActiveTurnId = pending.ActiveTurnId,
            CorrelationId = pending.CorrelationId,
            TranscriptConfidence = 1.0,
            OriginalUserQuestion = pending.OriginalUserQuestion,
            CurrentAssistantSentence = pending.DiscardedPartialSentence,
            LastCompletedAssistantSentence = pending.LastCompletedSentence,
            StartedAtUtc = pending.CreatedAtUtc,
            EndedAtUtc = response.ConsumedAtUtc
        };

        if (!_options.Enabled || !_options.EnableLiveBargeInIntegration || !_options.EnableLiveMinimalBehavior)
        {
            return await HandlePendingClarificationFailedAsync(
                candidate,
                decision,
                "Pending clarification response could not run because live interruption integration is disabled.",
                cancellationToken);
        }

        var disabledReason = GetSequentialRecompositionDisabledReason();
        if (disabledReason is not null)
        {
            return await HandlePendingClarificationFailedAsync(
                candidate,
                decision,
                $"Pending clarification response could not recompose: {disabledReason}",
                cancellationToken);
        }

        var checkpoint = CreateCheckpointFromPending(pending);
        if (!HasMinimumCheckpointInputs(checkpoint))
        {
            return await HandlePendingClarificationFailedAsync(
                candidate,
                decision,
                "Pending clarification response could not recompose because the original interruption checkpoint was incomplete.",
                cancellationToken);
        }

        await ResolvePendingProvisionalHoldAsync(pending, decision, cancellationToken);
        await _playbackPort.FlushFinalAnswerSpeechForTurnAsync(
            pending.ActiveTurnId,
            "pending_interruption_clarification_recomposition",
            cancellationToken);
        await _feedbackPort.SuppressNormalProgressAsync(pending.ActiveTurnId, cancellationToken);
        await _playbackPort.CancelCurrentAsync(
            pending.ActiveTurnId,
            decision.Reason,
            cancellationToken);

        try
        {
            var clarification = new ClarificationResult
            {
                ReplyText = response.ResponseText,
                ClarificationContext = BuildPendingClarificationContext(pending, response),
                ShouldRecomposeContinuation = true,
                UserQuestionAnswered = true
            };
            var continuationRequest = BuildContinuationRequest(candidate, checkpoint!, clarification);
            var continuation = await _modelPort!.GenerateContinuationAsync(continuationRequest, cancellationToken);
            await _speechOutputPort!.SpeakInterruptionContentAsync(
                pending.ActiveTurnId,
                pending.CorrelationId,
                continuation.ContinuationText,
                "recomposed_continuation",
                cancellationToken);
            await EmitPendingClarificationResolvedAsync(
                pending,
                "pending_interruption_clarification_recomposition_completed",
                "speaking",
                "interruption_continuation",
                audiblePlaybackActive: true,
                cancellationToken);

            var reason = "Pending clarification response recomposed continuation.";
            _logger.LogInformation(
                "pending_interruption_clarification_recomposed ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. ResponseCaptureId: {ResponseCaptureId}. ResponseLength: {ResponseLength}.",
                pending.ClarificationId,
                pending.ActiveTurnId,
                pending.CorrelationId,
                response.CaptureId,
                response.ResponseText.Length);
            return new LiveInterruptionHandlingOutcome
            {
                WasEvaluatedByConversationalInterruption = true,
                WasHandledByConversationalInterruption = true,
                AllowLegacyCleanup = false,
                AllowLegacySemanticRouting = false,
                ShouldCancelPlayback = true,
                ShouldCancelCurrentTurn = false,
                PendingClarificationId = pending.ClarificationId,
                Result = Result(
                    InterruptionHandlingResultType.ClarificationAndRecompositionPrepared,
                    decision,
                    reason,
                    playbackCancelled: true,
                    normalProgressSuppressed: true,
                    checkpoint: checkpoint,
                    clarificationResult: clarification,
                    continuationRequest: continuationRequest,
                    continuationResult: continuation),
                InterruptionType = decision.Type,
                Strategy = decision.Strategy,
                Reason = reason
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "pending_interruption_clarification_recomposition_failed ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}.",
                pending.ClarificationId,
                pending.ActiveTurnId,
                pending.CorrelationId);
            return await HandlePendingClarificationFailedAsync(
                candidate,
                decision,
                $"Pending clarification recomposition failed: {exception.Message}",
                cancellationToken,
                playbackCancelled: true);
        }
    }

    private LiveInterruptionHandlingOutcome IdleRequestSkippedOutcome(YieldedInterruptionUtterance utterance)
    {
        const string reason = "No active or recently-yielded final-answer speech context; treat as normal request.";
        _logger.LogInformation(
            "conversational_interruption_skipped_idle_request CaptureId: {CaptureId}. ObservedTurnId: {ObservedTurnId}. ResolvedTurnId: {ResolvedTurnId}. AssistantWasSpeakingOriginal: {AssistantWasSpeakingOriginal}. AssistantWasSpeakingResolved: {AssistantWasSpeakingResolved}. RecentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. RecentlyYieldedSnapshotAgeMs: {RecentlyYieldedSnapshotAgeMs}. TurnBindingSource: {TurnBindingSource}. TranscriptLength: {TranscriptLength}. Reason: {Reason}.",
            utterance.CaptureId,
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

    private ConversationalInterruptionDecision? TryMapPlaybackControlStopToDecision(
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionCandidate candidate)
    {
        if (!IsPlaybackControlStop(utterance))
        {
            return null;
        }

        var decision = new ConversationalInterruptionDecision
        {
            Type = ConversationalInterruptionType.StopRequest,
            Strategy = ConversationalInterruptionHandlingStrategy.StopPlayback,
            Confidence = Math.Clamp(utterance.Layer1Confidence ?? 0.94, 0.0, 1.0),
            PausePlayback = true,
            CancelOriginalTurn = true,
            ResumeRawPlayback = false,
            DiscardCurrentPartialSentence = true,
            RequiresBridgeFeedback = false,
            RequiresDeepInfraClarification = false,
            RequiresContinuationRecomposition = false,
            ClarificationMaxTokens = _options.ClarificationMaxTokens,
            ContinuationMaxTokens = _options.ContinuationMaxTokens,
            Reason = "Layer 1 playback-control stop route mapped to conversational stop request."
        };

        _logger.LogInformation(
            "playback_control_stop_mapped_to_ci_stop CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Transcript: {Transcript}. LiveGateDecision: {LiveGateDecision}. RouteAction: {RouteAction}. DecisionType: {DecisionType}. Strategy: {Strategy}. AssistantWasSpeakingOriginal: {AssistantWasSpeakingOriginal}. AssistantWasSpeakingResolved: {AssistantWasSpeakingResolved}. TurnBindingSource: {TurnBindingSource}. RecentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. HoldId: {HoldId}. StopConfirmationExpected: {StopConfirmationExpected}.",
            utterance.CaptureId,
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            utterance.Transcript,
            utterance.Layer1Decision,
            utterance.RouteAction,
            decision.Type,
            decision.Strategy,
            utterance.AssistantWasSpeakingOriginal,
            utterance.AssistantWasSpeakingResolved,
            utterance.TurnBindingSource,
            utterance.RecentlyYieldedSnapshotFound,
            utterance.ProvisionalAudioHoldId,
            true);

        return decision;
    }

    private static bool IsPlaybackControlStop(YieldedInterruptionUtterance utterance)
    {
        return string.Equals(utterance.Layer1Decision, "AcceptPlaybackControl", StringComparison.Ordinal)
            && string.Equals(utterance.RouteAction, "StopSpeechOnlyNoConfirmation", StringComparison.Ordinal);
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

    private async Task<LiveInterruptionHandlingOutcome> HandleAskUserToClarifyInterruptionAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        CancellationToken cancellationToken)
    {
        if (IsLikelyShortFragment(candidate.Transcript))
        {
            return await HandleTerminalFallbackAsync(
                candidate,
                utterance,
                decision,
                "AskClarification live utterance looked like a short fragment; resumed held playback instead of asking a dead-end clarification.",
                cancellationToken);
        }

        var disabledReason = GetSequentialRecompositionDisabledReason();
        if (_options.EnablePendingInterruptionClarification && _pendingClarifications is not null)
        {
            if (string.IsNullOrWhiteSpace(candidate.ActiveTurnId) || string.IsNullOrWhiteSpace(candidate.CorrelationId))
            {
                return await HandleTerminalFallbackAsync(
                    candidate,
                    utterance,
                    decision,
                    $"AskClarification pending owner requires turn and correlation ids ({disabledReason}); resumed held playback instead of deferring.",
                    cancellationToken);
            }

            return await HandlePendingAskClarificationOwnerAsync(
                candidate,
                utterance,
                decision,
                disabledReason ?? "pending clarification response will own recomposition",
                cancellationToken);
        }

        if (disabledReason is null)
        {
            _logger.LogInformation(
                "ask_clarification_live_mapped_to_sequential_recomposition TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. TranscriptLength: {TranscriptLength}.",
                candidate.ActiveTurnId,
                candidate.CorrelationId,
                decision.Type,
                decision.Strategy,
                candidate.Transcript?.Length ?? 0);
            return await HandleSequentialClarificationRecompositionAsync(candidate, utterance, decision, cancellationToken);
        }

        return await HandleTerminalFallbackAsync(
            candidate,
            utterance,
            decision,
            $"AskClarification had no executable live clarification owner ({disabledReason}); resumed held playback instead of deferring.",
            cancellationToken);
    }

    private async Task<LiveInterruptionHandlingOutcome> HandlePendingAskClarificationOwnerAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        string disabledReason,
        CancellationToken cancellationToken)
    {
        var checkpoint = TryCreateLiveCheckpoint(utterance);
        if (!HasMinimumCheckpointInputs(checkpoint))
        {
            return await HandleTerminalFallbackAsync(
                candidate,
                utterance,
                decision,
                "AskClarification pending owner requires a checkpoint with the original question; resumed held playback instead of creating an incomplete owner.",
                cancellationToken);
        }

        var pending = _pendingClarifications!.CreatePending(
            new PendingInterruptionClarificationCreateRequest
            {
                ActiveTurnId = candidate.ActiveTurnId,
                CorrelationId = candidate.CorrelationId,
                CaptureId = utterance.CaptureId,
                OriginalTranscript = candidate.Transcript ?? string.Empty,
                NormalizedTranscript = ConversationalInterruptionTextNormalizer.Normalize(candidate.Transcript),
                RouteKind = utterance.RouteKind,
                RouteAction = utterance.RouteAction,
                Layer1Decision = utterance.Layer1Decision,
                ProvisionalAudioHoldId = utterance.ProvisionalAudioHoldId,
                WasHeldByProvisionalAudioHold = utterance.WasHeldByProvisionalAudioHold,
                OriginalUserQuestion = checkpoint!.OriginalUserQuestion,
                SafeSpokenPrefix = checkpoint.SafeSpokenPrefix,
                LastCompletedSentence = checkpoint.LastCompletedSentence,
                DiscardedPartialSentence = checkpoint.DiscardedPartialSentence,
                CurrentTopicLabel = checkpoint.CurrentTopicLabel,
                OriginalPlanOrIntent = checkpoint.OriginalPlanOrIntent
            });

        await ResolveProvisionalAudioHoldAsync(
            candidate,
            utterance,
            decision,
            ProvisionalAudioHoldResolutionAction.Resume,
            "pending_interruption_clarification_created",
            cancellationToken);
        await EmitAwaitingClarificationAsync(candidate, pending, cancellationToken);

        var reason = $"AskClarification pending owner created; full recomposition is still disabled ({disabledReason}).";
        _logger.LogInformation(
            "ask_clarification_pending_owner_created TurnId: {TurnId}. CorrelationId: {CorrelationId}. ClarificationId: {ClarificationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            pending.ClarificationId,
            decision.Type,
            decision.Strategy,
            true,
            false,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = true,
            AllowLegacyCleanup = true,
            AllowLegacySemanticRouting = false,
            ShouldResumeOrContinuePlaybackIfPossible = true,
            PendingClarificationId = pending.ClarificationId,
            Result = Result(InterruptionHandlingResultType.AskedUserToClarify, decision, reason),
            InterruptionType = decision.Type,
            Strategy = decision.Strategy,
            Reason = reason
        };
    }

    private Task EmitAwaitingClarificationAsync(
        ConversationalInterruptionCandidate candidate,
        PendingInterruptionClarification pending,
        CancellationToken cancellationToken)
    {
        if (_assistantUiStateBroadcaster is null)
        {
            return Task.CompletedTask;
        }

        return _assistantUiStateBroadcaster.EmitImmediateAsync(
            AssistantUiStateEvent.Create(
                "listening",
                "pending_interruption_clarification_awaiting_response",
                pending.CorrelationId,
                pending.ActiveTurnId,
                speechItemType: "interruption_clarification",
                audiblePlaybackActive: false,
                interruptionState: AssistantUiStateEvent.InterruptionStateAwaitingClarification,
                timestampUtc: candidate.EndedAtUtc == default
                    ? DateTimeOffset.UtcNow
                    : candidate.EndedAtUtc),
            nameof(LiveInterruptionIntegrationService),
            cancellationToken);
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleTerminalFallbackAsync(
        ConversationalInterruptionCandidate candidate,
        YieldedInterruptionUtterance utterance,
        ConversationalInterruptionDecision decision,
        string reason,
        CancellationToken cancellationToken)
    {
        await ResolveProvisionalAudioHoldAsync(
            candidate,
            utterance,
            decision,
            ProvisionalAudioHoldResolutionAction.Resume,
            "conversational_interruption_terminal_fallback",
            cancellationToken);

        _logger.LogInformation(
            "conversational_interruption_terminal_fallback_resolved TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. ShouldResume: {ShouldResume}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            true,
            false,
            true,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasEvaluatedByConversationalInterruption = true,
            WasHandledByConversationalInterruption = true,
            AllowLegacyCleanup = true,
            AllowLegacySemanticRouting = false,
            ShouldResumeOrContinuePlaybackIfPossible = true,
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
            return await HandleTerminalFallbackAsync(
                candidate,
                utterance,
                decision,
                $"{disabledReason} Resumed held playback instead of deferring.",
                cancellationToken);
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

    private async Task ResolvePendingProvisionalHoldAsync(
        PendingInterruptionClarification pending,
        ConversationalInterruptionDecision decision,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pending.ProvisionalAudioHoldId))
        {
            return;
        }

        var result = await _playbackPort.FlushProvisionalAudioHoldAsync(
            pending.ProvisionalAudioHoldId,
            "pending_interruption_clarification_recomposition",
            cancellationToken);
        _logger.LogInformation(
            "pending_interruption_clarification_hold_flush_attempted ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. HoldId: {HoldId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Success: {Success}. FailureReason: {FailureReason}.",
            pending.ClarificationId,
            pending.ActiveTurnId,
            pending.CorrelationId,
            result.HoldId ?? pending.ProvisionalAudioHoldId,
            decision.Type,
            decision.Strategy,
            result.Success,
            result.FailureReason);
    }

    private async Task<LiveInterruptionHandlingOutcome> HandlePendingClarificationFailedAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        string reason,
        CancellationToken cancellationToken,
        bool playbackCancelled = false)
    {
        await EmitPendingClarificationResolvedAsync(
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            "pending_interruption_clarification_recomposition_failed",
            "idle",
            "none",
            audiblePlaybackActive: false,
            cancellationToken);
        return FailedHandledOutcome(candidate, decision, reason, playbackCancelled);
    }

    private Task EmitPendingClarificationResolvedAsync(
        PendingInterruptionClarification pending,
        string reason,
        string baseState,
        string speechItemType,
        bool audiblePlaybackActive,
        CancellationToken cancellationToken) =>
        EmitPendingClarificationResolvedAsync(
            pending.ActiveTurnId,
            pending.CorrelationId,
            reason,
            baseState,
            speechItemType,
            audiblePlaybackActive,
            cancellationToken);

    private Task EmitPendingClarificationResolvedAsync(
        string turnId,
        string correlationId,
        string reason,
        string baseState,
        string speechItemType,
        bool audiblePlaybackActive,
        CancellationToken cancellationToken)
    {
        if (_assistantUiStateBroadcaster is null)
        {
            return Task.CompletedTask;
        }

        return _assistantUiStateBroadcaster.EmitImmediateAsync(
            AssistantUiStateEvent.Create(
                baseState,
                reason,
                correlationId,
                turnId,
                speechItemType: speechItemType,
                audiblePlaybackActive: audiblePlaybackActive,
                interruptionState: AssistantUiStateEvent.InterruptionStateNone),
            nameof(LiveInterruptionIntegrationService),
            cancellationToken);
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

    private static bool IsLikelyShortFragment(string? transcript)
    {
        var normalized = NormalizeForFragmentDetection(transcript);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (HasDirectedInterruptionMarker(normalized))
        {
            return false;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 4;
    }

    private static bool HasDirectedInterruptionMarker(string normalized)
    {
        return normalized.StartsWith("no ", StringComparison.Ordinal)
            || normalized.StartsWith("wait", StringComparison.Ordinal)
            || normalized.StartsWith("actually", StringComparison.Ordinal)
            || normalized.StartsWith("hang on", StringComparison.Ordinal)
            || normalized.StartsWith("hold on", StringComparison.Ordinal)
            || normalized.StartsWith("stop", StringComparison.Ordinal)
            || normalized.StartsWith("i mean ", StringComparison.Ordinal)
            || normalized.StartsWith("i meant ", StringComparison.Ordinal)
            || normalized.Contains(" i mean ", StringComparison.Ordinal)
            || normalized.Contains(" i meant ", StringComparison.Ordinal)
            || normalized.Contains(" what i mean", StringComparison.Ordinal);
    }

    private static string NormalizeForFragmentDetection(string? transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return string.Empty;
        }

        var chars = transcript.Trim().ToLowerInvariant().Select(character =>
            char.IsLetterOrDigit(character) || char.IsWhiteSpace(character)
                ? character
                : ' ');
        return string.Join(' ', new string(chars.ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
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

    private static SpokenAnswerCheckpoint? CreateCheckpointFromPending(PendingInterruptionClarification pending)
    {
        if (string.IsNullOrWhiteSpace(pending.OriginalUserQuestion))
        {
            return null;
        }

        return new SpokenAnswerCheckpoint
        {
            TurnId = pending.ActiveTurnId,
            CorrelationId = pending.CorrelationId,
            OriginalUserQuestion = pending.OriginalUserQuestion,
            SafeSpokenPrefix = pending.SafeSpokenPrefix ?? string.Empty,
            LastCompletedSentence = pending.LastCompletedSentence ?? string.Empty,
            DiscardedPartialSentence = pending.DiscardedPartialSentence ?? string.Empty,
            CurrentTopicLabel = pending.CurrentTopicLabel,
            OriginalPlanOrIntent = pending.OriginalPlanOrIntent
        };
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

    private static ConversationalInterruptionDecision PendingClarificationDecision() => new()
    {
        Type = ConversationalInterruptionType.RelatedFollowUpQuestion,
        Strategy = ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
        RequiresDeepInfraClarification = false,
        RequiresContinuationRecomposition = true,
        DiscardCurrentPartialSentence = true,
        Reason = "Pending interruption clarification response captured."
    };

    private static string BuildResolvedPendingInterruptionText(
        PendingInterruptionClarification pending,
        PendingInterruptionClarificationResponse response)
    {
        if (string.IsNullOrWhiteSpace(pending.OriginalTranscript))
        {
            return response.ResponseText;
        }

        return $"{pending.OriginalTranscript} Clarification answer: {response.ResponseText}";
    }

    private static string BuildPendingClarificationContext(
        PendingInterruptionClarification pending,
        PendingInterruptionClarificationResponse response)
    {
        if (string.IsNullOrWhiteSpace(pending.OriginalTranscript))
        {
            return $"The user clarified: {response.ResponseText}";
        }

        return $"The unclear interruption was '{pending.OriginalTranscript}'. The user clarified: {response.ResponseText}";
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
