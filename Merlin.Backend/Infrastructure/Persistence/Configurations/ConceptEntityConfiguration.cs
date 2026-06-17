using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class ConceptEntityConfiguration : IEntityTypeConfiguration<ConceptEntity>
{
    public void Configure(EntityTypeBuilder<ConceptEntity> builder)
    {
        builder.ToTable("concepts");
        builder.HasKey(concept => concept.Id);
        builder.Property(concept => concept.Name).HasMaxLength(160).IsRequired();
        builder.Property(concept => concept.ConceptType).HasMaxLength(80);
        builder.Property(concept => concept.ParentConceptId).HasMaxLength(80);
        builder.HasIndex(concept => concept.Name).IsUnique();
        builder.HasIndex(concept => concept.ConceptType);
        builder.HasIndex(concept => concept.ParentConceptId);
        builder.HasOne(concept => concept.ParentConcept)
            .WithMany(concept => concept.ChildConcepts)
            .HasForeignKey(concept => concept.ParentConceptId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
