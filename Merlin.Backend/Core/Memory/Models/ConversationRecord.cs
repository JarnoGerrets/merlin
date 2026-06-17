namespace Merlin.Backend.Core.Memory.Models;

public sealed record ConversationRecord
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? ActiveTopic { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
}
