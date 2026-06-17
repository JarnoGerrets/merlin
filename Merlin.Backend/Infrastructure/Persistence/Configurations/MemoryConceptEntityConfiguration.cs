using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class MemoryConceptEntityConfiguration : IEntityTypeConfiguration<MemoryConceptEntity>
{
    public void Configure(EntityTypeBuilder<MemoryConceptEntity> builder)
    {
        builder.ToTable("memory_concepts");
        builder.HasKey(link => new { link.MemoryId, link.ConceptId });
        builder.Property(link => link.MemoryId).HasMaxLength(80);
        builder.Property(link => link.ConceptId).HasMaxLength(80);
        builder.HasIndex(link => link.ConceptId);
        builder.HasOne(link => link.Memory)
            .WithMany(memory => memory.MemoryConcepts)
            .HasForeignKey(link => link.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(link => link.Concept)
            .WithMany(concept => concept.MemoryConcepts)
            .HasForeignKey(link => link.ConceptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
