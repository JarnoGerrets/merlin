namespace Merlin.Backend.Models;

public sealed class MemoryCandidate
{
    public string CandidateId { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
