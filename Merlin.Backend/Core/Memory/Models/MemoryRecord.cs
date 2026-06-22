namespace Merlin.Backend.Core.Memory.Models;

public sealed record MemoryRecord
{
    public required string Id { get; init; }
    public required string MemoryType { get; init; }
    public string Status { get; init; } = MemoryStatuses.Active;
    public string? Title { get; init; }
    public required string Content { get; init; }
    public string? CompactContent { get; init; }
    public string? Summary { get; init; }
    public string? TagsJson { get; init; }
    public string? MemoryAnchorsJson { get; init; }
    public string? Project { get; init; }
    public string? Topic { get; init; }
    public double Importance { get; init; } = 0.5;
    public double Confidence { get; init; } = 0.8;
    public bool UserConfirmed { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? LastAccessedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? Source { get; init; }
    public string? SourceConversationId { get; init; }
    public string? SourceTurnId { get; init; }
    public string? MergedIntoMemoryId { get; init; }
    public string? SupersedesMemoryId { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
}

public static class MemoryStatuses
{
    public const string Active = "active";
    public const string Merged = "merged";
    public const string Superseded = "superseded";
    public const string Archived = "archived";
    public const string Deleted = "deleted";
}
