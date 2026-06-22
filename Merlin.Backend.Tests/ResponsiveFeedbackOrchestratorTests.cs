using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.Acknowledgement;
using Merlin.Backend.Services.Feedback;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ResponsiveFeedbackOrchestratorTests
{
    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenCardSelected_QueuesAcknowledgementSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("external_open_01", "Opening that.")),
            playback);

        var result = await orchestrator.TryEmitImmediateFeedbackAsync(
            Context(),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.True(result.Emitted);
        Assert.Equal("external_open_01", result.CardId);
        Assert.Equal("Opening that.", result.Text);
        var request = Assert.Single(playback.Requests);
        Assert.Equal("Opening that.", request.Text);
        Assert.Equal("feedback:external_open_01", request.CacheKey);
        Assert.True(request.IsReplayableSpeech);
        Assert.Equal(SpeechPlaybackItemType.Acknowledgement, request.ItemType);
        Assert.True(request.CancelOnlyBeforePlayback);
    }

    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenSelectorReturnsNull_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(new FixedSelector(null), playback);

        var result = await orchestrator.TryEmitImmediateFeedbackAsync(
            Context(),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("no_selection", result.Reason);
        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenDisabled_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("external_open_01", "Opening that.")),
            playback,
            new ResponsiveFeedbackOptions { Enabled = false });

        var result = await orchestrator.TryEmitImmediateFeedbackAsync(
            Context(),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("disabled", result.Reason);
        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenSpeechDisallowed_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("external_open_01", "Opening that.")),
            playback);

        var result = await orchestrator.TryEmitImmediateFeedbackAsync(
            Context(allowSpeech: false),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("speech_not_allowed", result.Reason);
        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenPlaybackThrows_DoesNotThrow()
    {
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("external_open_01", "Opening that.")),
            new ThrowingPlaybackService());

        var result = await orchestrator.TryEmitImmediateFeedbackAsync(
            Context(),
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("queue_failed", result.Reason);
    }

    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenMainResponseReadyBeforeDelay_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("external_open_01", "Opening that.")),
            playback,
            new ResponsiveFeedbackOptions
            {
                ImmediateFeedbackDelayMs = 25,
                SuppressIfMainResponseReady = true
            });
        var context = Context();

        var task = orchestrator.TryEmitImmediateFeedbackAsync(
            context,
            (_, _) => Task.CompletedTask,
            CancellationToken.None);
        orchestrator.MarkMainResponseReady(context.CorrelationId);
        var result = await task;

        Assert.False(result.Emitted);
        Assert.Equal("main_response_ready", result.Reason);
        Assert.Empty(playback.Requests);
        Assert.False(orchestrator.WasImmediateFeedbackEmitted(context.CorrelationId));
    }

    [Fact]
    public async Task TryEmitImmediateFeedbackAsync_WhenQueued_TracksImmediateEmission()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("external_open_01", "Opening that.")),
            playback);
        var context = Context();

        var result = await orchestrator.TryEmitImmediateFeedbackAsync(
            context,
            (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.True(result.Emitted);
        Assert.True(orchestrator.WasImmediateFeedbackEmitted(context.CorrelationId));
    }

    [Fact]
    public async Task TryEmitInterruptionBridgeAsync_WhenCardSelected_QueuesInterruptionCard()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("interruption_recompose_01", "Good point, let me include that.")),
            playback);

        var result = await orchestrator.TryEmitInterruptionBridgeAsync(
            InterruptionContext(),
            CancellationToken.None);

        Assert.True(result.Emitted);
        Assert.Equal("interruption_recompose_01", result.CardId);
        var request = Assert.Single(playback.Requests);
        Assert.Equal("Good point, let me include that.", request.Text);
        Assert.Equal("feedback:interruption_recompose_01", request.CacheKey);
        Assert.True(request.IsReplayableSpeech);
        Assert.Equal(SpeechPlaybackItemType.Acknowledgement, request.ItemType);
    }

    [Fact]
    public async Task TryEmitInterruptionBridgeAsync_WhenNonInterruptionContext_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("interruption_recompose_01", "Good point, let me include that.")),
            playback);

        var result = await orchestrator.TryEmitInterruptionBridgeAsync(
            Context(),
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("not_interruption_context", result.Reason);
        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task TryEmitInterruptionBridgeAsync_WhenSpeechDisabled_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("interruption_recompose_01", "Good point, let me include that.")),
            playback,
            new ResponsiveFeedbackOptions
            {
                EnableSpeechFeedback = false,
                ImmediateFeedbackDelayMs = 0,
                GlobalCooldownMs = 0
            });

        var result = await orchestrator.TryEmitInterruptionBridgeAsync(
            InterruptionContext(),
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("disabled", result.Reason);
        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task TryEmitInterruptionBridgeAsync_WhenContextDisallowsSpeech_DoesNotQueueSpeech()
    {
        var playback = new RecordingPlaybackService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("interruption_recompose_01", "Good point, let me include that.")),
            playback);

        var result = await orchestrator.TryEmitInterruptionBridgeAsync(
            InterruptionContext(allowSpeech: false),
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("speech_not_allowed", result.Reason);
        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task TryEmitInterruptionBridgeAsync_WhenPlaybackThrows_DoesNotThrow()
    {
        var orchestrator = CreateOrchestrator(
            new FixedSelector(Card("interruption_recompose_01", "Good point, let me include that.")),
            new ThrowingPlaybackService());

        var result = await orchestrator.TryEmitInterruptionBridgeAsync(
            InterruptionContext(),
            CancellationToken.None);

        Assert.False(result.Emitted);
        Assert.Equal("queue_failed", result.Reason);
    }

    [Fact]
    public void StartProgressFeedback_DelegatesToExistingProgressService()
    {
        var progress = new RecordingProgressSpeechService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(null),
            new RecordingPlaybackService(),
            progressService: progress);

        orchestrator.StartProgressFeedback(
            Context(),
            new RequestProgressSpeechRequest
            {
                RequestId = "request-1",
                CorrelationId = "correlation-1",
                CommandReceivedAtUtc = DateTimeOffset.UtcNow,
                Decision = AcknowledgementDecision.Skipped(
                    "test",
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(3),
                    0,
                    RequestProgressState.StillWorking),
                SendEventAsync = (_, _) => Task.CompletedTask
            },
            CancellationToken.None);

        Assert.Equal(1, progress.StartCount);
    }

    [Fact]
    public void SuppressNormalProgressForTurn_PreventsProgressForThatTurn()
    {
        var progress = new RecordingProgressSpeechService();
        var orchestrator = CreateOrchestrator(
            new FixedSelector(null),
            new RecordingPlaybackService(),
            progressService: progress);

        orchestrator.SuppressNormalProgressForTurn("turn-suppressed", "interruption_bridge");

        var handle = orchestrator.StartProgressFeedback(
            Context(turnId: "turn-suppressed"),
            ProgressRequest("turn-suppressed"),
            CancellationToken.None);

        Assert.Null(handle);
        Assert.Equal(0, progress.StartCount);
    }

    private static ResponsiveFeedbackOrchestrator CreateOrchestrator(
        IFeedbackSelector selector,
        IAssistantSpeechPlaybackService playback,
        ResponsiveFeedbackOptions? options = null,
        IRequestProgressSpeechService? progressService = null)
    {
        return new ResponsiveFeedbackOrchestrator(
            selector,
            playback,
            progressService ?? new RecordingProgressSpeechService(),
            Options.Create(options ?? new ResponsiveFeedbackOptions
            {
                ImmediateFeedbackDelayMs = 0,
                GlobalCooldownMs = 0
            }),
            NullLogger<ResponsiveFeedbackOrchestrator>.Instance);
    }

    private static FeedbackContext Context(bool allowSpeech = true, string? turnId = null)
    {
        return new FeedbackContext
        {
            CorrelationId = "correlation-1",
            TurnId = turnId ?? "correlation-1",
            Phase = FeedbackPhase.Executing,
            Domain = FeedbackDomain.ExternalApp,
            AllowSpeech = allowSpeech
        };
    }

    private static FeedbackContext InterruptionContext(bool allowSpeech = true)
    {
        return new FeedbackContext
        {
            CorrelationId = "interruption-correlation-1",
            TurnId = "interruption-turn-1",
            Phase = FeedbackPhase.RecomposingContinuation,
            Domain = FeedbackDomain.Interruption,
            IsInterruptionFeedback = true,
            IsRecompositionFeedback = true,
            AllowSpeech = allowSpeech,
            Tags = ["recompose"]
        };
    }

    private static RequestProgressSpeechRequest ProgressRequest(string correlationId)
    {
        return new RequestProgressSpeechRequest
        {
            RequestId = correlationId,
            CorrelationId = correlationId,
            CommandReceivedAtUtc = DateTimeOffset.UtcNow,
            Decision = AcknowledgementDecision.Skipped(
                "test",
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3),
                0,
                RequestProgressState.StillWorking),
            SendEventAsync = (_, _) => Task.CompletedTask
        };
    }

    private static FeedbackCard Card(string id, string text)
    {
        return new FeedbackCard
        {
            Id = id,
            Text = text,
            InterruptibleBeforePlayback = true,
            IsReplayableSpeech = true
        };
    }

    private sealed class FixedSelector : IFeedbackSelector
    {
        private readonly FeedbackCard? _card;

        public FixedSelector(FeedbackCard? card)
        {
            _card = card;
        }

        public FeedbackSelection? Select(FeedbackContext context)
        {
            return _card is null
                ? null
                : new FeedbackSelection
                {
                    Card = _card,
                    Score = 1,
                    Reason = "test"
                };
        }
    }

    private sealed class RecordingPlaybackService : IAssistantSpeechPlaybackService
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

    private sealed class ThrowingPlaybackService : IAssistantSpeechPlaybackService
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
            throw new InvalidOperationException("boom");
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingProgressSpeechService : IRequestProgressSpeechService
    {
        public int StartCount { get; private set; }

        public IRequestProgressSpeechHandle Start(
            RequestProgressSpeechRequest request,
            CancellationToken cancellationToken)
        {
            StartCount++;
            return new NoOpProgressHandle();
        }
    }

    private sealed class NoOpProgressHandle : IRequestProgressSpeechHandle
    {
        public void MarkMainResponseReady()
        {
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record SpeechRequest(
        string Text,
        string? CacheKey,
        bool? IsReplayableSpeech,
        SpeechPlaybackItemType ItemType,
        bool CancelOnlyBeforePlayback);
}
