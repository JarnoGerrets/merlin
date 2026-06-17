using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class AssistantTurnEntityConfiguration : IEntityTypeConfiguration<AssistantTurnEntity>
{
    public void Configure(EntityTypeBuilder<AssistantTurnEntity> builder)
    {
        builder.ToTable("assistant_turns");
        builder.HasKey(turn => turn.Id);
        builder.Property(turn => turn.ConversationId).HasMaxLength(80).IsRequired();
        builder.Property(turn => turn.TopicId).HasMaxLength(80);
        builder.Property(turn => turn.OriginalUserMessage).IsRequired();
        builder.Property(turn => turn.State).HasMaxLength(60).IsRequired();
        builder.Property(turn => turn.InterruptionReason).HasMaxLength(160);
        builder.HasIndex(turn => turn.ConversationId);
        builder.HasIndex(turn => turn.TopicId);
        builder.HasIndex(turn => turn.State);
        builder.HasIndex(turn => turn.CreatedAt);
        builder.HasOne(turn => turn.Conversation)
            .WithMany(conversation => conversation.AssistantTurns)
            .HasForeignKey(turn => turn.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(turn => turn.Topic)
            .WithMany(topic => topic.AssistantTurns)
            .HasForeignKey(turn => turn.TopicId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
