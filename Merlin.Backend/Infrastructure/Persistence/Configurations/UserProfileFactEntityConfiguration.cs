using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class UserProfileFactEntityConfiguration : IEntityTypeConfiguration<UserProfileFactEntity>
{
    public void Configure(EntityTypeBuilder<UserProfileFactEntity> builder)
    {
        builder.ToTable("user_profile_facts");
        builder.HasKey(fact => fact.Id);
        builder.Property(fact => fact.ProfileId).HasMaxLength(80).IsRequired();
        builder.Property(fact => fact.Key).HasMaxLength(160).IsRequired();
        builder.Property(fact => fact.Category).HasMaxLength(80).IsRequired();
        builder.Property(fact => fact.Value).HasMaxLength(240).IsRequired();
        builder.Property(fact => fact.DisplayText).IsRequired();
        builder.Property(fact => fact.Status).HasMaxLength(40).IsRequired();
        builder.Property(fact => fact.SourceType).HasMaxLength(80).IsRequired();
        builder.Property(fact => fact.SourceMemoryId).HasMaxLength(80);
        builder.Property(fact => fact.SupersedesFactId).HasMaxLength(80);
        builder.HasIndex(fact => fact.ProfileId);
        builder.HasIndex(fact => fact.Key);
        builder.HasIndex(fact => fact.Category);
        builder.HasIndex(fact => fact.Status);
        builder.HasIndex(fact => fact.UpdatedAt);
        builder.HasIndex(fact => new { fact.ProfileId, fact.Key, fact.Status });
        builder.HasIndex(fact => new { fact.ProfileId, fact.Key })
            .IsUnique()
            .HasFilter("Status = 'active'");
    }
}
