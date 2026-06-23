namespace Merlin.Backend.Infrastructure.TrustedRegistry.Entities;

public sealed class TrustedAppMappingEntity
{
    public long Id { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string NormalizedAlias { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastUsedAtUtc { get; set; }

    public int UseCount { get; set; }

    public string Status { get; set; } = TrustedRegistryStatuses.Active;

    public string? MetadataJson { get; set; }
}
