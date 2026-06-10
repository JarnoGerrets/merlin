using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IConversationSummaryStore
{
    bool IsHealthy { get; }

    IReadOnlyCollection<ConversationSummary> GetAll();

    ConversationSummary SaveSummary(ConversationSummary summary);

    IReadOnlyList<ConversationSummary> GetRecentSummaries(int count);

    IReadOnlyList<ConversationSummary> SearchSummaries(string query);
}
