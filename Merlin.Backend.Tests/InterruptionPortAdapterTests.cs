using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.Acknowledgement;
using Merlin.Backend.Services.Feedback;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class InterruptionPortAdapterTests
{
    [Fact]
    public async Task PlaybackPort_WhenPlaybackActionsDisabled_DoesNotCallPlaybackService()
    {
        var playback = new FakePlaybackService();
        var port = PlaybackPort(playback, new InterruptionHandlingOptions
        {
            Enabled = true,
            EnableLiveShadowMode = false,
            EnableLivePlaybackActions = false
        });

        await port.StopCurrentAsync("turn-1", "test");
        await port.CancelCurrentAsync("turn-1", "test");
        await port.PauseCurrentAsync("turn-1", "test");

        Assert.Equal(0, playback.StopCount);
        Assert.Equal(0, playback.ClearQueueCount);
        Assert.Equal(0, playback.PauseCount);
    }

    [Fact]
    public async Task PlaybackPort_WhenShadowModeEnabled_DoesNotCallPlaybackServiceEvenIfActionEnabled()
    {
        var playback = new FakePlaybackService();
        var port = PlaybackPort(playback, new InterruptionHandlingOptions
        {
            Enabled = true,
            EnableLiveShadowMode = true,
            EnableLivePlaybackActions = true
        });

        await port.StopCurrentAsync("turn-1", "test");

        Assert.Equal(0, playback.StopCount);
    }

    [Fact]
    public async Task PlaybackPort_WhenEnabledAndNotShadow_CallsPlaybackService()
    {
        var playback = new FakePlaybackService();
        var port = PlaybackPort(playback, new InterruptionHandlingOptions
        {
            Enabled = true,
            EnableLiveShadowMode = false,
            EnableLivePlaybackActions = true
        });

        await port.PauseCurrentAsync("turn-1", "test");
        await port.StopCurrentAsync("turn-1", "test");
        await port.CancelCurrentAsync("turn-1", "test");

        Assert.Equal(1, playback.PauseCount);
        Assert.Equal(2, playback.StopCount);
        Assert.Equal(1, playback.ClearQueueCount);
    }

    [Fact]
    public async Task FeedbackPort_WhenBridgeDisabled_DoesNotCallFeedbackServices()
    {
        var feedback = new FakeResponsiveFeedbackOrchestrator();
        var port = FeedbackPort(feedback, new InterruptionHandlingOptions
        {
            Enabled = true,
            EnableLiveShadowMode = false,
            EnableLiveResponsiveFeedbackBridge = false
        });

        await port.SuppressNormalProgressAsync("turn-1");
        await port.RequestBridgeFeedbackAsync(Candidate(), Decision(), FocusAction());

        Assert.Equal(0, feedback.SuppressCount);
        Assert.Equal(0, feedback.BridgeCount);
    }

    [Fact]
    public async Task FeedbackPort_WhenShadowModeEnabled_DoesNotCallFeedbackServicesEvenIfBridgeEnabled()
    {
        var feedback = new FakeResponsiveFeedbackOrchestrator();
        var port = FeedbackPort(feedback, new InterruptionHandlingOptions
        {
            Enabled = true,
            EnableLiveShadowMode = true,
            EnableLiveResponsiveFeedbackBridge = true
        });

        await port.SuppressNormalProgressAsync("turn-1");
        await port.RequestBridgeFeedbackAsync(Candidate(), Decision(), FocusAction());

        Assert.Equal(0, feedback.SuppressCount);
        Assert.Equal(0, feedback.BridgeCount);
    }

    [Fact]
    public async Task FeedbackPort_WhenEnabledAndNotShadow_CallsFeedbackServices()
    {
        var feedback = new FakeResponsiveFeedbackOrchestrator();
        var port = FeedbackPort(feedback, new InterruptionHandlingOptions
        {
            Enabled = true,
            EnableLiveShadowMode = false,
            EnableLiveResponsiveFeedbackBridge = true
        });

        await port.SuppressNormalProgressAsync("turn-1");
        await port.RequestBridgeFeedbackAsync(Candidate(), Decision(), FocusAction());

        Assert.Equal(1, feedback.SuppressCount);
        Assert.Equal(1, feedback.BridgeCount);
        Assert.Equal("turn-1", feedback.LastBridgeContext?.TurnId);
        Assert.True(feedback.LastBridgeContext?.IsInterruptionFeedback);
    }

    [Fact]
    public async Task RouterPort_RemainsNoOpInPr6()
    {
        var port = new NoOpInterruptionRequestRouterPort();

        await port.RouteRedirectedRequestAsync("new request", "turn-1", "correlation-1");
    }

    [Fact]
    public async Task ModelPort_RemainsFailFastInPr6()
    {
        var port = new NoOpInterruptionModelPort();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.GenerateClarificationAsync(new ClarificationRequest()));
    }

    private static AssistantSpeechInterruptionPlaybackPort PlaybackPort(
        FakePlaybackService playback,
        InterruptionHandlingOptions options) =>
        new(
            playback,
            Options.Create(options),
            NullLogger<AssistantSpeechInterruptionPlaybackPort>.Instance);

    private static ResponsiveFeedbackInterruptionPort FeedbackPort(
        FakeResponsiveFeedbackOrchestrator feedback,
        InterruptionHandlingOptions options) =>
        new(
            feedback,
            new InterruptionFeedbackAdapter(),
            Options.Create(options),
            NullLogger<ResponsiveFeedbackInterruptionPort>.Instance);

    private static ConversationalInterruptionCandidate Candidate() => new()
    {
        CorrelationId = "correlation-1",
        ActiveTurnId = "turn-1",
        Transcript = "actually make that blue",
        IsLikelyUserSpeech = true
    };

    private static ConversationalInterruptionDecision Decision() => new()
    {
        Type = ConversationalInterruptionType.Correction,
        Strategy = ConversationalInterruptionHandlingStrategy.CancelAndRedirect
    };

    private static ConversationFocusAction FocusAction() => new()
    {
        Type = ConversationFocusActionType.CancelAndReplaceMainTurn,
        ActiveTurnId = "turn-1",
        RequiresBridgeFeedback = true
    };

    private sealed class FakePlaybackService : IAssistantSpeechPlaybackService
    {
        public int PauseCount { get; private set; }
        public int StopCount { get; private set; }
        public int ClearQueueCount { get; private set; }

        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false) =>
            Task.CompletedTask;

        public Task StopCurrentAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            PauseCount++;
            return Task.CompletedTask;
        }

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            ClearQueueCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeResponsiveFeedbackOrchestrator : IResponsiveFeedbackOrchestrator
    {
        public int SuppressCount { get; private set; }
        public int BridgeCount { get; private set; }
        public FeedbackContext? LastBridgeContext { get; private set; }

        public Task<FeedbackEmissionResult> TryEmitImmediateFeedbackAsync(
            FeedbackContext context,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            CancellationToken cancellationToken) =>
            Task.FromResult(new FeedbackEmissionResult());

        public Task<FeedbackEmissionResult> TryEmitInterruptionBridgeAsync(
            FeedbackContext context,
            CancellationToken cancellationToken)
        {
            BridgeCount++;
            LastBridgeContext = context;
            return Task.FromResult(new FeedbackEmissionResult { Emitted = true });
        }

        public IRequestProgressSpeechHandle? StartProgressFeedback(
            FeedbackContext context,
            RequestProgressSpeechRequest request,
            CancellationToken cancellationToken) =>
            null;

        public void MarkMainResponseReady(string correlationId)
        {
        }

        public bool WasImmediateFeedbackEmitted(string correlationId) => false;

        public void SuppressNormalProgressForTurn(string turnId, string reason)
        {
            SuppressCount++;
        }
    }
}
