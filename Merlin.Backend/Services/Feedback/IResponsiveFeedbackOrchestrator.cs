using Merlin.Backend.Models;
using Merlin.Backend.Services.Acknowledgement;

namespace Merlin.Backend.Services.Feedback;

public interface IResponsiveFeedbackOrchestrator
{
    Task<FeedbackEmissionResult> TryEmitImmediateFeedbackAsync(
        FeedbackContext context,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        CancellationToken cancellationToken);

    Task<FeedbackEmissionResult> TryEmitInterruptionBridgeAsync(
        FeedbackContext context,
        CancellationToken cancellationToken);

    IRequestProgressSpeechHandle? StartProgressFeedback(
        FeedbackContext context,
        RequestProgressSpeechRequest request,
        CancellationToken cancellationToken);

    void MarkMainResponseReady(string correlationId);

    bool WasImmediateFeedbackEmitted(string correlationId);

    void SuppressNormalProgressForTurn(string turnId, string reason);
}
