namespace Merlin.Backend.Services.Feedback;

public interface IFeedbackVectorBuilder
{
    IReadOnlyDictionary<string, double> Build(FeedbackContext context);
}
