using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.Acknowledgement;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AcknowledgementIntegrationTests
{
    [Fact]
    public async Task RouteAsync_WhenDeepInfraBoundRequest_StartsAcknowledgementWithoutBlockingMainTask()
    {
        var playback = new RecordingSpeechPlaybackService();
        var tool = new DelayedTool(TimeSpan.FromMilliseconds(80));
        var router = CreateRouter(tool, playback);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var responseTask = router.RouteAsync(new AssistantRequest
        {
            Message = "Explain the tradeoffs of local-first memory architecture.",
            CorrelationId = "ack-test",
            InteractionSource = "voice_stream",
            SpeechEventSender = (_, _) => Task.CompletedTask,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });

        await Task.Delay(20);

        Assert.NotEmpty(playback.Requests);
        var response = await responseTask;
        stopwatch.Stop();

        Assert.True(response.Success);
        Assert.Equal("Final answer.", response.Message);
        Assert.True(stopwatch.ElapsedMilliseconds >= 70);
        Assert.Contains(playback.Requests, request => request.Text.Contains("tradeoffs", StringComparison.OrdinalIgnoreCase)
            || request.Text.Contains("architecture", StringComparison.OrdinalIgnoreCase)
            || request.Text.Contains("thoughts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RouteAsync_WhenAcknowledgementPlaybackFails_FinalAnswerStillReturns()
    {
        var router = CreateRouter(new DelayedTool(TimeSpan.FromMilliseconds(10)), new ThrowingSpeechPlaybackService());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "Why do people fear change?",
            CorrelationId = "ack-failure-test",
            InteractionSource = "voice_stream",
            SpeechEventSender = (_, _) => Task.CompletedTask,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.True(response.Success);
        Assert.Equal("Final answer.", response.Message);
    }

    [Fact]
    public async Task RouteAsync_EnqueuesAcknowledgementAsCancelOnlyBeforePlayback()
    {
        var playback = new RecordingSpeechPlaybackService();
        var router = CreateRouter(new DelayedTool(TimeSpan.FromMilliseconds(40)), playback);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "Why do people fear change?",
            CorrelationId = "ack-type-test",
            InteractionSource = "voice_stream",
            SpeechEventSender = (_, _) => Task.CompletedTask,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        });

        Assert.True(response.Success);
        var acknowledgement = Assert.Single(playback.Requests.Where(request => request.ItemType == SpeechPlaybackItemType.Acknowledgement));
        Assert.True(acknowledgement.CancelOnlyBeforePlayback);
    }

    private static CommandRouter CreateRouter(ITool tool, IAssistantSpeechPlaybackService playback)
    {
        var acknowledgementOptions = Options.Create(new AcknowledgementSpeechOptions
        {
            FirstProgressAfterMs = 1000,
            SecondProgressAfterMs = 2000,
            LongWaitProgressAfterMs = 3000,
            PhraseCooldownSeconds = 0
        });
        var phraseLibrary = new AcknowledgementPhraseLibrary(acknowledgementOptions);

        return new CommandRouter(
            new FixedIntentParser(),
            new ToolRegistry([tool]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            acknowledgementPolicy: new AcknowledgementPolicy(phraseLibrary, acknowledgementOptions),
            acknowledgementSpeechService: new AcknowledgementSpeechService(
                playback,
                acknowledgementOptions,
                NullLogger<AcknowledgementSpeechService>.Instance),
            progressSpeechService: new RequestProgressSpeechService(
                phraseLibrary,
                playback,
                acknowledgementOptions,
                NullLogger<RequestProgressSpeechService>.Instance),
            llmOptions: Options.Create(new LlmOptions { Provider = "deepinfra" }));
    }

    private sealed class FixedIntentParser : IIntentParser
    {
        public Task<IntentParseResult> ParseAsync(string message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentParseResult
            {
                Intent = "general_conversation",
                NormalizedCommand = $"chat {message}",
                Confidence = 1,
                OriginalMessage = message,
                CapabilityId = "general_conversation",
                CapabilityName = "General Conversation"
            });
        }
    }

    private sealed class DelayedTool : ITool
    {
        private readonly TimeSpan _delay;

        public DelayedTool(TimeSpan delay)
        {
            _delay = delay;
        }

        public string Name => "General Conversation";

        public string Description => "Test tool.";

        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command)
        {
            return command.StartsWith("chat ", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken);
            return new ToolResult
            {
                Success = true,
                Message = "Final answer.",
                ToolName = Name,
                Intent = "general_conversation"
            };
        }
    }

    private sealed class RecordingSpeechPlaybackService : IAssistantSpeechPlaybackService
    {
        public List<SpeechRequest> Requests { get; } = [];

        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false)
        {
            Requests.Add(new SpeechRequest(text, correlationId, speechCacheKey, itemType, cancelOnlyBeforePlayback));
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingSpeechPlaybackService : IAssistantSpeechPlaybackService
    {
        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false)
        {
            throw new InvalidOperationException("Playback failed.");
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record SpeechRequest(
        string Text,
        string? CorrelationId,
        string? CacheKey,
        SpeechPlaybackItemType ItemType,
        bool CancelOnlyBeforePlayback);
}
