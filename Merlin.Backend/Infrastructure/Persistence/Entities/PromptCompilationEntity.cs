namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class PromptCompilationEntity
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;
    public string? TurnId { get; set; }
    public string PromptType { get; set; } = default!;
    public string CompiledPrompt { get; set; } = default!;
    public int? EstimatedInputTokens { get; set; }
    public string? IncludedMemoryIdsJson { get; set; }
    public string? IncludedConceptIdsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ConversationEntity Conversation { get; set; } = default!;
    public AssistantTurnEntity? Turn { get; set; }
}
