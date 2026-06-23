using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Configurations;

public sealed class TrustedRegistryEventEntityConfiguration : IEntityTypeConfiguration<TrustedRegistryEventEntity>
{
    public void Configure(EntityTypeBuilder<TrustedRegistryEventEntity> builder)
    {
        builder.ToTable("trusted_registry_events");
        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.EventType).IsRequired();
        builder.Property(entity => entity.EntityType).IsRequired();

        builder.HasIndex(entity => entity.EventType);
        builder.HasIndex(entity => entity.EntityType);
        builder.HasIndex(entity => entity.EntityId);
        builder.HasIndex(entity => entity.CreatedAtUtc);
    }
}
