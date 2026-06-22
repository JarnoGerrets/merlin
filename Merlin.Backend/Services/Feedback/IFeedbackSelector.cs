namespace Merlin.Backend.Services.Feedback;

public interface IFeedbackSelector
{
    FeedbackSelection? Select(FeedbackContext context);
}
