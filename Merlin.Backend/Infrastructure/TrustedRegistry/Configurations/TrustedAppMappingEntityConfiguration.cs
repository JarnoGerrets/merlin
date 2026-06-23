using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Configurations;

public sealed class TrustedAppMappingEntityConfiguration : IEntityTypeConfiguration<TrustedAppMappingEntity>
{
    public void Configure(EntityTypeBuilder<TrustedAppMappingEntity> builder)
    {
        builder.ToTable("trusted_app_mappings");
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Alias).IsRequired();
        builder.Property(entity => entity.NormalizedAlias).IsRequired();
        builder.Property(entity => entity.DisplayName).IsRequired();
        builder.Property(entity => entity.ExecutablePath).IsRequired();
        builder.Property(entity => entity.Source).IsRequired();
        builder.Property(entity => entity.Status).IsRequired();

        builder.HasIndex(entity => entity.NormalizedAlias);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.LastUsedAtUtc);
        builder.HasIndex(entity => entity.UseCount);
        builder.HasIndex(entity => new { entity.Status, entity.NormalizedAlias });
        builder.HasIndex(entity => entity.NormalizedAlias)
            .IsUnique()
            .HasFilter("Status = 'active'");
    }
}
