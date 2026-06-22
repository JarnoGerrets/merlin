namespace Merlin.Backend.Services.Feedback;

public interface IFeedbackCooldownTracker
{
    bool IsAllowed(FeedbackCard card, FeedbackContext context, DateTimeOffset now);

    void MarkUsed(FeedbackCard card, FeedbackContext context, DateTimeOffset now);
}
