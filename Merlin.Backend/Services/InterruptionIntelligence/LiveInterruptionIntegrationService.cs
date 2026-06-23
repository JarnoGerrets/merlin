using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class LiveInterruptionIntegrationService : ILiveInterruptionIntegrationService
{
    private readonly IConversationalInterruptionCandidateFactory _candidateFactory;
    private readonly IConversationalInterruptionClassifier _classifier;
    private readonly IInterruptionOrchestrator _orchestrator;
    private readonly IInterruptionPlaybackPort _playbackPort;
    private readonly IInterruptionFeedbackPort _feedbackPort;
    private readonly IInterruptionRequestRouterPort _requestRouterPort;
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
        ILogger<LiveInterruptionIntegrationService> logger)
    {
        _candidateFactory = candidateFactory;
        _classifier = classifier;
        _orchestrator = orchestrator;
        _playbackPort = playbackPort;
        _feedbackPort = feedbackPort;
        _requestRouterPort = requestRouterPort;
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
            return null;
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

        var candidate = _candidateFactory.CreateFromYieldedInterruption(utterance);
        _logger.LogInformation(
            "conversational_interruption_yielded_utterance_observed TurnId: {TurnId}. CorrelationId: {CorrelationId}. TranscriptLength: {TranscriptLength}. YieldReason: {YieldReason}. CaptureKind: {CaptureKind}. RouteKind: {RouteKind}. Layer1Confidence: {Layer1Confidence}. Layer1Decision: {Layer1Decision}. ShadowMode: {ShadowMode}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
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
                WasHandled = false,
                ShouldContinueOldPath = true,
                Result = new InterruptionHandlingResult
                {
                    Type = InterruptionHandlingResultType.Ignored,
                    Decision = shadowDecision,
                    Reason = "Yielded utterance shadow mode classified conversational meaning without orchestration side effects."
                },
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
                HandleContinue(candidate, decision, InterruptionHandlingResultType.Ignored, "Yielded utterance is conversationally useless."),
            ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse =>
                HandleContinue(candidate, decision, InterruptionHandlingResultType.Continued, "Yielded utterance is a backchannel/passive agreement."),
            ConversationalInterruptionHandlingStrategy.StopPlayback =>
                await HandleStopAsync(candidate, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.CancelAndRedirect =>
                await HandleCorrectionRedirectAsync(candidate, decision, cancellationToken),
            ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption =>
                DeferToOldPath(candidate, decision, "Ask-user-to-clarify is not executed live in PR7."),
            ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint
                or ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint
                or ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent =>
                DeferToOldPath(candidate, decision, $"Strategy {decision.Strategy} is deferred to PR8/PR9."),
            _ => DeferToOldPath(candidate, decision, $"Strategy {decision.Strategy} is not supported by PR7.")
        };
    }

    private LiveInterruptionHandlingOutcome HandleContinue(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        InterruptionHandlingResultType resultType,
        string reason)
    {
        _logger.LogInformation(
            "conversational_interruption_live_handled TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. ShouldContinueOldPath: {ShouldContinueOldPath}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            true,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasHandled = true,
            ShouldContinueOldPath = true,
            Result = Result(resultType, decision, reason),
            Reason = reason
        };
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleStopAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        CancellationToken cancellationToken)
    {
        if (!_options.EnableLivePlaybackActions)
        {
            return DeferToOldPath(candidate, decision, "Live playback actions are disabled.");
        }

        if (decision.Type is ConversationalInterruptionType.CancelRequest)
        {
            await _playbackPort.CancelCurrentAsync(candidate.ActiveTurnId, decision.Reason, cancellationToken);
        }
        else
        {
            await _playbackPort.StopCurrentAsync(candidate.ActiveTurnId, decision.Reason, cancellationToken);
        }

        _logger.LogInformation(
            "conversational_interruption_live_stop_executed TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy);

        return new LiveInterruptionHandlingOutcome
        {
            WasHandled = true,
            ShouldContinueOldPath = false,
            ShouldCancelActiveTurn = true,
            Result = Result(
                InterruptionHandlingResultType.Stopped,
                decision,
                "Stop/cancel handled by PR7 live minimal behavior.",
                playbackCancelled: true,
                originalTurnCancelled: true),
            Reason = "Stop/cancel handled by PR7 live minimal behavior."
        };
    }

    private async Task<LiveInterruptionHandlingOutcome> HandleCorrectionRedirectAsync(
        ConversationalInterruptionCandidate candidate,
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

        await _requestRouterPort.RouteRedirectedRequestAsync(
            decision.RewrittenUserRequest,
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            cancellationToken);

        _logger.LogInformation(
            "conversational_interruption_live_correction_redirect_executed TurnId: {TurnId}. CorrelationId: {CorrelationId}. RewrittenLength: {RewrittenLength}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.RewrittenUserRequest.Length);

        return new LiveInterruptionHandlingOutcome
        {
            WasHandled = true,
            ShouldContinueOldPath = false,
            ShouldCancelActiveTurn = true,
            IsCorrectionRedirect = true,
            RedirectedRequest = decision.RewrittenUserRequest,
            Result = Result(
                InterruptionHandlingResultType.CancelledAndRedirected,
                decision,
                "Correction/redirect handled by PR7 live minimal behavior.",
                redirectedRequest: decision.RewrittenUserRequest,
                playbackCancelled: true,
                originalTurnCancelled: true,
                bridgeFeedbackRequested: decision.RequiresBridgeFeedback && _options.EnableLiveResponsiveFeedbackBridge,
                normalProgressSuppressed: _options.EnableLiveResponsiveFeedbackBridge),
            Reason = "Correction/redirect handled by PR7 live minimal behavior."
        };
    }

    private LiveInterruptionHandlingOutcome DeferToOldPath(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        string reason)
    {
        _logger.LogInformation(
            "conversational_interruption_live_deferred_to_old_path TurnId: {TurnId}. CorrelationId: {CorrelationId}. DecisionType: {DecisionType}. Strategy: {Strategy}. Reason: {Reason}.",
            candidate.ActiveTurnId,
            candidate.CorrelationId,
            decision.Type,
            decision.Strategy,
            reason);

        return new LiveInterruptionHandlingOutcome
        {
            WasHandled = false,
            ShouldContinueOldPath = true,
            Result = Result(InterruptionHandlingResultType.Ignored, decision, reason),
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
        bool normalProgressSuppressed = false) =>
        new()
        {
            Type = type,
            Decision = decision,
            RedirectedRequest = redirectedRequest,
            PlaybackCancelled = playbackCancelled,
            OriginalTurnCancelled = originalTurnCancelled,
            BridgeFeedbackRequested = bridgeFeedbackRequested,
            NormalProgressSuppressed = normalProgressSuppressed,
            Reason = reason
        };
}
