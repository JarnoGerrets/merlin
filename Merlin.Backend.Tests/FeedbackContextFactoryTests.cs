using Merlin.Backend.Models;
using Merlin.Backend.Services.Feedback;
using Merlin.Backend.Tools;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class FeedbackContextFactoryTests
{
    [Fact]
    public void CreateInitial_MapsCorrelationTextAndVoiceFields()
    {
        var context = new FeedbackContextFactory().CreateInitial(
            new AssistantRequest
            {
                Message = "open app",
                InteractionSource = "voice_stream",
                ClientMode = "orb",
                SpeechEventSender = (_, _) => Task.CompletedTask
            },
            "correlation-1",
            "open app",
            DateTimeOffset.UtcNow);

        Assert.Equal("correlation-1", context.CorrelationId);
        Assert.Equal("correlation-1", context.TurnId);
        Assert.Equal("open app", context.NormalizedUserText);
        Assert.True(context.IsVoiceInteraction);
        Assert.True(context.IsOrbClient);
        Assert.True(context.AllowSpeech);
    }

    [Fact]
    public void EnrichWithRouting_WhenOpenApplication_MapsExternalApp()
    {
        var factory = new FeedbackContextFactory();
        var initial = Initial(factory);

        var context = factory.EnrichWithRouting(
            initial,
            Intent("open_application", "application_launch"),
            new FakeTool("Open Application"));

        Assert.Equal(FeedbackDomain.ExternalApp, context.Domain);
        Assert.True(context.IsExternalAction);
        Assert.Equal(FeedbackDurationEstimate.Short, context.DurationEstimate);
    }

    [Fact]
    public void EnrichWithRouting_WhenWebSearch_MapsWebSearch()
    {
        var factory = new FeedbackContextFactory();

        var context = factory.EnrichWithRouting(
            Initial(factory),
            Intent("web_search", "web_search"),
            new FakeTool("Web Search"));

        Assert.Equal(FeedbackDomain.WebSearch, context.Domain);
    }

    [Fact]
    public void EnrichWithRouting_WhenMemoryCapability_MapsMemory()
    {
        var factory = new FeedbackContextFactory();

        var context = factory.EnrichWithRouting(
            Initial(factory),
            Intent("missing_capability", "memory_lookup", "check memory"),
            null);

        Assert.Equal(FeedbackDomain.Memory, context.Domain);
    }

    [Fact]
    public void EnrichWithRouting_WhenUnknownTool_MapsLocalTool()
    {
        var factory = new FeedbackContextFactory();

        var context = factory.EnrichWithRouting(
            Initial(factory),
            Intent("unknown", "unknown"),
            new FakeTool("Custom Tool"));

        Assert.Equal(FeedbackDomain.LocalTool, context.Domain);
    }

    private static FeedbackContext Initial(FeedbackContextFactory factory)
    {
        return factory.CreateInitial(
            new AssistantRequest
            {
                Message = "test",
                InteractionSource = "voice_stream",
                SpeechEventSender = (_, _) => Task.CompletedTask
            },
            "correlation-1",
            "test",
            DateTimeOffset.UtcNow);
    }

    private static IntentParseResult Intent(
        string intent,
        string capabilityId,
        string normalizedCommand = "test")
    {
        return new IntentParseResult
        {
            Intent = intent,
            CapabilityId = capabilityId,
            NormalizedCommand = normalizedCommand,
            OriginalMessage = normalizedCommand,
            Confidence = 0.95
        };
    }

    private sealed class FakeTool : ITool
    {
        public FakeTool(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "Fake tool.";

        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command) => true;

        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ToolResult());
        }
    }
}
