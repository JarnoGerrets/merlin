namespace Merlin.Backend.Core.Memory.Models;

public sealed record ExplicitMemoryRequest
{
    public required bool IsExplicitMemoryRequest { get; init; }
    public string? ContentToRemember { get; init; }
    public string? TriggerPhrase { get; init; }
    public string? MemoryTypeHint { get; init; }
    public double Confidence { get; init; }
    public string? Reason { get; init; }
}

public sealed record MemorySaveResult
{
    public required bool Saved { get; init; }
    public string? MemoryId { get; init; }
    public string? MemoryType { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<string> Concepts { get; init; } = [];
    public bool WasDuplicate { get; init; }
    public string? ExistingMemoryId { get; init; }
    public string? Message { get; init; }
}
