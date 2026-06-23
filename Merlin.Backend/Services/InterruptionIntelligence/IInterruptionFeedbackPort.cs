namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IInterruptionFeedbackPort
{
    Task SuppressNormalProgressAsync(
        string turnId,
        CancellationToken cancellationToken = default);

    Task RequestBridgeFeedbackAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken = default);
}
