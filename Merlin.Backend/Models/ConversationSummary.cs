namespace Merlin.Backend.Models;

public sealed class ConversationSummary
{
    public string SummaryId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; }

    public string Title { get; init; } = string.Empty;

    public string SummaryText { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Tags { get; init; } = [];

    public int MessageCount { get; init; }
}
