namespace Merlin.Backend.Models;

public sealed class TrustedCommandMapping
{
    public string OriginalCommand { get; init; } = string.Empty;

    public string NormalizedOriginalCommand { get; init; } = string.Empty;

    public string Intent { get; init; } = string.Empty;

    public string NormalizedCommand { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUsedAtUtc { get; init; }

    public int UseCount { get; init; }
}
