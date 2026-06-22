using Merlin.Backend.Models;
using Merlin.Backend.Tools;

namespace Merlin.Backend.Services.Feedback;

public interface IFeedbackContextFactory
{
    FeedbackContext CreateInitial(
        AssistantRequest request,
        string correlationId,
        string normalizedUserText,
        DateTimeOffset createdAtUtc);

    FeedbackContext EnrichWithRouting(
        FeedbackContext context,
        IntentParseResult intentResult,
        ITool? tool,
        FeedbackPhase phase = FeedbackPhase.Executing);
}
