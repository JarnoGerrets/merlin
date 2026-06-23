namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class NoOpInterruptionFeedbackPort : IInterruptionFeedbackPort
{
    public Task SuppressNormalProgressAsync(string turnId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RequestBridgeFeedbackAsync(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision,
        ConversationFocusAction focusAction,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
