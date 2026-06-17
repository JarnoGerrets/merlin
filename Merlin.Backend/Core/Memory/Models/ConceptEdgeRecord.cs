namespace Merlin.Backend.Core.Memory.Models;

public sealed record ConceptEdgeRecord
{
    public required string FromConceptId { get; init; }
    public required string ToConceptId { get; init; }
    public required string RelationType { get; init; }
    public double Weight { get; init; } = 1.0;
}
