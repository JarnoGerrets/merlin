using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class InterruptionOrchestrator : IInterruptionOrchestrator
{
    private readonly IConversationalInterruptionClassifier _classifier;
    private readonly IConversationFocusManager _focusManager;
    private readonly ISpokenAnswerTracker _spokenAnswerTracker;
    private readonly IAnswerRecomposer _answerRecomposer;
    private readonly IInterruptionPlaybackPort _playbackPort;
    private readonly IInterruptionFeedbackPort _feedbackPort;
    private readonly IInterruptionRequestRouterPort _requestRouterPort;
    private readonly IInterruptionModelPort _modelPort;
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<InterruptionOrchestrator> _logger;

    public InterruptionOrchestrator(
        IConversationalInterruptionClassifier classifier,
        IConversationFocusManager focusManager,
        ISpokenAnswerTracker spokenAnswerTracker,
        IAnswerRecomposer answerRecomposer,
        IInterruptionPlaybackPort playbackPort,
        IInterruptionFeedbackPort feedbackPort,
        IInterruptionRequestRouterPort requestRouterPort,
        IInterruptionModelPort modelPort,
        IOptions<InterruptionHandlingOptions> options,
        ILogger<InterruptionOrchestrator> logger)
    {
        _classifier = classifier;
        _focusManager = focusManager;
        _spokenAnswerTracker = spokenAnswerTracker;
        _answerRecomposer = answerRecomposer;
        _playbackPort = playbackPort;
        _feedbackPort = feedbackPort;
        _requestRouterPort = requestRouterPort;
        _modelPort = modelPort;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<InterruptionHandlingResult> HandleInterruptionAsync(
        ConversationalInterruptionCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!_options.Enabled)
        {
            _logger.LogInformation("interruption_orchestrator_disabled");
            return new InterruptionHandlingResult
            {
                Type = InterruptionHandlingResultType.Ignored,
                Reason = "Interruption handling is disabled."
            };
        }

        try
        {
            _logger.LogInformation("interruption_orchestrator_candidate_received");
            var decision = _classifier.Classify(candidate);
            _logger.LogInformation(
                "interruption_orchestrator_decision Type: {Type}. Strategy: {Strategy}.",
                decision.Type,
                decision.Strategy);
            var focusAction = _focusManager.ApplyInterruptionDecision(candidate, decision);
            _logger.LogInformation(
                "interruption_orchestrator_focus_action Type: {Type}.",
                focusAction.Type);

            return focusAction.Type switch
            {
                ConversationFocusActionType.IgnoreAndContinue => Result(
                    InterruptionHandlingResultType.Ignored,
                    decision,
                    focusAction,
                    "Interruption ignored."),
                ConversationFocusActionType.ContinueMainAnswer => Result(
                    InterruptionHandlingResultType.Continued,
                    decision,
                    focusAction,
                    "Main answer continued."),
                ConversationFocusActionType.StopCurrentTurn => await HandleStopAsync(
                    decision,
                    focusAction,
                    cancellationToken),
                ConversationFocusActionType.CancelAndReplaceMainTurn => await HandleCancelAndRedirectAsync(
                    candidate,
                    decision,
                    focusAction,
                    cancellationToken),
                ConversationFocusActionType.ClarifyThenRecomposeMainAnswer => await HandleClarificationAndRecompositionAsync(
                    candidate,
                    decision,
                    focusAction,
                    cancellationToken),
                ConversationFocusActionType.RecomposeMainAnswer => await HandleRecompositionAsync(
                    candidate,
                    decision,
                    focusAction,
                    cancellationToken),
                ConversationFocusActionType.QueueFollowUpAfterCurrent => await HandleQueueFollowUpAsync(
                    candidate,
                    decision,
                    focusAction,
                    cancellationToken),
                _ => await HandleAskUserToClarifyAsync(
                    candidate,
                    decision,
                    focusAction,
                    cancellationToken)
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "interruption_orchestrator_failed");
            return new InterruptionHandlingResult
            {
                Type = InterruptionHandlingResultType.Failed,
                Reason = $"Interruption orchestration failed: {exception.Message}"
            };
        }
    }

    private async Task<InterruptionHandlingResult> HandleStopAsync(
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        await _playbackPort.StopCurrentAsync(focusAction.ActiveTurnId, focusAction.Reason, cancellationToken);
        _logger.LogInformation("interruption_orchestrator_playback_cancel_requested");
        return Result(
            InterruptionHandlingResultType.Stopped,
            decision,
            focusAction,
            "Current turn stopped.",
            playbackCancelled: true,
            originalTurnCancelled: true);
    }

    private async Task<InterruptionHandlingResult> HandleCancelAndRedirectAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        var progressSuppressed = await SuppressProgressIfNeededAsync(focusAction, cancellationToken);
        await _playbackPort.CancelCurrentAsync(focusAction.ActiveTurnId, focusAction.Reason, cancellationToken);
        _logger.LogInformation("interruption_orchestrator_playback_cancel_requested");
        var bridgeRequested = await RequestBridgeIfNeededAsync(candidate, decision, focusAction, cancellationToken);
        await _requestRouterPort.RouteRedirectedRequestAsync(
            focusAction.RewrittenRequest ?? string.Empty,
            focusAction.ActiveTurnId,
            candidate.CorrelationId,
            cancellationToken);

        return Result(
            InterruptionHandlingResultType.CancelledAndRedirected,
            decision,
            focusAction,
            "Redirected interruption routed.",
            redirectedRequest: focusAction.RewrittenRequest,
            playbackCancelled: true,
            originalTurnCancelled: true,
            bridgeFeedbackRequested: bridgeRequested,
            normalProgressSuppressed: progressSuppressed);
    }

    private async Task<InterruptionHandlingResult> HandleAskUserToClarifyAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        var bridgeRequested = await RequestBridgeIfNeededAsync(candidate, decision, focusAction, cancellationToken);
        return Result(
            InterruptionHandlingResultType.AskedUserToClarify,
            decision,
            focusAction,
            focusAction.Reason,
            bridgeFeedbackRequested: bridgeRequested);
    }

    private async Task<InterruptionHandlingResult> HandleClarificationAndRecompositionAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        var progressSuppressed = await SuppressProgressIfNeededAsync(focusAction, cancellationToken);
        await CancelPlaybackForFocusActionAsync(focusAction, cancellationToken);
        var checkpoint = CreateCheckpoint(focusAction);
        var clarificationRequest = BuildClarificationRequest(candidate, checkpoint);
        _ = _answerRecomposer.BuildClarificationPrompt(clarificationRequest);
        _logger.LogInformation("interruption_orchestrator_clarification_requested");
        var clarification = await _modelPort.GenerateClarificationAsync(clarificationRequest, cancellationToken);
        if (!clarification.ShouldRecomposeContinuation)
        {
            return Result(
                InterruptionHandlingResultType.ClarificationPrepared,
                decision,
                focusAction,
                "Clarification prepared without continuation recomposition.",
                checkpoint: checkpoint,
                clarificationRequest: clarificationRequest,
                clarificationResult: clarification,
                playbackCancelled: focusAction.ShouldCancelPlayback,
                normalProgressSuppressed: progressSuppressed);
        }

        var continuationRequest = BuildContinuationRequest(candidate, checkpoint, clarification);
        _ = _answerRecomposer.BuildContinuationRecompositionPrompt(continuationRequest);
        _logger.LogInformation("interruption_orchestrator_continuation_requested");
        var continuation = await _modelPort.GenerateContinuationAsync(continuationRequest, cancellationToken);
        return Result(
            InterruptionHandlingResultType.ClarificationAndRecompositionPrepared,
            decision,
            focusAction,
            "Clarification and continuation recomposition prepared.",
            checkpoint: checkpoint,
            clarificationRequest: clarificationRequest,
            clarificationResult: clarification,
            continuationRequest: continuationRequest,
            continuationResult: continuation,
            playbackCancelled: focusAction.ShouldCancelPlayback,
            normalProgressSuppressed: progressSuppressed);
    }

    private async Task<InterruptionHandlingResult> HandleRecompositionAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        var progressSuppressed = await SuppressProgressIfNeededAsync(focusAction, cancellationToken);
        await CancelPlaybackForFocusActionAsync(focusAction, cancellationToken);
        var bridgeRequested = await RequestBridgeIfNeededAsync(candidate, decision, focusAction, cancellationToken);
        var checkpoint = CreateCheckpoint(focusAction);
        var continuationRequest = BuildContinuationRequest(
            candidate,
            checkpoint,
            new ClarificationResult
            {
                ReplyText = string.Empty,
                ClarificationContext = candidate.Transcript ?? string.Empty
            });
        _ = _answerRecomposer.BuildContinuationRecompositionPrompt(continuationRequest);
        _logger.LogInformation("interruption_orchestrator_continuation_requested");
        var continuation = await _modelPort.GenerateContinuationAsync(continuationRequest, cancellationToken);
        return Result(
            InterruptionHandlingResultType.RecompositionPrepared,
            decision,
            focusAction,
            "Continuation recomposition prepared.",
            checkpoint: checkpoint,
            continuationRequest: continuationRequest,
            continuationResult: continuation,
            playbackCancelled: focusAction.ShouldCancelPlayback,
            bridgeFeedbackRequested: bridgeRequested,
            normalProgressSuppressed: progressSuppressed);
    }

    private async Task<InterruptionHandlingResult> HandleQueueFollowUpAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        var bridgeRequested = await RequestBridgeIfNeededAsync(candidate, decision, focusAction, cancellationToken);
        return Result(
            InterruptionHandlingResultType.FollowUpQueued,
            decision,
            focusAction,
            "Follow-up queued.",
            queuedFollowUpId: focusAction.QueuedFollowUpId,
            bridgeFeedbackRequested: bridgeRequested);
    }

    private SpokenAnswerCheckpoint CreateCheckpoint(ConversationFocusAction focusAction)
    {
        try
        {
            return _spokenAnswerTracker.CreateCheckpoint(
                focusAction.ActiveTurnId,
                focusAction.ShouldDiscardPartialSentence);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Checkpoint creation failed: {exception.Message}", exception);
        }
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

    private async Task CancelPlaybackForFocusActionAsync(
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        if (focusAction.ShouldCancelPlayback)
        {
            await _playbackPort.CancelCurrentAsync(focusAction.ActiveTurnId, focusAction.Reason, cancellationToken);
            _logger.LogInformation("interruption_orchestrator_playback_cancel_requested");
        }
        else if (focusAction.ShouldPausePlayback)
        {
            await _playbackPort.PauseCurrentAsync(focusAction.ActiveTurnId, focusAction.Reason, cancellationToken);
        }
    }

    private async Task<bool> SuppressProgressIfNeededAsync(
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        if (!focusAction.RequiresBridgeFeedback
            && !focusAction.RequiresClarification
            && !focusAction.RequiresRecomposition)
        {
            return false;
        }

        await _feedbackPort.SuppressNormalProgressAsync(focusAction.ActiveTurnId, cancellationToken);
        return true;
    }

    private async Task<bool> RequestBridgeIfNeededAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken)
    {
        if (!focusAction.RequiresBridgeFeedback)
        {
            return false;
        }

        _logger.LogInformation("interruption_orchestrator_bridge_requested");
        await _feedbackPort.RequestBridgeFeedbackAsync(candidate, decision, focusAction, cancellationToken);
        return true;
    }

    private static InterruptionHandlingResult Result(
        InterruptionHandlingResultType type,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        string reason,
        SpokenAnswerCheckpoint? checkpoint = null,
        ClarificationRequest? clarificationRequest = null,
        ClarificationResult? clarificationResult = null,
        ContinuationRecompositionRequest? continuationRequest = null,
        ContinuationRecompositionResult? continuationResult = null,
        string? redirectedRequest = null,
        string? queuedFollowUpId = null,
        bool playbackPaused = false,
        bool playbackCancelled = false,
        bool originalTurnCancelled = false,
        bool bridgeFeedbackRequested = false,
        bool normalProgressSuppressed = false)
    {
        return new InterruptionHandlingResult
        {
            Type = type,
            Decision = decision,
            FocusAction = focusAction,
            Checkpoint = checkpoint,
            ClarificationRequest = clarificationRequest,
            ClarificationResult = clarificationResult,
            ContinuationRequest = continuationRequest,
            ContinuationResult = continuationResult,
            RedirectedRequest = redirectedRequest,
            QueuedFollowUpId = queuedFollowUpId,
            PlaybackPaused = playbackPaused,
            PlaybackCancelled = playbackCancelled,
            OriginalTurnCancelled = originalTurnCancelled,
            BridgeFeedbackRequested = bridgeFeedbackRequested,
            NormalProgressSuppressed = normalProgressSuppressed,
            Reason = reason
        };
    }
}
