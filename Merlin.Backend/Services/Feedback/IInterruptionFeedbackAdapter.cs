namespace Merlin.Backend.Services.Feedback;

public interface IInterruptionFeedbackAdapter
{
    FeedbackContext CreateBridgeContext(InterruptionFeedbackRequest request);
}
