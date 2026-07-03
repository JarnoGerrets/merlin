using Merlin.Backend.Core.Conversation;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Infrastructure.Persistence.Repositories;
using System.Text.Json;
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
    public void TopicSummarySanitizer_RemovesRecursiveUserDiscussedPrefix()
    {
        var sanitized = TopicSummarySanitizer.SanitizeForSession(
            "User discussed User discussed general conversation: that is the meaning of life.");

        Assert.DoesNotContain("User discussed User discussed", sanitized);
        Assert.Contains("meaning of life", sanitized);
    }

    [Fact]
    public void TopicSummarySanitizer_CollapsesRepeatedGeneralConversationLabels()
    {
        var sanitized = TopicSummarySanitizer.SanitizeForSession(
            "general conversation: general conversation: that is the meaning of life.");

        Assert.DoesNotContain("general conversation: general conversation:", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("General conversation about the meaning of life.", sanitized);
    }

    [Fact]
    public void TopicSummarySanitizer_DropsAssistantAnswerContamination()
    {
        var sanitized = TopicSummarySanitizer.SanitizeForSession(
            "general conversation: that is the meaning of life. general conversation: Since this is a deeply personal and philosophical topic, many people find meaning in relationships.");

        Assert.Equal("General conversation about the meaning of life.", sanitized);
        Assert.DoesNotContain("Since this is", sanitized);
        Assert.DoesNotContain("many people find meaning", sanitized);
    }

    [Theory]
    [InlineData("and when you ' re talking .", "It seems like your message might be incomplete. Could you clarify?", "low_value_or_partial_title")]
    [InlineData("and when you're talking", "It seems like your message might be incomplete. Could you clarify?", "low_value_or_partial_title")]
    [InlineData("because", "The message was incomplete and needed clarification.", "low_value_or_partial_title")]
    [InlineData("when you", "The message was incomplete and needed clarification.", "low_value_or_partial_title")]
    [InlineData("general conversation", "General conversation about Since this is a deeply personal topic.", "low_value_or_partial_title")]
    [InlineData("Merlin memory", "General conversation about Since this is a deeply personal topic.", "polluted_or_assistant_contaminated_summary")]
    [InlineData("what is the meaning of life?", "General conversation about the meaning of life.", "generic_conversation_without_working_context")]
    [InlineData("that is the meaning of life.", "General conversation about the meaning of life.", "generic_conversation_without_working_context")]
    public void MediumMemoryQualityGate_RejectsLowValueGenericOrPollutedEpisodes(
        string title,
        string summary,
        string expectedReason)
    {
        var result = MediumMemoryQualityGate.EvaluateTopicClose(new MediumMemoryQualityInput
        {
            Title = title,
            Summary = summary,
            Concepts = []
        });

        Assert.Equal(MediumMemoryQualityDecision.Skip, result.Decision);
        Assert.Equal(expectedReason, result.Reason);
    }

    [Theory]
    [InlineData("memory refactor", "Memory refactor added lifecycle and retrieval hygiene.")]
    [InlineData("PromptBlock compiler", "PromptBlock compiler keeps profile facts before retrieved memory.")]
    [InlineData("ResponsiveFeedback PR 2", "ResponsiveFeedback PR 2 improved interruption diagnostics.")]
    [InlineData("trusted_registry DB refactor", "trusted_registry DB refactor kept local schema decisions compact.")]
    public void MediumMemoryQualityGate_AcceptsUsefulProjectWorkingContext(string title, string summary)
    {
        var result = MediumMemoryQualityGate.EvaluateTopicClose(new MediumMemoryQualityInput
        {
            Title = title,
            Summary = summary,
            Concepts = ["Merlin"]
        });

        Assert.Equal(MediumMemoryQualityDecision.SaveActive, result.Decision);
        Assert.Equal("useful_recent_working_context", result.Reason);
        Assert.False(string.IsNullOrWhiteSpace(result.Category));
    }

    [Fact]
    public async Task PromptCompiler_RendersCompactCleanCurrentTopic()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var state = await fixture.CurrentConversation.ApplyUserMessageAsync("what is the meaning of life?");
        const string messySummary =
            "User discussed general conversation: and when you ' re talking . Assistant response touched on User discussed general conversation: It seems like your message might be incomplete. Could you please clarify or provide more context about what you're asking? User discussed User discussed general conversation: that is the meaning of life .";
        await fixture.ConversationStore.UpdateTopicSummaryAsync(state.ActiveTopicId!, messySummary);

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "continue",
            PromptType = "normal_conversation",
            RetrievedMemories = []
        });
        var sessionBlock = Assert.Single(result.Blocks, block => block.Type == PromptBlockTypes.SessionMemory);

        Assert.True(sessionBlock.Content.Length <= TopicSummarySanitizer.MaxSessionMemoryCharacters);
        Assert.DoesNotContain("User discussed User discussed", sessionBlock.Content);
        Assert.DoesNotContain("Could you please clarify", sessionBlock.Content);
        Assert.DoesNotContain("message might be incomplete", sessionBlock.Content);
        Assert.Contains("meaning of life", sessionBlock.Content);
    }

    [Fact]
    public async Task PromptCompiler_CleansObservedRepeatedLabelTopic_InBlocksAndRenderedPrompt()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var state = await fixture.CurrentConversation.ApplyUserMessageAsync("what is the meaning of life?");
        const string messySummary =
            "general conversation: general conversation: that is the meaning of life. general conversation: Since this is a deeply personal and philosophical topic, many people find meaning in relationships.";
        await fixture.ConversationStore.UpdateTopicSummaryAsync(state.ActiveTopicId!, messySummary);

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "\"that is the meaning of life .\"",
            PromptType = "normal_conversation",
            RetrievedMemories = []
        });
        var sessionBlock = Assert.Single(result.Blocks, block => block.Type == PromptBlockTypes.SessionMemory);

        Assert.Equal("General conversation about the meaning of life.", sessionBlock.Content);
        Assert.DoesNotContain("general conversation: general conversation:", sessionBlock.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Since this is", sessionBlock.Content);
        Assert.DoesNotContain("many people find meaning", sessionBlock.Content);
        Assert.Contains("CURRENT TOPIC:", result.CompiledPrompt);
        Assert.Contains("General conversation about the meaning of life.", result.CompiledPrompt);
        Assert.DoesNotContain("general conversation: general conversation:", result.CompiledPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Since this is", result.CompiledPrompt);
        Assert.DoesNotContain("many people find meaning", result.CompiledPrompt);
    }

    [Fact]
    public async Task CurrentConversationMemory_DoesNotRestoreStaleActiveTopicAfterRuntimeRestart()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var conversation = await fixture.ConversationStore.GetOrCreateActiveConversationAsync();
        await fixture.ConversationStore.StartTopicAsync(conversation.Id, "General conversation about what is the meaning of life.");

        fixture.RestartRuntimeTopicSession();
        var state = await fixture.CurrentConversation.GetOrCreateCurrentStateAsync();

        Assert.Null(state.ActiveTopicId);
        Assert.Null(state.ActiveTopicTitle);
        Assert.Null(state.RecentSummary);
        Assert.False(state.ActiveTopicTouchedInCurrentProcess);
    }

    [Fact]
    public async Task CurrentConversationMemory_CurrentUserMessageCreatesFreshRuntimeTopicAfterRestart()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var conversation = await fixture.ConversationStore.GetOrCreateActiveConversationAsync();
        await fixture.ConversationStore.StartTopicAsync(conversation.Id, "General conversation about what is the meaning of life.");

        fixture.RestartRuntimeTopicSession();
        var state = await fixture.CurrentConversation.ApplyUserMessageAsync("this is a test family car .");

        Assert.NotNull(state.ActiveTopicId);
        Assert.True(state.ActiveTopicTouchedInCurrentProcess);
        Assert.DoesNotContain("meaning of life", state.RecentSummary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("family", state.RecentSummary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PromptCompiler_OmitsStaleCurrentTopicAfterRuntimeRestart()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var conversation = await fixture.ConversationStore.GetOrCreateActiveConversationAsync();
        await fixture.ConversationStore.StartTopicAsync(conversation.Id, "General conversation about what is the meaning of life.");

        fixture.RestartRuntimeTopicSession();
        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "this is a test family car .",
            PromptType = "normal_conversation",
            RetrievedMemories = []
        });

        Assert.DoesNotContain("CURRENT TOPIC:", result.CompiledPrompt);
        Assert.DoesNotContain("meaning of life", result.CompiledPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CURRENT USER MESSAGE:", result.CompiledPrompt);
        Assert.Contains("this is a test family car .", result.CompiledPrompt);
    }

    [Fact]
    public async Task CurrentConversationMemory_IgnoresLowValueSttFragmentAndClarificationBoilerplate()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var conversation = await fixture.ConversationStore.GetOrCreateActiveConversationAsync();
        var seededTopic = await fixture.ConversationStore.StartTopicAsync(conversation.Id, "General conversation");
        fixture.RuntimeTopicSession.MarkTopicTouched(seededTopic.Id);
        var state = await fixture.CurrentConversation.GetOrCreateCurrentStateAsync();

        await fixture.CurrentConversation.ApplyUserMessageAsync("and when you're talking");
        await fixture.CurrentConversation.UpdateAfterAssistantResponseAsync("It seems like your message might be incomplete. Could you clarify?");
        var topic = await fixture.ConversationStore.GetTopicAsync(state.ActiveTopicId!);

        Assert.NotNull(topic);
        Assert.True(string.IsNullOrWhiteSpace(topic!.Summary));
    }

    [Fact]
    public async Task TopicClosing_ClarificationOnlyExchange_DoesNotCreateEpisodeMemory()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        await fixture.CurrentConversation.ApplyUserMessageAsync("and when you're talking");
        await fixture.CurrentConversation.UpdateAfterAssistantResponseAsync("It seems like your message might be incomplete. Could you clarify?");
        var close = await fixture.TopicClosing.CloseCurrentTopicAsync(TopicCloseReasons.TopicSwitch);
        var episodes = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            MemoryTypes = ["episode"],
            IncludeInactive = true,
            Limit = 10
        });

        Assert.False(close.Closed);
        Assert.Empty(episodes);
    }

    [Fact]
    public async Task TopicClosing_ProjectWorkExchange_CreatesCompactEpisodeMemory()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        await fixture.CurrentConversation.ApplyUserMessageAsync("Let's discuss Memory PR 3 retrieval hygiene and prompt dedupe.");
        await fixture.CurrentConversation.UpdateAfterAssistantResponseAsync("Memory PR 3 added active-only retrieval, stopword filtering, prompt dedupe, and topic-close duplicate guards.");
        var close = await fixture.TopicClosing.CloseCurrentTopicAsync(TopicCloseReasons.TopicSwitch);
        var memory = await fixture.MemoryStore.GetMemoryAsync(close.MediumMemoryId!);

        Assert.True(close.Closed, close.Reason);
        Assert.NotNull(memory);
        Assert.Equal("episode", memory!.MemoryType);
        Assert.Contains("Memory PR 3", memory.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User discussed User discussed", memory.Content);
        Assert.DoesNotContain("Could you please clarify", memory.Content);
        Assert.True(memory.Summary!.Length <= 500);
    }

    [Fact]
    public async Task TopicClosing_GenericQuestionAnswer_SkipsActiveMediumMemory()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        await fixture.CurrentConversation.ApplyUserMessageAsync("what is the meaning of life?");
        await fixture.CurrentConversation.UpdateAfterAssistantResponseAsync("There is no single answer, but many people find meaning in relationships and purpose.");
        var close = await fixture.TopicClosing.CloseCurrentTopicAsync(TopicCloseReasons.TopicSwitch);
        var episodes = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            MemoryTypes = ["episode"],
            IncludeInactive = true,
            Limit = 10
        });

        Assert.True(close.Closed, close.Reason);
        Assert.Null(close.MediumMemoryId);
        Assert.Contains("Skipped medium memory", close.Reason);
        Assert.Empty(episodes);
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
    public async Task MemoryStore_SearchMemories_DefaultsToActiveMemoriesOnly()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var active = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            MemoryType = "architecture_decision",
            Title = "Active lifecycle memory",
            Content = "lifecycle hygiene unique active memory",
            Summary = "lifecycle hygiene unique active memory",
            Status = MemoryStatuses.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        var archived = active with
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "Archived lifecycle memory",
            Status = MemoryStatuses.Archived,
            ArchivedAt = now
        };

        await fixture.MemoryStore.SaveMemoryAsync(active);
        await fixture.MemoryStore.SaveMemoryAsync(archived);

        var defaultResults = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = "lifecycle hygiene unique",
            Limit = 10
        });
        var includeInactiveResults = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = "lifecycle hygiene unique",
            IncludeInactive = true,
            Limit = 10
        });

        Assert.Contains(defaultResults, result => result.Memory.Id == active.Id);
        Assert.DoesNotContain(defaultResults, result => result.Memory.Id == archived.Id);
        Assert.Contains(includeInactiveResults, result => result.Memory.Id == active.Id);
        Assert.Contains(includeInactiveResults, result => result.Memory.Id == archived.Id);
    }

    [Fact]
    public void ProjectIdentifierNormalizer_NormalizesPrIdentifierVariants()
    {
        var identifiers = new[]
        {
            ProjectIdentifierNormalizer.ExtractIdentifiers("What was PR4 about?").Single(),
            ProjectIdentifierNormalizer.ExtractIdentifiers("What was PR 4 about?").Single(),
            ProjectIdentifierNormalizer.ExtractIdentifiers("What was PR-4 about?").Single()
        };

        Assert.All(identifiers, identifier => Assert.Equal("pr4", identifier));
        Assert.Contains("pr4", ProjectIdentifierNormalizer.NormalizeText("What was PR 4 about?"));
        Assert.Contains("pr4", ProjectIdentifierNormalizer.SearchVariants("PR-4"));
        Assert.Contains("pr 4", ProjectIdentifierNormalizer.SearchVariants("What was PR4 about?"));
        Assert.Empty(ProjectIdentifierNormalizer.ExtractIdentifiers("what was the thing about?"));
    }

    [Fact]
    public async Task AssociativeRetriever_FiltersStopwordKeywordReasons()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        await fixture.SaveMemoryAsync(
            "episode",
            "Meaning of life discussion",
            "The meaning of life conversation centered on curiosity and practical kindness.",
            []);

        var results = await fixture.Retriever.RetrieveAsync(new MemoryRetrievalRequest
        {
            Query = "what is the meaning of life",
            MaxResults = 5
        });

        var result = Assert.Single(results);
        Assert.Contains(result.MatchReasons, reason => reason.Contains("keyword: meaning", StringComparison.OrdinalIgnoreCase) || reason.Contains("keyword: life", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.MatchReasons, reason => reason.Contains("keyword: what", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("For Merlin, PR4 is about fail-closed memory.", "What was PR4 about?")]
    [InlineData("For Merlin, PR 4 is about fail-closed memory.", "What was PR4 about?")]
    [InlineData("For Merlin, PR-4 is about fail-closed memory.", "What was PR 4 about?")]
    public async Task AssociativeRetriever_RetrievesProjectIdentifierVariants(string savedSummary, string query)
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var memory = await fixture.SaveMemoryAsync(
            "episode",
            "Merlin / memory",
            savedSummary,
            ["Merlin", "memory"]);

        var results = await fixture.Retriever.RetrieveAsync(new MemoryRetrievalRequest
        {
            Query = query,
            MaxResults = 5
        });

        var result = Assert.Single(results, item => item.MemoryId == memory.Id);
        Assert.Contains(result.MatchReasons, reason => reason.Contains("keyword: pr4", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.MatchReasons, reason => reason.Contains("keyword: what", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MemoryStore_SearchMemories_SearchesCompactContentForProjectIdentifier()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var memory = new MemoryRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            MemoryType = "episode",
            Title = "Merlin / memory",
            Content = "The compact content carries the project identifier.",
            CompactContent = "For Merlin, PR 4 is about fail-closed memory.",
            Summary = "Project memory",
            Topic = "Merlin / memory",
            Project = "Merlin",
            Importance = 0.9,
            Confidence = 0.85,
            CreatedAt = now,
            UpdatedAt = now
        };
        await fixture.MemoryStore.SaveMemoryAsync(memory);

        var results = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = "PR4",
            MemoryTypes = ["episode"],
            Limit = 10
        });

        Assert.Contains(results, result => result.Memory.Id == memory.Id);
    }

    [Fact]
    public async Task PromptCompiler_IncludesPr4MediumMemory_WhenIdentifierRetrieved()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var memory = await fixture.SaveMemoryAsync(
            "episode",
            "Merlin / memory",
            "For Merlin, PR4 is about fail-closed memory.",
            ["Merlin", "memory"]);
        var retrieved = await fixture.Retriever.RetrieveAsync(new MemoryRetrievalRequest
        {
            Query = "What was PR4 about?",
            MaxResults = 5
        });

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "What was PR4 about?",
            PromptType = "normal_conversation",
            RetrievedMemories = retrieved
        });

        Assert.Contains(memory.Id, result.IncludedMemoryIds);
        Assert.Contains("RELEVANT MEDIUM MEMORY:", result.CompiledPrompt);
        Assert.Contains("fail-closed memory", result.CompiledPrompt);
        Assert.Contains("What was PR4 about?", result.CompiledPrompt);
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
    public async Task PromptCompiler_DedupesDuplicateMediumMemories_AndCleansRetrievalNotes()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        const string title = "Memory PR 3 retrieval hygiene";
        const string summary = "Memory PR 3 added active-only retrieval, stopword filtering, prompt dedupe, and topic-close duplicate guards.";
        var memories = Enumerable.Range(0, 6)
            .Select(index => new RetrievedMemory
            {
                MemoryId = $"episode-{index}",
                MemoryType = "episode",
                Title = title,
                Content = summary,
                Summary = summary,
                Score = 0.95 - (index * 0.01),
                MatchReasons = ["Matched keyword: what", "Matched keyword: memory"]
            })
            .ToList();

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "what did Memory PR 3 add?",
            PromptType = "normal_conversation",
            MaxInputTokens = 4000,
            MaxMemoryTokens = 4000,
            RetrievedMemories = memories
        });

        Assert.Equal(1, CountOccurrences(result.CompiledPrompt, title));
        var retrievalNotes = Assert.Single(result.Blocks, block => block.Type == PromptBlockTypes.RetrievalNotes);
        Assert.Contains("Suppressed 5 duplicate medium memories", retrievalNotes.Content);
        var promptLog = (await fixture.PromptStore.ListRecentPromptCompilationsAsync(1)).Single();
        Assert.Contains(PromptBlockTypes.RetrievalNotes, promptLog.CompiledBlocksJson);
        Assert.Contains("Suppressed 5 duplicate medium memories", promptLog.CompiledBlocksJson);
        Assert.DoesNotContain("RETRIEVAL NOTES:", result.CompiledPrompt);
        Assert.DoesNotContain("Suppressed 5 duplicate medium memories", result.CompiledPrompt);
        Assert.DoesNotContain("Matched keyword: what", result.CompiledPrompt);
        Assert.DoesNotContain("Matched keyword: memory", result.CompiledPrompt);
        Assert.Contains("what did Memory PR 3 add?", result.CompiledPrompt);
        Assert.Single(result.IncludedMemoryIds);
    }

    [Fact]
    public async Task PromptCompiler_KeepsRetrievalNotesInBlocks_ButNotCompiledPrompt()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "What was PR4 about?",
            PromptType = "normal_conversation",
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = "abc123",
                    MemoryType = "episode",
                    Title = "Merlin / memory",
                    Content = "For Merlin, PR4 is about fail-closed memory.",
                    Summary = "For Merlin, PR4 is about fail-closed memory.",
                    Score = 0.9,
                    MatchReasons = ["Matched keyword: pr4"]
                }
            ]
        });
        var promptLog = (await fixture.PromptStore.ListRecentPromptCompilationsAsync(1)).Single();

        var retrievalNotes = Assert.Single(result.Blocks, block => block.Type == PromptBlockTypes.RetrievalNotes);
        Assert.Contains("abc123", retrievalNotes.Content);
        Assert.Contains("Matched keyword: pr4", retrievalNotes.Content);
        Assert.Contains(PromptBlockTypes.RetrievalNotes, promptLog.CompiledBlocksJson);
        Assert.Contains("abc123", promptLog.CompiledBlocksJson);
        Assert.DoesNotContain("RETRIEVAL NOTES:", result.CompiledPrompt);
        Assert.DoesNotContain("abc123", result.CompiledPrompt);
        Assert.Contains("RELEVANT MEDIUM MEMORY:", result.CompiledPrompt);
        Assert.Contains("fail-closed memory", result.CompiledPrompt);
    }

    [Fact]
    public async Task PromptCompiler_FiltersOldBadActiveMediumMemory_BeforeRendering()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var bad = new RetrievedMemory
        {
            MemoryId = "old-bad-episode",
            MemoryType = "episode",
            Title = "and when you ' re talking .",
            Content = "Topic: and when you ' re talking .\n\nSummary:\nGeneral conversation about general conversation: that is the meaning of life. General conversation about Since this is a deeply personal and philosophical topic, many people find meaning in.\n\nKey concepts: general conversation\n\nOutcome: Closed because topic_switch.",
            Summary = "General conversation about general conversation: that is the meaning of life. General conversation about Since this is a deeply personal and philosophical topic, many people find meaning in.",
            Score = 0.99,
            MatchReasons = ["Matched keyword: meaning"]
        };

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "what is the meaning of life?",
            PromptType = "normal_conversation",
            RetrievedMemories = [bad]
        });

        Assert.DoesNotContain("RELEVANT MEDIUM MEMORY:", result.CompiledPrompt);
        Assert.DoesNotContain("and when you", result.CompiledPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("general conversation about general conversation", result.CompiledPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Since this is a deeply personal", result.CompiledPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("what is the meaning of life?", result.CompiledPrompt);
        Assert.Empty(result.IncludedMemoryIds);
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
    public async Task MemoryOrchestrator_ExplicitProfilePreferenceCreatesProfileFact_WithoutGenericMemory()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        var prepared = await fixture.Orchestrator.PrepareForModelCallAsync("I want short responses.", "test");
        var fact = await fixture.ProfileStore.GetActiveFactByKeyAsync(UserProfileDefaults.ProfileId, "response.length.default");
        var genericMemories = await fixture.MemoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = "short responses",
            Limit = 10
        });

        Assert.NotNull(fact);
        Assert.Equal("short", fact!.Value);
        Assert.Contains("short responses", prepared.LocalResponse);
        Assert.Empty(genericMemories);
    }

    [Fact]
    public async Task MemoryOrchestrator_ProfilePreferenceUpdateSupersedesPreviousActiveFact()
    {
        await using var fixture = await MemoryFixture.CreateAsync();

        await fixture.Orchestrator.PrepareForModelCallAsync("I want short responses.", "test");
        var updated = await fixture.Orchestrator.PrepareForModelCallAsync("I prefer medium to long responses.", "test");
        var active = await fixture.ProfileStore.GetActiveFactByKeyAsync(UserProfileDefaults.ProfileId, "response.length.default");
        var allFacts = await fixture.Db.UserProfileFacts.AsNoTracking().ToListAsync();

        Assert.Equal("medium_to_long", active!.Value);
        Assert.Contains("instead of short", updated.LocalResponse);
        Assert.Single(allFacts, fact => fact.Status == UserProfileFactStatuses.Active);
        Assert.Single(allFacts, fact => fact.Status == UserProfileFactStatuses.Superseded);
    }

    [Fact]
    public async Task PromptCompiler_IncludesActiveProfileFactsBeforeRetrievedMemory()
    {
        await using var fixture = await MemoryFixture.CreateAsync();
        var upsert = await fixture.ProfileService.UpsertAsync(new ProfileFactCandidate
        {
            Key = "response.style.conciseness",
            Category = "response_preferences",
            Value = "concise",
            DisplayText = "Jarno wants concise responses by default."
        });
        await fixture.ProfileStore.SaveFactAsync(new UserProfileFact
        {
            Id = Guid.NewGuid().ToString("N"),
            Key = "response.tone.default",
            Category = "response_preferences",
            Value = "verbose",
            DisplayText = "This superseded fact should not appear.",
            Status = UserProfileFactStatuses.Superseded,
            SourceType = UserProfileFactSourceTypes.ExplicitUserInstruction,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        var result = await fixture.PromptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = "How should we answer?",
            PromptType = "normal_conversation",
            RetrievedMemories =
            [
                new RetrievedMemory
                {
                    MemoryId = "memory-1",
                    MemoryType = "architecture_decision",
                    Title = "Memory",
                    Content = "Merlin uses SQLite memory.",
                    Summary = "Merlin uses SQLite memory.",
                    Score = 0.9
                }
            ]
        });

        Assert.Contains("USER PROFILE FACTS:", result.CompiledPrompt);
        Assert.Contains("RESPONSE PREFERENCES:", result.CompiledPrompt);
        Assert.Contains("Jarno wants concise responses by default.", result.CompiledPrompt);
        Assert.DoesNotContain("superseded fact", result.CompiledPrompt);
        Assert.True(result.CompiledPrompt.IndexOf("USER PROFILE FACTS:", StringComparison.Ordinal) <
            result.CompiledPrompt.IndexOf("RELEVANT LONG-TERM MEMORY:", StringComparison.Ordinal));
        Assert.Contains("CURRENT USER MESSAGE:", result.CompiledPrompt);
        Assert.Contains(result.Blocks, block => block.Type == PromptBlockTypes.ResponsePreferences);
        Assert.Contains(result.Blocks, block => block.Type == PromptBlockTypes.RelevantLongTermMemory);
        Assert.Equal(PromptBlockTypes.CurrentUserMessage, result.Blocks.OrderBy(block => block.SortOrder).Last().Type);
        Assert.Contains(upsert.ActiveFact.Id, result.IncludedProfileFactIds);
        Assert.True(result.Blocks.Single(block => block.Type == PromptBlockTypes.ResponsePreferences).SortOrder <
            result.Blocks.Single(block => block.Type == PromptBlockTypes.RelevantLongTermMemory).SortOrder);

        var promptLog = (await fixture.PromptStore.ListRecentPromptCompilationsAsync(1)).Single();
        Assert.False(string.IsNullOrWhiteSpace(promptLog.CompiledPrompt));
        Assert.False(string.IsNullOrWhiteSpace(promptLog.CompiledBlocksJson));
        Assert.False(string.IsNullOrWhiteSpace(promptLog.IncludedProfileFactIdsJson));
        Assert.Contains(upsert.ActiveFact.Id, JsonSerializer.Deserialize<IReadOnlyList<string>>(promptLog.IncludedProfileFactIdsJson!)!);
        Assert.Contains(PromptBlockTypes.ResponsePreferences, promptLog.CompiledBlocksJson);
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

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
            ProfileStore = new EfUserProfileFactStore(db, NullLogger<EfUserProfileFactStore>.Instance);
            ProfileService = new UserProfileFactService(ProfileStore);

            var extractor = new LocalConceptExtractionService();
            var cueDetector = new FollowUpCueDetector();
            var boundaryDetector = new TopicBoundaryDetector(cueDetector);
            RuntimeTopicSession = new RuntimeTopicSession(NullLogger<RuntimeTopicSession>.Instance);
            CurrentConversation = new CurrentConversationMemoryService(
                ConversationStore,
                extractor,
                boundaryDetector,
                new ActiveConceptMerger(),
                RuntimeTopicSession,
                NullLogger<CurrentConversationMemoryService>.Instance);
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
                new TokenBudgetService(new SimpleTokenEstimator()),
                RuntimeTopicSession,
                NullLogger<PromptCompiler>.Instance,
                ProfileStore);
            Orchestrator = new MemoryOrchestrator(
                CurrentConversation,
                MemoryWriter,
                TopicClosing,
                Retriever,
                PromptCompiler,
                NullLogger<MemoryOrchestrator>.Instance,
                new UserProfileFactDetector(),
                ProfileService);
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
        public EfUserProfileFactStore ProfileStore { get; }
        public UserProfileFactService ProfileService { get; }
        public RuntimeTopicSession RuntimeTopicSession { get; private set; }
        public CurrentConversationMemoryService CurrentConversation { get; private set; }
        public MemoryWriter MemoryWriter { get; }
        public TopicClosingService TopicClosing { get; }
        public AssociativeRetriever Retriever { get; }
        public PromptCompiler PromptCompiler { get; private set; }
        public MemoryOrchestrator Orchestrator { get; }
        public MemoryDebugService DebugService { get; }

        public void RestartRuntimeTopicSession()
        {
            RuntimeTopicSession = new RuntimeTopicSession(NullLogger<RuntimeTopicSession>.Instance);
            var extractor = new LocalConceptExtractionService();
            CurrentConversation = new CurrentConversationMemoryService(
                ConversationStore,
                extractor,
                new TopicBoundaryDetector(new FollowUpCueDetector()),
                new ActiveConceptMerger(),
                RuntimeTopicSession,
                NullLogger<CurrentConversationMemoryService>.Instance);
            PromptCompiler = new PromptCompiler(
                CurrentConversation,
                PromptStore,
                ConceptStore,
                new TokenBudgetService(new SimpleTokenEstimator()),
                RuntimeTopicSession,
                NullLogger<PromptCompiler>.Instance,
                ProfileStore);
        }

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
