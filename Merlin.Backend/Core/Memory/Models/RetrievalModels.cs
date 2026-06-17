namespace Merlin.Backend.Core.Memory.Models;

public sealed record MemoryRetrievalRequest
{
    public required string Query { get; init; }
    public IReadOnlyList<string> PreferredMemoryTypes { get; init; } = [];
    public int MaxResults { get; init; } = 8;
    public bool IncludeArchived { get; init; }
    public DateTimeOffset? NowUtc { get; init; }
}

public sealed record RetrievedMemory
{
    public required string MemoryId { get; init; }
    public required string MemoryType { get; init; }
    public string? Title { get; init; }
    public required string Content { get; init; }
    public string? Summary { get; init; }
    public double Score { get; init; }
    public IReadOnlyList<string> MatchedConcepts { get; init; } = [];
    public IReadOnlyList<string> MatchReasons { get; init; } = [];
}

public sealed record ActivatedConcept
{
    public required string ConceptId { get; init; }
    public required string Name { get; init; }
    public required double Score { get; init; }
    public required bool IsDirect { get; init; }
    public string? ActivatedByConceptId { get; init; }
    public string? RelationType { get; init; }
}
