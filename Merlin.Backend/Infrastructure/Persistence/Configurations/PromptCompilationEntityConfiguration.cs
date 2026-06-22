using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Merlin.Backend.Infrastructure.Persistence.Configurations;

public sealed class PromptCompilationEntityConfiguration : IEntityTypeConfiguration<PromptCompilationEntity>
{
    public void Configure(EntityTypeBuilder<PromptCompilationEntity> builder)
    {
        builder.ToTable("prompt_compilations");
        builder.HasKey(prompt => prompt.Id);
        builder.Property(prompt => prompt.ConversationId).HasMaxLength(80).IsRequired();
        builder.Property(prompt => prompt.TurnId).HasMaxLength(80);
        builder.Property(prompt => prompt.PromptType).HasMaxLength(60).IsRequired();
        builder.Property(prompt => prompt.CompiledPrompt).IsRequired();
        builder.Property(prompt => prompt.CompiledBlocksJson);
        builder.Property(prompt => prompt.IncludedProfileFactIdsJson);
        builder.HasIndex(prompt => prompt.ConversationId);
        builder.HasIndex(prompt => prompt.TurnId);
        builder.HasIndex(prompt => prompt.PromptType);
        builder.HasIndex(prompt => prompt.CreatedAt);
        builder.HasOne(prompt => prompt.Conversation)
            .WithMany(conversation => conversation.PromptCompilations)
            .HasForeignKey(prompt => prompt.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(prompt => prompt.Turn)
            .WithMany(turn => turn.PromptCompilations)
            .HasForeignKey(prompt => prompt.TurnId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
