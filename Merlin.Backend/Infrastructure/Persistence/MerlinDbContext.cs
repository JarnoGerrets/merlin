using System.Globalization;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Merlin.Backend.Infrastructure.Persistence;

public sealed class MerlinDbContext : DbContext
{
    public MerlinDbContext(DbContextOptions<MerlinDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemoryEntity> Memories => Set<MemoryEntity>();
    public DbSet<ConceptEntity> Concepts => Set<ConceptEntity>();
    public DbSet<MemoryConceptEntity> MemoryConcepts => Set<MemoryConceptEntity>();
    public DbSet<ConceptEdgeEntity> ConceptEdges => Set<ConceptEdgeEntity>();
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<ConversationTopicEntity> ConversationTopics => Set<ConversationTopicEntity>();
    public DbSet<AssistantTurnEntity> AssistantTurns => Set<AssistantTurnEntity>();
    public DbSet<PromptCompilationEntity> PromptCompilations => Set<PromptCompilationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MerlinDbContext).Assembly);
        ConfigureDateTimeOffsetStorage(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureDateTimeOffsetStorage(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, string>(
            value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, string?>(
            value => value.HasValue ? value.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : null,
            value => value == null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
