using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Configurations;

public sealed class TrustedUrlMappingEntityConfiguration : IEntityTypeConfiguration<TrustedUrlMappingEntity>
{
    public void Configure(EntityTypeBuilder<TrustedUrlMappingEntity> builder)
    {
        builder.ToTable("trusted_url_mappings");
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Alias).IsRequired();
        builder.Property(entity => entity.NormalizedAlias).IsRequired();
        builder.Property(entity => entity.Url).IsRequired();
        builder.Property(entity => entity.DisplayName).IsRequired();
        builder.Property(entity => entity.Status).IsRequired();

        builder.HasIndex(entity => entity.NormalizedAlias);
        builder.HasIndex(entity => entity.Url);
        builder.HasIndex(entity => entity.Status);
        builder.HasIndex(entity => entity.UseCount);
        builder.HasIndex(entity => entity.LastUsedAtUtc);
        builder.HasIndex(entity => new { entity.Status, entity.NormalizedAlias });
        builder.HasIndex(entity => entity.NormalizedAlias)
            .IsUnique()
            .HasFilter("Status = 'active'");
    }
}
