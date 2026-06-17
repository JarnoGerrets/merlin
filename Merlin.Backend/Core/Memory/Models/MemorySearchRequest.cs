namespace Merlin.Backend.Core.Memory.Models;

public sealed record MemorySearchRequest
{
    public string? Query { get; init; }
    public IReadOnlyCollection<string> ConceptIds { get; init; } = [];
    public IReadOnlyCollection<string> MemoryTypes { get; init; } = [];
    public string? Project { get; init; }
    public string? Topic { get; init; }
    public bool IncludeExpired { get; init; }
    public int Limit { get; init; } = 10;
}
