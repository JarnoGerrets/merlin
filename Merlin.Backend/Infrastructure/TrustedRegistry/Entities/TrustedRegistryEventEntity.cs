namespace Merlin.Backend.Infrastructure.TrustedRegistry.Entities;

public sealed class TrustedRegistryEventEntity
{
    public long Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public long? EntityId { get; set; }

    public string? Alias { get; set; }

    public string? Command { get; set; }

    public string? Target { get; set; }

    public string? ToolName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string? MetadataJson { get; set; }
}
