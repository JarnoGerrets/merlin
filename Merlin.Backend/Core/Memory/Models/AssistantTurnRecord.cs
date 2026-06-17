namespace Merlin.Backend.Core.Memory.Models;

public sealed record AssistantTurnRecord
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public string? TopicId { get; init; }
    public required string OriginalUserMessage { get; init; }
    public string? GeneratedTextSoFar { get; init; }
    public string? SpokenTextSoFar { get; init; }
    public required string State { get; init; }
    public string? InterruptionReason { get; init; }
    public string? InterruptedByUserMessage { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
