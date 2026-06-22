using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class MemoryEntityConfiguration : IEntityTypeConfiguration<MemoryEntity>
{
    public void Configure(EntityTypeBuilder<MemoryEntity> builder)
    {
        builder.ToTable("memories");
        builder.HasKey(memory => memory.Id);
        builder.Property(memory => memory.MemoryType).HasMaxLength(80).IsRequired();
        builder.Property(memory => memory.Status).HasMaxLength(40).IsRequired().HasDefaultValue("active");
        builder.Property(memory => memory.Title).HasMaxLength(240);
        builder.Property(memory => memory.Content).IsRequired();
        builder.Property(memory => memory.Project).HasMaxLength(120);
        builder.Property(memory => memory.Topic).HasMaxLength(160);
        builder.Property(memory => memory.Source).HasMaxLength(120);
        builder.Property(memory => memory.SourceConversationId).HasMaxLength(80);
        builder.Property(memory => memory.SourceTurnId).HasMaxLength(80);
        builder.Property(memory => memory.MergedIntoMemoryId).HasMaxLength(80);
        builder.Property(memory => memory.SupersedesMemoryId).HasMaxLength(80);
        builder.HasIndex(memory => memory.Status);
        builder.HasIndex(memory => new { memory.Status, memory.MemoryType });
        builder.HasIndex(memory => memory.MemoryType);
        builder.HasIndex(memory => memory.Project);
        builder.HasIndex(memory => memory.Topic);
        builder.HasIndex(memory => memory.Importance);
        builder.HasIndex(memory => memory.CreatedAt);
        builder.HasIndex(memory => memory.ExpiresAt);
        builder.HasIndex(memory => memory.ArchivedAt);
        builder.HasIndex(memory => memory.DeletedAt);
        builder.HasIndex(memory => memory.SourceConversationId);
        builder.HasIndex(memory => memory.SourceTurnId);
    }
}
