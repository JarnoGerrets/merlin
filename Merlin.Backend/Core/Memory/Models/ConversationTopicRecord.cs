namespace Merlin.Backend.Core.Memory.Models;

public sealed record ConversationTopicRecord
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
}
