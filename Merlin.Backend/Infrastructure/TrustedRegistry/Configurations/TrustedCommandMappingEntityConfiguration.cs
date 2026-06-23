using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Configurations;

public sealed class TrustedCommandMappingEntityConfiguration : IEntityTypeConfiguration<TrustedCommandMappingEntity>
{
    public void Configure(EntityTypeBuilder<TrustedCommandMappingEntity> builder)
    {
        builder.ToTable("trusted_command_mappings");
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.OriginalCommand).IsRequired();
        builder.Property(entity => entity.NormalizedOriginalCommand).IsRequired();
        builder.Property(entity => entity.Intent).IsRequired();
        builder.Property(entity => entity.NormalizedCommand).IsRequired();
        builder.Property(entity => entity.ToolName).IsRequired();
        builder.Property(entity => entity.Target).IsRequired();
        builder.Property(entity => entity.DisplayName).IsRequired();
        builder.Property(entity => entity.Status).IsRequired();

        builder.HasIndex(entity => entity.NormalizedOriginalCommand);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.ToolName);
        builder.HasIndex(entity => entity.Intent);
        builder.HasIndex(entity => entity.UseCount);
        builder.HasIndex(entity => entity.LastUsedAtUtc);
        builder.HasIndex(entity => new { entity.Status, entity.NormalizedOriginalCommand });
        builder.HasIndex(entity => entity.NormalizedOriginalCommand)
            .IsUnique()
            .HasFilter("Status = 'active'");
    }
}
