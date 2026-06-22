namespace Merlin.Backend.Core.Memory.Models;

public sealed record PromptCompilationRecord
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public string? TurnId { get; init; }
    public required string PromptType { get; init; }
    public required string CompiledPrompt { get; init; }
    public int? EstimatedInputTokens { get; init; }
    public string? IncludedMemoryIdsJson { get; init; }
    public string? IncludedConceptIdsJson { get; init; }
    public string? IncludedProfileFactIdsJson { get; init; }
    public string? CompiledBlocksJson { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
