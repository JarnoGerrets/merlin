namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class MemoryEntity
{
    public string Id { get; set; } = default!;
    public string MemoryType { get; set; } = default!;
    public string Status { get; set; } = "active";
    public string? Title { get; set; }
    public string Content { get; set; } = default!;
    public string? CompactContent { get; set; }
    public string? Summary { get; set; }
    public string? TagsJson { get; set; }
    public string? MemoryAnchorsJson { get; set; }
    public string? Project { get; set; }
    public string? Topic { get; set; }
    public double Importance { get; set; } = 0.5;
    public double Confidence { get; set; } = 0.8;
    public bool UserConfirmed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Source { get; set; }
    public string? SourceConversationId { get; set; }
    public string? SourceTurnId { get; set; }
    public string? MergedIntoMemoryId { get; set; }
    public string? SupersedesMemoryId { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public List<MemoryConceptEntity> MemoryConcepts { get; set; } = [];
}
