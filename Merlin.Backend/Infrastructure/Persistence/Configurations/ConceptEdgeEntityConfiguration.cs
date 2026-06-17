using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class ConceptEdgeEntityConfiguration : IEntityTypeConfiguration<ConceptEdgeEntity>
{
    public void Configure(EntityTypeBuilder<ConceptEdgeEntity> builder)
    {
        builder.ToTable("concept_edges");
        builder.HasKey(edge => new { edge.FromConceptId, edge.ToConceptId, edge.RelationType });
        builder.Property(edge => edge.FromConceptId).HasMaxLength(80);
        builder.Property(edge => edge.ToConceptId).HasMaxLength(80);
        builder.Property(edge => edge.RelationType).HasMaxLength(80).IsRequired();
        builder.HasIndex(edge => edge.ToConceptId);
        builder.HasIndex(edge => edge.RelationType);
        builder.HasOne(edge => edge.FromConcept)
            .WithMany(concept => concept.OutgoingEdges)
            .HasForeignKey(edge => edge.FromConceptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(edge => edge.ToConcept)
            .WithMany(concept => concept.IncomingEdges)
            .HasForeignKey(edge => edge.ToConceptId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
