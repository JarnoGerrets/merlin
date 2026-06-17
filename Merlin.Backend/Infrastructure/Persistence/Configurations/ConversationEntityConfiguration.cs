using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class ConversationEntityConfiguration : IEntityTypeConfiguration<ConversationEntity>
{
    public void Configure(EntityTypeBuilder<ConversationEntity> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Title).HasMaxLength(240);
        builder.Property(conversation => conversation.ActiveTopic).HasMaxLength(160);
        builder.Property(conversation => conversation.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(conversation => conversation.Status);
        builder.HasIndex(conversation => conversation.CreatedAt);
        builder.HasIndex(conversation => conversation.UpdatedAt);
    }
}
