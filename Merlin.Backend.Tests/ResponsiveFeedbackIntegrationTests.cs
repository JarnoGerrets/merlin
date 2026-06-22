using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.Acknowledgement;
using Merlin.Backend.Services.Feedback;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ResponsiveFeedbackIntegrationTests
{
    [Fact]
    public async Task RouteAsync_WhenResponsiveFeedbackEnabled_UsesFeedbackCardForImmediateSpeech()
    {
        var playback = new RecordingSpeechPlaybackService();
        var router = CreateRouter(playback, responsiveFeedbackEnabled: true);

        var response = await router.RouteAsync(Request());

        Assert.True(response.Success);
        var acknowledgement = Assert.Single(playback.Requests.Where(request => request.ItemType == SpeechPlaybackItemType.Acknowledgement));
        Assert.Equal("Opening that.", acknowledgement.Text);
        Assert.Equal("feedback:external_open_01", acknowledgement.CacheKey);
        Assert.True(acknowledgement.IsReplayableSpeech);
        Assert.True(acknowledgement.CancelOnlyBeforePlayback);
        Assert.True(response.SuppressSpeech);
    }

    [Fact]
    public async Task RouteAsync_WhenResponsiveFeedbackDisabled_FallsBackToOldAcknowledgement()
    {
        var playback = new RecordingSpeechPlaybackService();
        var router = CreateRouter(playback, responsiveFeedbackEnabled: false);

        var response = await router.RouteAsync(Request());

        Assert.True(response.Success);
        Assert.False(response.SuppressSpeech);
        var acknowledgement = Assert.Single(playback.Requests.Where(request => request.ItemType == SpeechPlaybackItemType.Acknowledgement));
        Assert.NotNull(acknowledgement.CacheKey);
        Assert.StartsWith("local_system_tool_", acknowledgement.CacheKey, StringComparison.Ordinal);
        Assert.NotEqual("feedback:external_open_01", acknowledgement.CacheKey);
    }

    [Fact]
    public async Task RouteAsync_WhenAppOpenFailsAfterImmediateFeedback_DoesNotSuppressFinalSpeech()
    {
        var playback = new RecordingSpeechPlaybackService();
        var router = CreateRouter(playback, responsiveFeedbackEnabled: true, toolSucceeds: false);

        var response = await router.RouteAsync(Request());

        Assert.False(response.Success);
        Assert.False(response.SuppressSpeech);
        Assert.Single(playback.Requests.Where(request => request.ItemType == SpeechPlaybackItemType.Acknowledgement));
    }

    private static AssistantRequest Request()
    {
        return new AssistantRequest
        {
            Message = "open test app",
            CorrelationId = "responsive-feedback-test",
            InteractionSource = "voice_stream",
            SpeechEventSender = (_, _) => Task.CompletedTask,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static CommandRouter CreateRouter(
        RecordingSpeechPlaybackService playback,
        bool responsiveFeedbackEnabled,
        bool toolSucceeds = true)
    {
        var acknowledgementOptions = Options.Create(new AcknowledgementSpeechOptions
        {
            FirstProgressAfterMs = 1000,
            SecondProgressAfterMs = 2000,
            LongWaitProgressAfterMs = 3000,
            PhraseCooldownSeconds = 0
        });
        var responsiveOptions = Options.Create(new ResponsiveFeedbackOptions
        {
            Enabled = responsiveFeedbackEnabled,
            ImmediateFeedbackDelayMs = 0,
            GlobalCooldownMs = 0,
            SameTextCooldownSeconds = 0,
            MinimumSelectionScore = 0.5
        });
        var phraseLibrary = new AcknowledgementPhraseLibrary(acknowledgementOptions);
        var progressSpeechService = new RequestProgressSpeechService(
            phraseLibrary,
            playback,
            acknowledgementOptions,
            NullLogger<RequestProgressSpeechService>.Instance);
        var selector = new FeedbackSelector(
            new DefaultFeedbackCardProvider(),
            new FeedbackVectorBuilder(),
            new FeedbackCooldownTracker(responsiveOptions),
            responsiveOptions,
            NullLogger<FeedbackSelector>.Instance);
        var orchestrator = new ResponsiveFeedbackOrchestrator(
            selector,
            playback,
            progressSpeechService,
            responsiveOptions,
            NullLogger<ResponsiveFeedbackOrchestrator>.Instance);

        return new CommandRouter(
            new FixedIntentParser(),
            new ToolRegistry([new DelayedOpenTool(toolSucceeds)]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            acknowledgementPolicy: new AcknowledgementPolicy(phraseLibrary, acknowledgementOptions),
            acknowledgementSpeechService: new AcknowledgementSpeechService(
                playback,
                acknowledgementOptions,
                NullLogger<AcknowledgementSpeechService>.Instance),
            progressSpeechService: progressSpeechService,
            feedbackContextFactory: new FeedbackContextFactory(),
            responsiveFeedbackOrchestrator: orchestrator,
            responsiveFeedbackOptions: responsiveOptions);
    }

    private sealed class FixedIntentParser : IIntentParser
    {
        public Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentParseResult
            {
                Intent = "open_application",
                NormalizedCommand = "open test app",
                Confidence = 1,
                OriginalMessage = message,
                CapabilityId = "application_launch",
                CapabilityName = "Application Launch"
            });
        }
    }

    private sealed class DelayedOpenTool : ITool
    {
        private readonly bool _succeeds;

        public DelayedOpenTool(bool succeeds)
        {
            _succeeds = succeeds;
        }

        public string Name => "Open Application";

        public string Description => "Test app opener.";

        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command)
        {
            return string.Equals(command, "open test app", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            await Task.Delay(20, cancellationToken);
            return new ToolResult
            {
                Success = _succeeds,
                Message = _succeeds ? "Opened." : "Could not open.",
                ErrorCode = _succeeds ? null : "APP_OPEN_FAILED",
                ToolName = Name,
                Intent = "open_application"
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
            Requests.Add(new SpeechRequest(text, speechCacheKey, isReplayableSpeech, itemType, cancelOnlyBeforePlayback));
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record SpeechRequest(
        string Text,
        string? CacheKey,
        bool? IsReplayableSpeech,
        SpeechPlaybackItemType ItemType,
        bool CancelOnlyBeforePlayback);
}
