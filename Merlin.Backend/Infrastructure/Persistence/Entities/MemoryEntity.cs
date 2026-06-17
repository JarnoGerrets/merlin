namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class MemoryEntity
{
    public string Id { get; set; } = default!;
    public string MemoryType { get; set; } = default!;
    public string? Title { get; set; }
    public string Content { get; set; } = default!;
    public string? Summary { get; set; }
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
    public List<MemoryConceptEntity> MemoryConcepts { get; set; } = [];
}
