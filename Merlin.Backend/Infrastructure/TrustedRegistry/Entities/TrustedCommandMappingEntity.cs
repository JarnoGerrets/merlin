namespace Merlin.Backend.Infrastructure.TrustedRegistry.Entities;

public sealed class TrustedCommandMappingEntity
{
    public long Id { get; set; }

    public string OriginalCommand { get; set; } = string.Empty;

    public string NormalizedOriginalCommand { get; set; } = string.Empty;

    public string Intent { get; set; } = string.Empty;

    public string NormalizedCommand { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastUsedAtUtc { get; set; }

    public int UseCount { get; set; }

    public string Status { get; set; } = TrustedRegistryStatuses.Active;

    public string? MetadataJson { get; set; }
}
