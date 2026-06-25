using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LiveSpokenAnswerTrackingServiceTests
{
    [Fact]
    public void TrackingDisabled_NoOpsWithoutMutatingTracker()
    {
        var tracker = new SpokenAnswerTracker();
        var service = CreateService(tracker, enabled: false);

        service.StartAnswer("turn-1", "corr-1", "Why does pool water look blue?", "Pool water looks blue.");
        service.MarkChunkStarted("turn-1", "Pool water looks");
        service.MarkChunkCompleted("turn-1", "Pool water looks blue.");
        service.MarkPlaybackCancelled("turn-1", "floor_yield");
        service.CompleteAnswer("turn-1");

        Assert.Null(tracker.GetState("turn-1"));
        Assert.Null(service.TryCreateCheckpoint("turn-1"));
    }

    [Fact]
    public void StartAnswer_WhenEnabled_CreatesState()
    {
        var tracker = new SpokenAnswerTracker();
        var service = CreateService(tracker);

        service.StartAnswer(
            "turn-1",
            "corr-1",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons.",
            "pool color");

        var state = tracker.GetState("turn-1");

        Assert.NotNull(state);
        Assert.Equal("turn-1", state!.TurnId);
        Assert.Equal("corr-1", state.CorrelationId);
        Assert.Equal("Why does pool water look blue?", state.OriginalUserQuestion);
        Assert.Equal("Pool water can look blue for several reasons.", state.OriginalAssistantDraft);
        Assert.Equal("pool color", state.CurrentTopicLabel);
        Assert.True(state.CanRecompose);
    }

    [Fact]
    public void MarkChunkCompleted_UpdatesSpokenState()
    {
        var tracker = new SpokenAnswerTracker();
        var service = CreateStartedService(tracker);

        service.MarkChunkCompleted("turn-1", "Pool water can look blue for several reasons.");

        var state = tracker.GetState("turn-1");

        Assert.NotNull(state);
        Assert.Equal("Pool water can look blue for several reasons.", state!.SpokenSoFar);
        Assert.Equal("Pool water can look blue for several reasons.", state.LastCompletedSentence);
        Assert.Equal(string.Empty, state.CurrentPartialSentence);
    }

    [Fact]
    public void StartedThenCancelledChunk_RemainsUnsafePartial()
    {
        var tracker = new SpokenAnswerTracker();
        var service = CreateStartedService(tracker);
        service.MarkChunkCompleted("turn-1", "Pool water can look blue for several reasons.");

        service.MarkChunkStarted("turn-1", "Due to the color of the pool li");
        service.MarkPlaybackCancelled("turn-1", "floor_yield");
        var checkpoint = service.TryCreateCheckpoint("turn-1");

        Assert.NotNull(checkpoint);
        Assert.Equal("Pool water can look blue for several reasons.", checkpoint!.SafeSpokenPrefix);
        Assert.Equal("Pool water can look blue for several reasons.", checkpoint.LastCompletedSentence);
        Assert.Equal("Due to the color of the pool li", checkpoint.DiscardedPartialSentence);
    }

    [Fact]
    public void CancelledChunk_IsNotMarkedCompleted()
    {
        var tracker = new SpokenAnswerTracker();
        var service = CreateStartedService(tracker);

        service.MarkChunkStarted("turn-1", "Due to the color of the pool li");
        service.MarkPlaybackCancelled("turn-1", "floor_yield");

        var state = tracker.GetState("turn-1");

        Assert.NotNull(state);
        Assert.Equal(string.Empty, state!.SpokenSoFar);
        Assert.Equal("Due to the color of the pool li", state.CurrentPartialSentence);
    }

    [Fact]
    public void CompleteAnswer_ClearsState()
    {
        var tracker = new SpokenAnswerTracker();
        var service = CreateStartedService(tracker);

        service.CompleteAnswer("turn-1");

        Assert.Null(tracker.GetState("turn-1"));
        Assert.Null(service.TryCreateCheckpoint("turn-1"));
    }

    [Fact]
    public void MainAnswerTrackingGuard_IgnoresFeedbackProgressAndToolSpeech()
    {
        var request = new AssistantRequest { Message = "Why does pool water look blue?" };

        Assert.True(WebSocketHandler.ShouldTrackMainAnswerSpeech(
            request,
            new AssistantResponse
            {
                Success = true,
                Message = "Pool water can look blue.",
                CorrelationId = "corr-1",
                ToolName = "General Conversation",
                Intent = "general_conversation",
                ResponseType = "assistant"
            }));

        Assert.False(WebSocketHandler.ShouldTrackMainAnswerSpeech(
            request,
            new AssistantResponse
            {
                Success = true,
                Message = "I am checking that.",
                CorrelationId = "corr-1",
                SpeechCacheKey = "deepinfra_progress_01",
                ToolName = "General Conversation",
                Intent = "general_conversation",
                ResponseType = "assistant"
            }));

        Assert.False(WebSocketHandler.ShouldTrackMainAnswerSpeech(
            request,
            new AssistantResponse
            {
                Success = true,
                Message = "Opening Notepad.",
                CorrelationId = "corr-1",
                ToolName = "Open Application",
                Intent = "open_application",
                ResponseType = "assistant"
            }));
    }

    private static LiveSpokenAnswerTrackingService CreateStartedService(SpokenAnswerTracker tracker)
    {
        var service = CreateService(tracker);
        service.StartAnswer(
            "turn-1",
            "corr-1",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons. Due to the color of the pool liner, it appears blue.");
        return service;
    }

    private static LiveSpokenAnswerTrackingService CreateService(
        SpokenAnswerTracker tracker,
        bool enabled = true) =>
        new(
            tracker,
            Options.Create(new InterruptionHandlingOptions
            {
                EnableLiveSpokenAnswerTracking = enabled,
                EnableSpokenAnswerTrackingDiagnostics = true
            }),
            NullLogger<LiveSpokenAnswerTrackingService>.Instance);
}
