namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class AssistantTurnEntity
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;
    public string? TopicId { get; set; }
    public string OriginalUserMessage { get; set; } = default!;
    public string? GeneratedTextSoFar { get; set; }
    public string? SpokenTextSoFar { get; set; }
    public string State { get; set; } = default!;
    public string? InterruptionReason { get; set; }
    public string? InterruptedByUserMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ConversationEntity Conversation { get; set; } = default!;
    public ConversationTopicEntity? Topic { get; set; }
    public List<PromptCompilationEntity> PromptCompilations { get; set; } = [];
}
