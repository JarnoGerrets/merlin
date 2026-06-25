using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ActiveSpokenTurnResolverTests
{
    [Fact]
    public void Resolve_ActiveFinalAnswerPlaybackWinsOverMonitorTurn()
    {
        var recentStore = new FakeRecentlyYieldedStore
        {
            Snapshot = RecentlyYieldedSnapshot("backend_voice:recent")
        };
        var playback = new FakePlaybackService
        {
            Snapshot = new ActiveSpeechPlaybackSnapshot
            {
                AssistantTurnId = "backend_voice:abc",
                CorrelationId = "backend_voice:abc",
                SpeechType = SpeechPlaybackItemType.FinalAnswer.ToString(),
                ItemType = SpeechPlaybackItemType.FinalAnswer.ToString(),
                IsActive = true,
                IsAudiblePlaybackActive = true,
                StartedAtUtc = DateTimeOffset.UtcNow
            }
        };
        var resolver = new ActiveSpokenTurnResolver(
            playback,
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            recentStore);

        var result = resolver.Resolve(MonitorContext());

        Assert.Equal("backend_voice:abc", result.ActiveTurnId);
        Assert.Equal("backend_voice:abc", result.CorrelationId);
        Assert.Equal("live-utterance-monitor", result.OriginalObservedTurnId);
        Assert.Equal("active_playback_snapshot", result.Source);
        Assert.True(result.IsActiveAnswerTurn);
    }

    [Fact]
    public void Resolve_RecentlyYieldedSnapshotWinsOverMonitorTurnWhenPlaybackSnapshotMissing()
    {
        var recentStore = new FakeRecentlyYieldedStore
        {
            Snapshot = RecentlyYieldedSnapshot("backend_voice:abc")
        };
        var resolver = new ActiveSpokenTurnResolver(
            new FakePlaybackService(),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            recentStore);

        var result = resolver.Resolve(MonitorContext());

        Assert.Equal("backend_voice:abc", result.ActiveTurnId);
        Assert.Equal("backend_voice:abc", result.CorrelationId);
        Assert.Equal("live-utterance-monitor", result.OriginalObservedTurnId);
        Assert.Equal("recently_yielded_spoken_turn", result.Source);
        Assert.True(result.IsActiveAnswerTurn);
        Assert.True(result.RecentlyYieldedSnapshotFound);
    }

    [Fact]
    public void Resolve_HeldRecentlyYieldedSnapshotWinsOverMonitorTurnWhenPlaybackSnapshotMissing()
    {
        var recentStore = new FakeRecentlyYieldedStore
        {
            Snapshot = RecentlyYieldedSnapshot(
                "backend_voice:abc",
                playbackWasCancelledByYieldFallback: false,
                playbackWasHeldByProvisionalAudioHold: true,
                holdId: "hold-1",
                yieldMode: "provisional_audio_hold")
        };
        var resolver = new ActiveSpokenTurnResolver(
            new FakePlaybackService(),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            recentStore);

        var result = resolver.Resolve(MonitorContext());

        Assert.Equal("backend_voice:abc", result.ActiveTurnId);
        Assert.Equal("backend_voice:abc", result.CorrelationId);
        Assert.Equal("recently_yielded_spoken_turn", result.Source);
        Assert.True(result.IsActiveAnswerTurn);
        Assert.True(result.RecentlyYieldedSnapshotFound);
    }

    [Fact]
    public void Resolve_FallsBackToObservedTurnWhenNoActivePlaybackOrRecentYieldOrSpeakingTurn()
    {
        var resolver = new ActiveSpokenTurnResolver(
            new FakePlaybackService(),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            new FakeRecentlyYieldedStore());

        var result = resolver.Resolve(MonitorContext());

        Assert.Equal("live-utterance-monitor", result.ActiveTurnId);
        Assert.Equal(string.Empty, result.CorrelationId);
        Assert.Equal("observed_context", result.Source);
        Assert.False(result.IsActiveAnswerTurn);
    }

    [Fact]
    public void Resolve_ExpiredSnapshotDoesNotHijackIdleRequest()
    {
        var resolver = new ActiveSpokenTurnResolver(
            new FakePlaybackService(),
            new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance),
            new FakeRecentlyYieldedStore());

        var result = resolver.Resolve(MonitorContext());

        Assert.Equal("live-utterance-monitor", result.ActiveTurnId);
        Assert.Equal("observed_context", result.Source);
    }

    [Fact]
    public void Resolve_UsesSpeakingLiveAssistantTurnWhenPlaybackSnapshotMissing()
    {
        var liveTurns = new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance);
        liveTurns.BeginTurn("default", "backend_voice:abc");
        liveTurns.UpdateTurnState("backend_voice:abc", LiveAssistantTurnState.Speaking);
        var resolver = new ActiveSpokenTurnResolver(new FakePlaybackService(), liveTurns, new FakeRecentlyYieldedStore());

        var result = resolver.Resolve(MonitorContext());

        Assert.Equal("backend_voice:abc", result.ActiveTurnId);
        Assert.Equal("backend_voice:abc", result.CorrelationId);
        Assert.Equal("live_assistant_turn_service", result.Source);
        Assert.True(result.IsActiveAnswerTurn);
    }

    private static BargeInSpeechContext MonitorContext() => new()
    {
        AssistantTurnId = "live-utterance-monitor",
        CorrelationId = null,
        SpeechType = SpeechPlaybackItemType.FinalAnswer,
        SpokenText = string.Empty
    };

    private static RecentlyYieldedSpokenTurnSnapshot RecentlyYieldedSnapshot(
        string turnId,
        bool playbackWasCancelledByYieldFallback = true,
        bool playbackWasHeldByProvisionalAudioHold = false,
        string? holdId = null,
        string? yieldMode = null) => new()
    {
        TurnId = turnId,
        CorrelationId = turnId,
        SpeechType = SpeechPlaybackItemType.FinalAnswer.ToString(),
        ItemType = SpeechPlaybackItemType.FinalAnswer.ToString(),
        YieldedAtUtc = DateTimeOffset.UtcNow,
        YieldReason = "residual_speech_detected",
        YieldSource = "FloorYieldController",
        PlaybackWasCancelledByYieldFallback = playbackWasCancelledByYieldFallback,
        PlaybackWasHeldByProvisionalAudioHold = playbackWasHeldByProvisionalAudioHold,
        HoldId = holdId,
        YieldMode = yieldMode
    };

    private sealed class FakePlaybackService : IAssistantSpeechPlaybackService
    {
        public ActiveSpeechPlaybackSnapshot? Snapshot { get; init; }

        public ActiveSpeechPlaybackSnapshot? GetActivePlaybackSnapshot() => Snapshot;

        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false) => Task.CompletedTask;

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRecentlyYieldedStore : IRecentlyYieldedSpokenTurnStore
    {
        public RecentlyYieldedSpokenTurnSnapshot? Snapshot { get; init; }

        public void Record(RecentlyYieldedSpokenTurnSnapshot snapshot)
        {
        }

        public RecentlyYieldedSpokenTurnSnapshot? TryGetFreshSnapshot(DateTimeOffset? nowUtc = null) => Snapshot;
    }
}
