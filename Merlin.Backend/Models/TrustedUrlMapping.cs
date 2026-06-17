namespace Merlin.Backend.Models;

public sealed class TrustedUrlMapping
{
    public string Alias { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUsedAtUtc { get; init; }

    public int UseCount { get; init; }
}
