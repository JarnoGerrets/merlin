using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class PendingInterruptionClarificationServiceTests
{
    [Fact]
    public void CreateStoresPendingWithExpiry()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var service = CreateService(timeoutMs: 15000);

        var pending = service.CreatePending(CreateRequest(), now);

        Assert.False(string.IsNullOrWhiteSpace(pending.ClarificationId));
        Assert.Equal("turn-1", pending.ActiveTurnId);
        Assert.Equal("correlation-1", pending.CorrelationId);
        Assert.Equal("capture-1", pending.CaptureId);
        Assert.Equal("What did you mean by that?", pending.OriginalTranscript);
        Assert.Equal("what did you mean by that", pending.NormalizedTranscript);
        Assert.Equal(now, pending.CreatedAtUtc);
        Assert.Equal(now.AddSeconds(15), pending.ExpiresAtUtc);
        Assert.Same(pending, service.TryGetLatestPending(now));
        Assert.True(service.HasActivePendingForTurn("turn-1", now));
    }

    [Fact]
    public void ConsumeResponseRemovesPending()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var service = CreateService();
        var pending = service.CreatePending(CreateRequest(), now);

        var response = service.TryConsumeResponse(" In the pool. ", "capture-2", "correlation-2", now.AddSeconds(2));

        Assert.NotNull(response);
        Assert.Equal(pending.ClarificationId, response.Pending.ClarificationId);
        Assert.Equal("In the pool.", response.ResponseText);
        Assert.Equal("in the pool", response.NormalizedResponseText);
        Assert.Equal("capture-2", response.CaptureId);
        Assert.Equal("correlation-2", response.CorrelationId);
        Assert.Null(service.TryGetLatestPending(now.AddSeconds(2)));
        Assert.False(service.HasActivePendingForTurn("turn-1", now.AddSeconds(2)));
    }

    [Fact]
    public void ExpiredPendingIsNotReturnedOrConsumed()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var service = CreateService(timeoutMs: 1000);
        service.CreatePending(CreateRequest(), now);

        var later = now.AddSeconds(2);

        Assert.Null(service.TryGetLatestPending(later));
        Assert.Null(service.TryConsumeResponse("in the pool", "capture-2", "correlation-2", later));
        Assert.False(service.HasActivePendingForTurn("turn-1", later));
    }

    [Fact]
    public async Task ExpiredPendingClearsAwaitingInterruptionClarificationState()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var ui = new RecordingUiStateSink();
        var service = CreateService(timeoutMs: 1000, ui.Broadcaster);
        service.CreatePending(CreateRequest(), now);

        var expired = service.ExpireDue(now.AddSeconds(2));

        Assert.Equal(1, expired);
        await ui.WaitUntilAsync(state =>
            state.Reason == "pending_interruption_clarification_timeout"
            && state.TurnId == "turn-1"
            && state.CorrelationId == "correlation-1"
            && state.InterruptionState == AssistantUiStateEvent.InterruptionStateNone);
    }

    [Fact]
    public void CancelForTurnRemovesOnlyMatchingTurn()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var service = CreateService();
        service.CreatePending(CreateRequest(activeTurnId: "turn-1", correlationId: "correlation-1"), now);
        var second = service.CreatePending(CreateRequest(activeTurnId: "turn-2", correlationId: "correlation-2"), now.AddSeconds(1));

        Assert.True(service.CancelForTurn("turn-1", "test", now.AddSeconds(2)));

        var remaining = service.TryGetLatestPending(now.AddSeconds(2));
        Assert.NotNull(remaining);
        Assert.Equal(second.ClarificationId, remaining.ClarificationId);
    }

    [Fact]
    public async Task CancelForTurnClearsAwaitingInterruptionClarificationState()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var ui = new RecordingUiStateSink();
        var service = CreateService(uiStateBroadcaster: ui.Broadcaster);
        service.CreatePending(CreateRequest(activeTurnId: "turn-1", correlationId: "correlation-1"), now);

        Assert.True(service.CancelForTurn("turn-1", "test", now.AddSeconds(1)));

        await ui.WaitUntilAsync(state =>
            state.Reason == "pending_interruption_clarification_cancelled"
            && state.TurnId == "turn-1"
            && state.CorrelationId == "correlation-1"
            && state.InterruptionState == AssistantUiStateEvent.InterruptionStateNone);
    }

    [Fact]
    public async Task TimeoutRecoveryExpiresPendingWithoutPassiveAccess()
    {
        var ui = new RecordingUiStateSink();
        var service = CreateService(timeoutMs: 25, ui.Broadcaster);
        service.CreatePending(CreateRequest(), DateTimeOffset.UtcNow);

        await ui.WaitUntilAsync(state =>
            state.Reason == "pending_interruption_clarification_timeout"
            && state.InterruptionState == AssistantUiStateEvent.InterruptionStateNone);

        Assert.Null(service.TryGetLatestPending());
        Assert.False(service.HasActivePendingForTurn("turn-1"));
    }

    [Fact]
    public void DefaultTimeoutConfigUsesHumanScaleFallback()
    {
        var now = DateTimeOffset.Parse("2026-07-07T10:00:00Z");
        var service = new PendingInterruptionClarificationService(
            Options.Create(new InterruptionHandlingOptions()),
            NullLogger<PendingInterruptionClarificationService>.Instance);

        var pending = service.CreatePending(CreateRequest(), now);

        Assert.Equal(now.AddSeconds(15), pending.ExpiresAtUtc);
    }

    private static PendingInterruptionClarificationService CreateService(
        int timeoutMs = 15000,
        AssistantUiStateBroadcaster? uiStateBroadcaster = null) =>
        new(
            Options.Create(new InterruptionHandlingOptions
            {
                PendingInterruptionClarificationTimeoutMs = timeoutMs
            }),
            NullLogger<PendingInterruptionClarificationService>.Instance,
            uiStateBroadcaster);

    private static PendingInterruptionClarificationCreateRequest CreateRequest(
        string activeTurnId = "turn-1",
        string correlationId = "correlation-1") => new()
    {
        ActiveTurnId = activeTurnId,
        CorrelationId = correlationId,
        CaptureId = "capture-1",
        OriginalTranscript = "What did you mean by that?",
        NormalizedTranscript = "what did you mean by that",
        RouteKind = "PauseAndClarify",
        RouteAction = "AskClarification",
        Layer1Decision = "AskClarification",
        ProvisionalAudioHoldId = "hold-1",
        WasHeldByProvisionalAudioHold = true
    };

    private sealed class RecordingUiStateSink
    {
        private readonly object _sync = new();
        private readonly List<AssistantUiStateEvent> _events = [];

        public RecordingUiStateSink()
        {
            Broadcaster = new AssistantUiStateBroadcaster(NullLogger<AssistantUiStateBroadcaster>.Instance);
            Broadcaster.StateChanged += (uiState, _, _) =>
            {
                lock (_sync)
                {
                    _events.Add(uiState);
                }

                return Task.CompletedTask;
            };
        }

        public AssistantUiStateBroadcaster Broadcaster { get; }

        public async Task WaitUntilAsync(Func<AssistantUiStateEvent, bool> predicate)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (DateTimeOffset.UtcNow < deadline)
            {
                lock (_sync)
                {
                    if (_events.Any(predicate))
                    {
                        return;
                    }
                }

                await Task.Delay(10);
            }

            Assert.Fail("Timed out waiting for assistant UI state.");
        }
    }
}
