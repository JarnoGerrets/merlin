namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class ConceptEdgeEntity
{
    public string FromConceptId { get; set; } = default!;
    public string ToConceptId { get; set; } = default!;
    public string RelationType { get; set; } = default!;
    public double Weight { get; set; } = 1.0;
    public ConceptEntity FromConcept { get; set; } = default!;
    public ConceptEntity ToConcept { get; set; } = default!;
}
