using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConversationSessionServiceTests
{
    [Fact]
    public void CurrentSession_WhenServiceIsCreated_HasNewSession()
    {
        var service = CreateService();

        var session = service.CurrentSession;

        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Empty(session.Messages);
        Assert.Equal(string.Empty, session.RunningSummary);
    }

    [Fact]
    public void AddMessage_StoresMessages()
    {
        var service = CreateService();

        service.AddUserMessage("Who are you?");
        service.AddAssistantMessage("I am Merlin.");

        var messages = service.GetRecentMessages();

        Assert.Equal(2, messages.Count);
        Assert.Equal("User", messages[0].Role);
        Assert.Equal("Who are you?", messages[0].Content);
        Assert.Equal("Assistant", messages[1].Role);
        Assert.Equal("I am Merlin.", messages[1].Content);
    }

    [Fact]
    public void GetRecentMessages_ReturnsRequestedRecentMessages()
    {
        var service = CreateService();
        for (var index = 1; index <= 5; index++)
        {
            service.AddUserMessage($"message {index}");
        }

        var messages = service.GetRecentMessages(2);

        Assert.Equal(2, messages.Count);
        Assert.Equal("message 4", messages[0].Content);
        Assert.Equal("message 5", messages[1].Content);
    }

    [Fact]
    public void AddMessage_WhenLimitExceeded_CompactsOldMessagesIntoSummary()
    {
        var service = CreateService();

        for (var index = 1; index <= 21; index++)
        {
            service.AddUserMessage($"message {index}");
        }

        var session = service.CurrentSession;

        Assert.True(session.Messages.Count <= 20);
        Assert.Equal(11, session.Messages.Count);
        Assert.Contains("message 1", session.RunningSummary);
        Assert.Contains("message 10", session.RunningSummary);
        Assert.Equal("message 11", session.Messages[0].Content);
        Assert.Equal("message 21", session.Messages[^1].Content);
    }

    [Fact]
    public void UpdateRunningSummary_StoresSummary()
    {
        var service = CreateService();

        service.UpdateRunningSummary("User worked on Merlin backend.");

        Assert.Equal("User worked on Merlin backend.", service.CurrentSession.RunningSummary);
    }

    [Fact]
    public void ClearSession_CreatesNewEmptySession()
    {
        var service = CreateService();
        var oldSessionId = service.CurrentSession.SessionId;
        service.AddUserMessage("hello");

        service.ClearSession();

        var session = service.CurrentSession;
        Assert.NotEqual(oldSessionId, session.SessionId);
        Assert.Empty(session.Messages);
        Assert.Equal(string.Empty, session.RunningSummary);
    }

    [Fact]
    public void FinalizeCurrentSession_SavesSummaryAndClearsSession()
    {
        var summaryStore = new FakeConversationSummaryStore();
        var service = CreateService(summaryStore);
        service.UpdateRunningSummary("User worked on Merlin backend.");
        service.AddUserMessage("We added LocalAI intent parsing.");
        service.AddAssistantMessage("Trusted command persistence was implemented.");

        var summary = service.FinalizeCurrentSession();

        Assert.NotNull(summary);
        Assert.Equal("Merlin Backend Development", summary.Title);
        Assert.Contains("backend", summary.Tags);
        Assert.Contains("local-ai", summary.Tags);
        Assert.Single(summaryStore.GetAll());
        Assert.Empty(service.CurrentSession.Messages);
        Assert.Equal(string.Empty, service.CurrentSession.RunningSummary);
    }

    [Fact]
    public void FinalizeCurrentSession_WhenSessionIsEmpty_ReturnsNullAndCreatesFreshSession()
    {
        var service = CreateService();
        var oldSessionId = service.CurrentSession.SessionId;

        var summary = service.FinalizeCurrentSession();

        Assert.Null(summary);
        Assert.NotEqual(oldSessionId, service.CurrentSession.SessionId);
    }

    private static ConversationSessionService CreateService(
        IConversationSummaryStore? summaryStore = null)
    {
        return new ConversationSessionService(summaryStore ?? new FakeConversationSummaryStore());
    }
}
