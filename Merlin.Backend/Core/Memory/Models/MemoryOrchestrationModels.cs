namespace Merlin.Backend.Core.Memory.Models;

public sealed record MemoryPreparationResult
{
    public required string ConversationId { get; init; }
    public string? TopicId { get; init; }
    public string? TurnId { get; init; }
    public required string CompiledPrompt { get; init; }
    public required int EstimatedInputTokens { get; init; }
    public IReadOnlyList<string> IncludedMemoryIds { get; init; } = [];
    public IReadOnlyList<string> IncludedConceptIds { get; init; } = [];
    public IReadOnlyList<RetrievedMemory> RetrievedMemories { get; init; } = [];
    public MemorySaveResult? ExplicitSaveResult { get; init; }
    public string? LocalResponse { get; init; }
}
