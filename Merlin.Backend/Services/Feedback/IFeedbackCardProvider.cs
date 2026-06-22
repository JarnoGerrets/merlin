namespace Merlin.Backend.Services.Feedback;

public interface IFeedbackCardProvider
{
    IReadOnlyList<FeedbackCard> GetCards();
}
