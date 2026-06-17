namespace Merlin.Backend.Core.Memory.Models;

public sealed record ConceptRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? ConceptType { get; init; }
    public string? ParentConceptId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
