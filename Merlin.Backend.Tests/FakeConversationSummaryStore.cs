using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class FakeConversationSummaryStore : IConversationSummaryStore
{
    private readonly List<ConversationSummary> _summaries = [];

    public bool IsHealthy { get; set; } = true;

    public IReadOnlyCollection<ConversationSummary> GetAll()
    {
        return _summaries.ToArray();
    }

    public ConversationSummary SaveSummary(ConversationSummary summary)
    {
        var saved = new ConversationSummary
        {
            SummaryId = string.IsNullOrWhiteSpace(summary.SummaryId)
                ? Guid.NewGuid().ToString("N")
                : summary.SummaryId,
            CreatedAtUtc = summary.CreatedAtUtc == default ? DateTimeOffset.UtcNow : summary.CreatedAtUtc,
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            Title = summary.Title,
            SummaryText = summary.SummaryText,
            Tags = summary.Tags.ToArray(),
            MessageCount = summary.MessageCount
        };

        _summaries.RemoveAll(item => item.SummaryId == saved.SummaryId);
        _summaries.Add(saved);
        return saved;
    }

    public IReadOnlyList<ConversationSummary> GetRecentSummaries(int count)
    {
        return _summaries
            .OrderByDescending(summary => summary.LastUpdatedUtc)
            .Take(count)
            .ToArray();
    }

    public IReadOnlyList<ConversationSummary> SearchSummaries(string query)
    {
        return _summaries
            .Where(summary =>
                summary.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || summary.SummaryText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || summary.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }
}
