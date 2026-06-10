namespace Merlin.Backend.Models;

public sealed class MemoryRecord
{
    public string MemoryId { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string Key { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }

    public double Confidence { get; init; }
}
