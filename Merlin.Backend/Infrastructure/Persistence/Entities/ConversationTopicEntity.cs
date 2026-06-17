namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class ConversationTopicEntity
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Summary { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public ConversationEntity Conversation { get; set; } = default!;
    public List<AssistantTurnEntity> AssistantTurns { get; set; } = [];
}
