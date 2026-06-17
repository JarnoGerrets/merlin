using Merlin.Backend.Core.Conversation;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Infrastructure.Persistence.Repositories;
using Merlin.Backend.Infrastructure.Persistence.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class PersistenceFoundationSmokeTests
{
    [Fact]
    public void MerlinDbPathResolver_UsesAppDataDatabaseLocation()
    {
        var resolver = new MerlinDbPathResolver(
            Options.Create(new MerlinDbOptions()),
            NullLogger<MerlinDbPathResolver>.Instance);

        var path = resolver.ResolveDatabasePath();

        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), path, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Merlin", "db", "merlin_memory.db"), path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MemoryConceptSeedAndSearchSmokeFlow_Works()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var memoryStore = new EfMemoryStore(fixture.Db, NullLogger<EfMemoryStore>.Instance);
        var conceptStore = new EfConceptStore(fixture.Db, NullLogger<EfConceptStore>.Instance);
        var seeder = new MerlinConceptSeeder(conceptStore, NullLogger<MerlinConceptSeeder>.Instance);
        var extractor = new LocalConceptExtractionService();
        var search = new MemorySearchService(memoryStore);

        await seeder.SeedAsync();
        var sqliteConcept = await conceptStore.GetConceptByNameAsync("SQLite");
        var memoryConcept = await conceptStore.GetConceptByNameAsync("memory");
        Assert.NotNull(sqliteConcept);
        Assert.NotNull(memoryConcept);

        var memory = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            MemoryType = "project_decision",
            Title = "Merlin SQLite EF persistence smoke test",
            Content = "Merlin stores local memory in SQLite under AppData using EF Core.",
            Project = "Merlin",
            Importance = 0.9,
            Confidence = 0.95,
            UserConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await memoryStore.SaveMemoryAsync(memory);
        var conceptNames = extractor.ExtractConceptNames(memory.Content);
        Assert.Contains("SQLite", conceptNames);
        Assert.Contains("memory", conceptNames);

        foreach (var conceptName in conceptNames)
        {
            var concept = await conceptStore.GetOrCreateConceptAsync(conceptName);
            await conceptStore.LinkMemoryToConceptAsync(memory.Id, concept.Id, 1.0);
        }

        var textResults = await search.SearchAsync(new MemorySearchRequest
        {
            Query = "SQLite",
            Limit = 10
        });
        Assert.Contains(textResults, result => result.Memory.Id == memory.Id);

        var conceptResults = await search.SearchAsync(new MemorySearchRequest
        {
            ConceptIds = [sqliteConcept!.Id],
            Limit = 10
        });
        Assert.Contains(conceptResults, result => result.Memory.Id == memory.Id);

        await memoryStore.UpdateLastAccessedAsync([memory.Id], DateTimeOffset.UtcNow);
        var updated = await memoryStore.GetMemoryAsync(memory.Id);
        Assert.NotNull(updated!.LastAccessedAt);
    }

    [Fact]
    public async Task ConversationTurnAndPromptSmokeFlow_Works()
    {
        await using var fixture = await PersistenceFixture.CreateAsync();
        var conversationStore = new EfConversationStateStore(fixture.Db);
        var turnStore = new EfTurnStateStore(fixture.Db, NullLogger<EfTurnStateStore>.Instance);
        var promptStore = new EfPromptCompilationStore(fixture.Db, NullLogger<EfPromptCompilationStore>.Instance);
        var runtimeState = new ConversationRuntimeState(conversationStore);
        var turnTracker = new AssistantTurnTracker(runtimeState, turnStore);
        var promptLogger = new PromptCompilationLogger(promptStore, new SimpleTokenEstimator());

        var conversation = await runtimeState.GetCurrentConversationAsync();
        var topic = await runtimeState.StartOrSwitchTopicAsync("Persistence foundation test");
        var turn = await turnTracker.StartTurnAsync("How should we build this?", topic.Id);

        await turnTracker.AppendGeneratedTextAsync(turn.Id, "I would use PostgreSQL...");
        await turnTracker.AppendSpokenTextAsync(turn.Id, "I would use PostgreSQL...");
        await turnTracker.MarkInterruptedAsync(turn.Id, "user_correction", "No, SQLite.");
        await promptLogger.LogPromptAsync(
            conversation.Id,
            turn.Id,
            PromptTypes.Correction,
            "Correction prompt: discard PostgreSQL and continue with SQLite.",
            null,
            ["memory-sqlite"],
            ["concept-sqlite", "concept-correction"]);

        var savedTurn = await turnStore.GetTurnAsync(turn.Id);
        var promptCompilations = await promptStore.GetPromptCompilationsForTurnAsync(turn.Id);

        Assert.Equal(AssistantTurnStates.Interrupted, savedTurn!.State);
        Assert.Equal("How should we build this?", savedTurn.OriginalUserMessage);
        Assert.Contains("PostgreSQL", savedTurn.GeneratedTextSoFar);
        Assert.Contains("PostgreSQL", savedTurn.SpokenTextSoFar);
        Assert.Equal("No, SQLite.", savedTurn.InterruptedByUserMessage);
        Assert.Single(promptCompilations);
        Assert.True(promptCompilations[0].EstimatedInputTokens > 0);
    }

    private sealed class PersistenceFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private PersistenceFixture(SqliteConnection connection, MerlinDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public MerlinDbContext Db { get; }

        public static async Task<PersistenceFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<MerlinDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new MerlinDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new PersistenceFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
