using Merlin.Backend.Core.Conversation;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class BrainLikeMemoryLayerTests
{
    [Fact]
    public async Task CurrentConversationMemory_FollowUpContinuesTopic_AndUnrelatedStartsNewTopic()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var first = await fixture.CurrentConversation.ApplyUserMessageAsync("Let's design Merlin's memory system.");
        var followUpDecision = await fixture.CurrentConversation.AnalyzeUserMessageAsync("okay but how should the prompt compiler work?");
        var followUp = await fixture.CurrentConversation.ApplyUserMessageAsync("okay but how should the prompt compiler work?");
        var unrelatedDecision = await fixture.CurrentConversation.AnalyzeUserMessageAsync("what does beam do in Whisper?");
        var unrelated = await fixture.CurrentConversation.ApplyUserMessageAsync("what does beam do in Whisper?");

        Assert.NotNull(first.ActiveTopicId);
        Assert.False(followUpDecision.IsNewTopic);
        Assert.Equal(first.ActiveTopicId, followUp.ActiveTopicId);
        Assert.True(unrelatedDecision.IsNewTopic);
        Assert.NotEqual(first.ActiveTopicId, unrelated.ActiveTopicId);
    }

    [Fact]
    public async Task CurrentConversationMemory_SummaryStaysBounded()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        for (var index = 0; index < 20; index++)
        {
            await fixture.CurrentConversation.ApplyUserMessageAsync($"continue this memory design note with prompt compiler detail {index}");
        }

        var state = await fixture.CurrentConversation.GetOrCreateCurrentStateAsync();

        Assert.NotNull(state.RecentSummary);
        Assert.True(state.RecentSummary!.Length <= 1200);
    }

    [Fact]
    public async Task MemoryWriter_ExplicitRememberSavesConfirmedLongTermMemory_AndAvoidsDuplicate()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var first = await fixture.MemoryWriter.SaveExplicitMemoryAsync("Remember that Merlin should never send full chat history to DeepInfra by default.");
        var second = await fixture.MemoryWriter.SaveExplicitMemoryAsync("Remember that Merlin should never send full chat history to DeepInfra by default.");
        var reminder = fixture.MemoryWriter.DetectExplicitRequest("Remind me tomorrow to check the database.");

        Assert.True(first.Saved);
        Assert.Equal("architecture_decision", first.MemoryType);
        Assert.Contains("Merlin", first.Concepts);
        Assert.True(second.WasDuplicate);
        Assert.False(reminder.IsExplicitMemoryRequest);
    }

    [Theory]
    [InlineData("please save into long-term memory that Merlin should prefer SQLite for local memory storage", "Merlin should prefer SQLite for local memory storage")]
    [InlineData("save to memory that Merlin should use compact prompts", "Merlin should use compact prompts")]
    [InlineData("store in memory that Merlin should never send full chat history to DeepInfra", "Merlin should never send full chat history to DeepInfra")]
    [InlineData("add to memory that Merlin uses SQLite for local storage", "Merlin uses SQLite for local storage")]
    [InlineData("save as project decision that Merlin memory uses SQLite in AppData", "Merlin memory uses SQLite in AppData")]
    [InlineData("save as preference that dates should be spoken with month names", "Dates should be spoken with month names")]
    public async Task MemoryWriter_ExplicitStorageCommands_SaveOnlyContent(string input, string expectedContent)
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var result = await fixture.MemoryWriter.SaveExplicitMemoryAsync(input);
        var memory = await fixture.MemoryStore.GetMemoryAsync(result.MemoryId!);

        Assert.True(result.Saved);
        Assert.Equal("Saved.", result.Message);
        Assert.NotNull(memory);
        Assert.Equal(expectedContent, memory!.Content);
        Assert.True(memory.UserConfirmed);
    }

    [Theory]
    [InlineData("remember that Merlin should prefer SQLite")]
    [InlineData("please remember that Merlin should use compact prompts")]
    [InlineData("note that Merlin should not send full chat history to DeepInfra")]
    public void ExplicitMemoryRequestDetector_SecondaryImperativeCommands_Save(string input)
    {
        var detector = new ExplicitMemoryRequestDetector();

        var request = detector.Detect(input);

        Assert.True(request.IsExplicitMemoryRequest);
        Assert.NotNull(request.ContentToRemember);
    }

    [Theory]
    [InlineData("I remember that we talked about SQLite a while ago, what was the verdict?")]
    [InlineData("I remember this differently, what did we decide?")]
    [InlineData("Do you remember that SQLite discussion?")]
    [InlineData("What do you remember about SQLite?")]
    [InlineData("What did I say about memory storage?")]
    [InlineData("What was the verdict on SQLite?")]
    [InlineData("Can you recall our discussion about DeepInfra costs?")]
    [InlineData("Search memory for SQLite")]
    [InlineData("Find memories about prompt compiler")]
    [InlineData("Show memories about Merlin memory")]
    public void ExplicitMemoryRequestDetector_RecallSearchPhrases_DoNotSave(string input)
    {
        var detector = new ExplicitMemoryRequestDetector();

        var request = detector.Detect(input);

        Assert.False(request.IsExplicitMemoryRequest);
        Assert.Contains("Recall/search", request.Reason);
    }

    [Theory]
    [InlineData("save as project decision that Merlin memory uses SQLite in AppData", "project_decision")]
    [InlineData("save as architecture decision that Merlin memory uses SQLite in AppData", "architecture_decision")]
    [InlineData("save as preference that dates should be spoken with month names", "user_preference")]
    [InlineData("save as tool preference that Merlin should format dates carefully", "tool_preference")]
    public async Task MemoryWriter_TypeHintsAreRespected(string input, string expectedType)
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var result = await fixture.MemoryWriter.SaveExplicitMemoryAsync(input);

        Assert.True(result.Saved);
        Assert.Equal(expectedType, result.MemoryType);
    }

    [Fact]
    public async Task TopicClosing_CreatesEpisodeMemory_WhenTopicCloses()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        await fixture.CurrentConversation.ApplyUserMessageAsync("Let's design Merlin memory architecture with DeepInfra token reduction.");
        await fixture.CurrentConversation.ApplyUserMessageAsync("continue with medium memory and prompt compiler details.");

        var result = await fixture.TopicClosing.CloseCurrentTopicAsync(TopicCloseReasons.TopicSwitch);
        var memory = await fixture.MemoryStore.GetMemoryAsync(result.MediumMemoryId!);

        Assert.True(result.Closed, result.Reason);
        Assert.NotNull(memory);
        Assert.Equal("episode", memory!.MemoryType);
        Assert.Contains("Topic:", memory.Content);
    }

    [Fact]
    public async Task AssociativeRetriever_RetrievesByKeywordConceptAndOneHopGraph()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var memory = await fixture.SaveMemoryAsync(
            "architecture_decision",
            "Associative retrieval filing cabinet",
            "Merlin should retrieve memories associatively using concepts.",
            ["associative retrieval"]);

        var filing = await fixture.ConceptStore.GetOrCreateConceptAsync("filing cabinet");
        var associative = await fixture.ConceptStore.GetOrCreateConceptAsync("associative retrieval");
        await fixture.ConceptStore.UpsertConceptEdgeAsync(filing.Id, associative.Id, "related_to", 1.0);

        var keywordResults = await fixture.Retriever.RetrieveAsync(new MemoryRetrievalRequest { Query = "associative retrieval", MaxResults = 5 });
        var graphResults = await fixture.Retriever.RetrieveAsync(new MemoryRetrievalRequest { Query = "what was the filing cabinet idea?", MaxResults = 5 });

        Assert.Contains(keywordResults, result => result.MemoryId == memory.Id && result.MatchReasons.Any(reason => reason.Contains("keyword", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(graphResults, result => result.MemoryId == memory.Id && result.MatchReasons.Any(reason => reason.Contains("Activated related concept", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task PromptCompiler_PreservesExactUserMessage_TrimsAndLogs()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var exact = "i think we should use sql lite? maybe appdata? don't change my words";
        var memories = Enumerable.Range(0, 8)
            .Select(index => new RetrievedMemory
            {
                MemoryId = $"memory-{index}",
                MemoryType = index == 0 ? "architecture_decision" : "episode",
                Title = $"Memory {index}",
                Content = new string('x', 600),
                Summary = $"Summary {index} " + new string('s', 180),
                Score = 1.0 - (index * 0.1),
                MatchReasons = ["test memory"]
            })
            .ToList();

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = exact,
            PromptType = "normal_conversation",
            MaxInputTokens = 250,
            MaxMemoryTokens = 20,
            RetrievedMemories = memories
        });
        var logs = await fixture.PromptStore.ListRecentPromptCompilationsAsync(10);

        Assert.Contains(exact, result.CompiledPrompt);
        Assert.True(result.OmittedMemoryIds.Count > 0);
        Assert.Single(logs);
        Assert.True(logs[0].EstimatedInputTokens > 0);
    }

    [Fact]
    public async Task MemoryOrchestrator_ExplicitRememberReturnsLocalResponse_AndNormalCallUsesCompactPrompt()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var saved = await fixture.Orchestrator.PrepareForModelCallAsync("Remember that Merlin should keep memory local-first.", "test");
        var prepared = await fixture.Orchestrator.PrepareForModelCallAsync("how should Merlin reduce DeepInfra costs again?", "test");
        var rawHistory = string.Join("\n", Enumerable.Range(0, 40).Select(index => $"User said lots of raw history {index} about unrelated text."));

        Assert.Equal("Saved.", saved.LocalResponse);
        Assert.Null(prepared.LocalResponse);
        Assert.Contains("CURRENT USER MESSAGE:", prepared.CompiledPrompt);
        Assert.Contains("how should Merlin reduce DeepInfra costs again?", prepared.CompiledPrompt);
        Assert.True(prepared.CompiledPrompt.Length < rawHistory.Length);
    }

    [Fact]
    public async Task MemoryOrchestrator_ExplicitSaveSkipsDeepInfraByReturningLocalResponse()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var prepared = await fixture.Orchestrator.PrepareForModelCallAsync(
            "please save into long-term memory that Merlin should prefer SQLite for local memory storage",
            "test");
        var saved = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = "SQLite",
            Limit = 10
        });

        Assert.Equal("Saved.", prepared.LocalResponse);
        Assert.Contains(saved, result => result.Memory.UserConfirmed);
        Assert.Contains(saved, result => result.Memory.Content == "Merlin should prefer SQLite for local memory storage");
        Assert.Contains(saved.SelectMany(result => fixture.ConceptStore.GetConceptsForMemoryAsync(result.Memory.Id).GetAwaiter().GetResult()), concept => concept.Name == "SQLite");
    }

    [Fact]
    public async Task MemoryDebugService_ListsMemoriesRetrievalAndPromptLogs()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        await fixture.MemoryWriter.SaveExplicitMemoryAsync("Remember that Merlin should use local memory to reduce DeepInfra token costs.");

        var memories = await fixture.DebugService.ListMemoriesAsync("architecture_decision", "Merlin", null, 20);
        var retrieved = await fixture.DebugService.RetrieveAsync("DeepInfra token costs", 8);
        var compiled = await fixture.DebugService.CompilePromptAsync("how do we reduce DeepInfra costs again?", 2500);
        var logs = await fixture.DebugService.ListPromptCompilationsAsync(20);

        Assert.NotEmpty(memories);
        Assert.NotEmpty(retrieved);
        Assert.All(retrieved, item => Assert.NotEmpty(item.MatchReasons));
        Assert.Contains("how do we reduce DeepInfra costs again?", compiled.CompiledPrompt);
        Assert.NotEmpty(logs);
    }

    private sealed class MemoryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private MemoryFixture(SqliteConnection connection, MerlinDbContext db)
        {
            _connection = connection;
            Db = db;
            MemoryStore = new EfMemoryStore(db, NullLogger<EfMemoryStore>.Instance);
            ConceptStore = new EfConceptStore(db, NullLogger<EfConceptStore>.Instance);
            ConversationStore = new EfConversationStateStore(db);
            PromptStore = new EfPromptCompilationStore(db, NullLogger<EfPromptCompilationStore>.Instance);

            var extractor = new LocalConceptExtractionService();
            var cueDetector = new FollowUpCueDetector();
            var boundaryDetector = new TopicBoundaryDetector(cueDetector);
            CurrentConversation = new CurrentConversationMemoryService(
                ConversationStore,
                extractor,
                boundaryDetector,
                new ActiveConceptMerger());
            MemoryWriter = new MemoryWriter(
                MemoryStore,
                ConceptStore,
                extractor,
                new ExplicitMemoryRequestDetector(),
                new MemoryTypeClassifier());
            TopicClosing = new TopicClosingService(
                CurrentConversation,
                ConversationStore,
                MemoryStore,
                ConceptStore,
                MemoryWriter,
                new TopicSummaryBuilder(),
                new TopicImportanceScorer());
            Retriever = new AssociativeRetriever(
                MemoryStore,
                ConceptStore,
                extractor,
                new ConceptGraphActivationService(ConceptStore));
            PromptCompiler = new PromptCompiler(
                CurrentConversation,
                PromptStore,
                ConceptStore,
                new TokenBudgetService(new SimpleTokenEstimator()));
            Orchestrator = new MemoryOrchestrator(
                CurrentConversation,
                MemoryWriter,
                TopicClosing,
                Retriever,
                PromptCompiler,
                NullLogger<MemoryOrchestrator>.Instance);
            DebugService = new MemoryDebugService(
                CurrentConversation,
                MemoryStore,
                ConceptStore,
                PromptStore,
                Retriever,
                PromptCompiler);
        }

        public MerlinDbContext Db { get; }
        public EfMemoryStore MemoryStore { get; }
        public EfConceptStore ConceptStore { get; }
        public EfConversationStateStore ConversationStore { get; }
        public EfPromptCompilationStore PromptStore { get; }
        public CurrentConversationMemoryService CurrentConversation { get; }
        public MemoryWriter MemoryWriter { get; }
        public TopicClosingService TopicClosing { get; }
        public AssociativeRetriever Retriever { get; }
        public PromptCompiler PromptCompiler { get; }
        public MemoryOrchestrator Orchestrator { get; }
        public MemoryDebugService DebugService { get; }

        public static async Task<MemoryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MerlinDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new MerlinDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new MemoryFixture(connection, db);
        }

        public async Task<MemoryRecord> SaveMemoryAsync(
            string type,
            string title,
            string content,
            IReadOnlyCollection<string> concepts)
        {
            var now = DateTimeOffset.UtcNow;
            var memory = new MemoryRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                MemoryType = type,
                Title = title,
                Content = content,
                Summary = content,
                Project = "Merlin",
                Importance = 0.9,
                Confidence = 0.95,
                UserConfirmed = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            await MemoryStore.SaveMemoryAsync(memory);
            foreach (var conceptName in concepts)
            {
                var concept = await ConceptStore.GetOrCreateConceptAsync(conceptName);
                await ConceptStore.LinkMemoryToConceptAsync(memory.Id, concept.Id, 1.0);
            }

            return memory;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
