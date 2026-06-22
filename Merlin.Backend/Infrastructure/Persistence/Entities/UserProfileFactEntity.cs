namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class UserProfileFactEntity
{
    public string Id { get; set; } = default!;
    public string ProfileId { get; set; } = "default";
    public string Key { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string DisplayText { get; set; } = default!;
    public double Priority { get; set; } = 0.5;
    public double Confidence { get; set; } = 1.0;
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastConfirmedAt { get; set; }
    public string SourceType { get; set; } = default!;
    public string? SourceMemoryId { get; set; }
    public string? SupersedesFactId { get; set; }
    public string? MetadataJson { get; set; }
}
