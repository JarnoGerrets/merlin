using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AssistantUiStateBroadcasterTests
{
    [Fact]
    public async Task EmitImmediateAsync_AssignsMonotonicSequences()
    {
        var sink = new RecordingUiStateSink();

        await sink.Broadcaster.EmitImmediateAsync(Create("listening", "capture_started"), "test");
        await sink.Broadcaster.EmitImmediateAsync(Create("speaking", "audio_playback_started", audible: true), "test");
        await sink.Broadcaster.EmitTerminalAsync(Create("idle", "final_answer_completed"), "test");

        Assert.Equal(3, sink.Events.Count);
        Assert.True(sink.Events[0].Sequence < sink.Events[1].Sequence);
        Assert.True(sink.Events[1].Sequence < sink.Events[2].Sequence);
    }

    [Fact]
    public async Task ImmediateSpeakingSupersedesPendingCoalescedThinking()
    {
        var sink = new RecordingUiStateSink();

        await sink.Broadcaster.RequestCoalescedStateAsync(Create("thinking", "request_accepted"), "test");
        await sink.Broadcaster.EmitImmediateAsync(Create("speaking", "audio_playback_started", audible: true), "test");
        await Task.Delay(150);

        var emitted = Assert.Single(sink.Events);
        Assert.Equal("speaking", emitted.BaseState);
        Assert.Equal("audio_playback_started", emitted.Reason);
    }

    [Fact]
    public async Task RapidCoalescedStatesCollapseToLatest()
    {
        var sink = new RecordingUiStateSink();

        await sink.Broadcaster.RequestCoalescedStateAsync(Create("thinking", "intent_routing_started"), "test");
        await sink.Broadcaster.RequestCoalescedStateAsync(Create("thinking", "tool_execution_started"), "test");
        await sink.Broadcaster.RequestCoalescedStateAsync(Create("thinking", "deepinfra_waiting"), "test");
        Assert.True(await sink.WaitUntilAsync(@event => @event.Reason == "deepinfra_waiting"));

        var emitted = Assert.Single(sink.Events);
        Assert.Equal("thinking", emitted.BaseState);
        Assert.Equal("deepinfra_waiting", emitted.Reason);
    }

    [Fact]
    public async Task ImmediateListeningSupersedesPendingCoalescedIdle()
    {
        var sink = new RecordingUiStateSink();

        await sink.Broadcaster.RequestCoalescedStateAsync(Create("idle", "tts_chunk_gap"), "test");
        await sink.Broadcaster.EmitImmediateAsync(Create("listening", "provisional_audio_hold_started"), "test");
        await Task.Delay(150);

        var emitted = Assert.Single(sink.Events);
        Assert.Equal("listening", emitted.BaseState);
        Assert.Equal("provisional_audio_hold_started", emitted.Reason);
    }

    [Fact]
    public async Task ChangedIdleReasonIsNotSuppressed()
    {
        var sink = new RecordingUiStateSink();

        await sink.Broadcaster.EmitImmediateAsync(Create("idle", "tts_chunk_gap"), "test");
        await sink.Broadcaster.EmitTerminalAsync(Create("idle", "final_answer_completed"), "test");

        Assert.Equal(2, sink.Events.Count);
        Assert.Equal("tts_chunk_gap", sink.Events[0].Reason);
        Assert.Equal("final_answer_completed", sink.Events[1].Reason);
    }

    [Fact]
    public async Task ExactDuplicateIsSuppressed()
    {
        var logger = new RecordingLogger<AssistantUiStateBroadcaster>();
        var sink = new RecordingUiStateSink(logger);

        var state = Create("idle", "final_answer_completed");
        await sink.Broadcaster.EmitTerminalAsync(state, "test");
        await sink.Broadcaster.EmitTerminalAsync(state, "test");

        var emitted = Assert.Single(sink.Events);
        Assert.Equal("final_answer_completed", emitted.Reason);
        Assert.Contains(logger.Messages, message => message.Contains("assistant_ui_state_suppressed_duplicate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OverlayClearEmitsWithHigherSequence()
    {
        var sink = new RecordingUiStateSink();

        await sink.Broadcaster.EmitImmediateAsync(
            Create("thinking", "confirmation_required", overlay: "confirmation"),
            "test");
        await sink.Broadcaster.EmitImmediateAsync(
            Create("thinking", "new_turn_clears_confirmation", overlay: "none"),
            "test");

        Assert.Equal(2, sink.Events.Count);
        Assert.Equal("confirmation", sink.Events[0].OverlayState);
        Assert.Equal("none", sink.Events[1].OverlayState);
        Assert.True(sink.Events[0].Sequence < sink.Events[1].Sequence);
    }

    private static AssistantUiStateEvent Create(
        string baseState,
        string reason,
        string overlay = "none",
        bool audible = false) =>
        AssistantUiStateEvent.Create(
            baseState,
            reason,
            correlationId: "turn-1",
            turnId: "turn-1",
            overlayState: overlay,
            speechItemType: baseState == "speaking" ? "final_answer" : "none",
            audiblePlaybackActive: audible);

    private sealed class RecordingUiStateSink
    {
        private readonly object _sync = new();
        private readonly List<AssistantUiStateEvent> _events = [];

        public RecordingUiStateSink(ILogger<AssistantUiStateBroadcaster>? logger = null)
        {
            Broadcaster = new AssistantUiStateBroadcaster(
                logger ?? new RecordingLogger<AssistantUiStateBroadcaster>());
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

        public IReadOnlyList<AssistantUiStateEvent> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToArray();
                }
            }
        }

        public async Task<bool> WaitUntilAsync(Func<AssistantUiStateEvent, bool> predicate)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (DateTimeOffset.UtcNow < deadline)
            {
                lock (_sync)
                {
                    if (_events.Any(predicate))
                    {
                        return true;
                    }
                }

                await Task.Delay(10);
            }

            return false;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_sync)
                {
                    return _messages.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_sync)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
