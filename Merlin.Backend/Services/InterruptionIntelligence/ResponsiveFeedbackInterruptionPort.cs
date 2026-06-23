using Merlin.Backend.Configuration;
using Merlin.Backend.Services.Feedback;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ResponsiveFeedbackInterruptionPort : IInterruptionFeedbackPort
{
    private readonly IResponsiveFeedbackOrchestrator _feedbackOrchestrator;
    private readonly IInterruptionFeedbackAdapter _feedbackAdapter;
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<ResponsiveFeedbackInterruptionPort> _logger;

    public ResponsiveFeedbackInterruptionPort(
        IResponsiveFeedbackOrchestrator feedbackOrchestrator,
        IInterruptionFeedbackAdapter feedbackAdapter,
        IOptions<InterruptionHandlingOptions> options,
        ILogger<ResponsiveFeedbackInterruptionPort> logger)
    {
        _feedbackOrchestrator = feedbackOrchestrator;
        _feedbackAdapter = feedbackAdapter;
        _options = options.Value;
        _logger = logger;
    }

    public Task SuppressNormalProgressAsync(string turnId, CancellationToken cancellationToken = default)
    {
        if (!CanExecuteFeedbackAction("suppress_progress", turnId))
        {
            return Task.CompletedTask;
        }

        _feedbackOrchestrator.SuppressNormalProgressForTurn(turnId, "conversational_interruption");
        return Task.CompletedTask;
    }

    public async Task RequestBridgeFeedbackAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(focusAction);

        if (!CanExecuteFeedbackAction("bridge_feedback", focusAction.ActiveTurnId))
        {
            return;
        }

        var request = new InterruptionFeedbackRequest
        {
            CorrelationId = candidate.CorrelationId,
            TurnId = focusAction.ActiveTurnId,
            InterruptionType = decision.Type.ToString(),
            Strategy = decision.Strategy.ToString(),
            RequiresBridgeFeedback = focusAction.RequiresBridgeFeedback,
            IsRecompositionFeedback = focusAction.RequiresRecomposition,
            IsWaitBridge = focusAction.RequiresClarification,
            IsQueueFollowUp = focusAction.Type is ConversationFocusActionType.QueueFollowUpAfterCurrent,
            IsRedirectOrCorrection = focusAction.Type is ConversationFocusActionType.CancelAndReplaceMainTurn,
            IsUnclear = focusAction.RequiresClarification
        };
        var feedbackContext = _feedbackAdapter.CreateBridgeContext(request);
        await _feedbackOrchestrator.TryEmitInterruptionBridgeAsync(feedbackContext, cancellationToken);
    }

    private bool CanExecuteFeedbackAction(string action, string turnId)
    {
        if (!_options.Enabled || _options.EnableLiveShadowMode || !_options.EnableLiveResponsiveFeedbackBridge)
        {
            _logger.LogInformation(
                "interruption_feedback_action_suppressed Action: {Action}. TurnId: {TurnId}. ShadowMode: {ShadowMode}. FeedbackBridgeEnabled: {FeedbackBridgeEnabled}.",
                action,
                turnId,
                _options.EnableLiveShadowMode,
                _options.EnableLiveResponsiveFeedbackBridge);
            return false;
        }

        return true;
    }
}
