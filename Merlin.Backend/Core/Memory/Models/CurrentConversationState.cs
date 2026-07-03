namespace Merlin.Backend.Core.Memory.Models;

public sealed record CurrentConversationState
{
    public required string ConversationId { get; init; }
    public string? ActiveTopicId { get; init; }
    public string? ActiveTopicTitle { get; init; }
    public DateTimeOffset? ActiveTopicUpdatedAt { get; init; }
    public bool ActiveTopicTouchedInCurrentProcess { get; init; }
    public string? CurrentGoal { get; init; }
    public string? RecentSummary { get; init; }
    public IReadOnlyList<string> ActiveConcepts { get; init; } = [];
    public IReadOnlyList<string> ActiveEntities { get; init; } = [];
    public IReadOnlyList<string> UnresolvedQuestions { get; init; } = [];
    public DateTimeOffset LastUpdatedUtc { get; init; }
}
