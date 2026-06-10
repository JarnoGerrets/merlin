namespace Merlin.Backend.Models;

public sealed class TrustedApplicationMapping
{
    public string Alias { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUsedAtUtc { get; init; }
}
