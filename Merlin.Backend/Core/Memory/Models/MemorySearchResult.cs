namespace Merlin.Backend.Core.Memory.Models;

public sealed record MemorySearchResult
{
    public required MemoryRecord Memory { get; init; }
    public double Score { get; init; }
    public string MatchReason { get; init; } = "unknown";
}
