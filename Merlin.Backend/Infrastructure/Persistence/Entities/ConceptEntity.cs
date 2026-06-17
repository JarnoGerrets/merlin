namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class ConceptEntity
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ConceptType { get; set; }
    public string? ParentConceptId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ConceptEntity? ParentConcept { get; set; }
    public List<ConceptEntity> ChildConcepts { get; set; } = [];
    public List<MemoryConceptEntity> MemoryConcepts { get; set; } = [];
    public List<ConceptEdgeEntity> OutgoingEdges { get; set; } = [];
    public List<ConceptEdgeEntity> IncomingEdges { get; set; } = [];
}
