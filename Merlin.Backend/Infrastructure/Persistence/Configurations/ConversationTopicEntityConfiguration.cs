using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class ConversationTopicEntityConfiguration : IEntityTypeConfiguration<ConversationTopicEntity>
{
    public void Configure(EntityTypeBuilder<ConversationTopicEntity> builder)
    {
        builder.ToTable("conversation_topics");
        builder.HasKey(topic => topic.Id);
        builder.Property(topic => topic.ConversationId).HasMaxLength(80).IsRequired();
        builder.Property(topic => topic.Title).HasMaxLength(240).IsRequired();
        builder.Property(topic => topic.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(topic => topic.ConversationId);
        builder.HasIndex(topic => topic.Status);
        builder.HasIndex(topic => topic.StartedAt);
        builder.HasOne(topic => topic.Conversation)
            .WithMany(conversation => conversation.Topics)
            .HasForeignKey(topic => topic.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
