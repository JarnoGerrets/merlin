namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class ConversationEntity
{
    public string Id { get; set; } = default!;
    public string? Title { get; set; }
    public string? ActiveTopic { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public List<ConversationTopicEntity> Topics { get; set; } = [];
    public List<AssistantTurnEntity> AssistantTurns { get; set; } = [];
    public List<PromptCompilationEntity> PromptCompilations { get; set; } = [];
}
