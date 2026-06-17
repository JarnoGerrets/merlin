namespace Merlin.Backend.Core.Memory.Models;

public sealed record PromptCompileRequest
{
    public required string CurrentUserMessage { get; init; }
    public required string PromptType { get; init; }
    public string? ConversationId { get; init; }
    public string? TurnId { get; init; }
    public string? EscalationReason { get; init; }
    public int MaxInputTokens { get; init; } = 2500;
    public int MaxMemoryTokens { get; init; } = 1000;
    public IReadOnlyList<RetrievedMemory> RetrievedMemories { get; init; } = [];
}

public sealed record PromptCompileResult
{
    public required string CompiledPrompt { get; init; }
    public required int EstimatedInputTokens { get; init; }
    public IReadOnlyList<string> IncludedMemoryIds { get; init; } = [];
    public IReadOnlyList<string> IncludedConceptIds { get; init; } = [];
    public IReadOnlyList<string> OmittedMemoryIds { get; init; } = [];
    public IReadOnlyList<string> TrimReasons { get; init; } = [];
    public string? PromptCompilationId { get; init; }
}

public sealed record CompiledMemoryContext
{
    public required string Prompt { get; init; }
    public required string CurrentUserMessage { get; init; }
    public IReadOnlyList<string> IncludedMemoryIds { get; init; } = [];
    public int EstimatedInputTokens { get; init; }
}
