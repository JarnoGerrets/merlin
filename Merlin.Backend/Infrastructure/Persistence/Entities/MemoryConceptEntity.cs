namespace Merlin.Backend.Infrastructure.Persistence.Entities;

public sealed class MemoryConceptEntity
{
    public string MemoryId { get; set; } = default!;
    public string ConceptId { get; set; } = default!;
    public double Weight { get; set; } = 1.0;
    public MemoryEntity Memory { get; set; } = default!;
    public ConceptEntity Concept { get; set; } = default!;
}
