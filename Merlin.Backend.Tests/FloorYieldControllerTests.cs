using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Services.SpeechPresence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class FloorYieldControllerTests
{
    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenSingleYieldWorthyDecision_DoesNotYield()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(shouldYield: true));

        Assert.Equal(0, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.False(debug.Triggered);
        Assert.True(debug.CandidateActive);
        Assert.Equal(1, debug.CandidateStartFrameId);
        Assert.Equal(0, debug.CandidateDurationMs);
        Assert.Equal(30, debug.RequiredSustainedMs);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenYieldWorthyEvidenceSustainsForRequiredTime_YieldsOnce()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 2, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true));

        Assert.Equal(1, playback.HoldCount);
        Assert.Equal(0, playback.PauseCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.Triggered);
        Assert.False(debug.CandidateActive);
        Assert.Equal(4, debug.LastFrameId);
        Assert.Equal(1, debug.CandidateStartFrameId);
        Assert.Equal(30, debug.CandidateDurationMs);
        Assert.Equal(30, debug.RequiredSustainedMs);
        Assert.Equal(FloorYieldController.PlaybackYieldMode, debug.LastMode);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenHoldStarts_RecordsHeldRecentlyYieldedSnapshot()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var recentStore = new RecordingRecentlyYieldedStore();
        var controller = CreateController(monitor, playback, recentlyYieldedTurns: recentStore);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true));

        Assert.Equal(1, playback.HoldCount);
        Assert.Equal(0, playback.PauseCount);
        Assert.NotNull(recentStore.LastSnapshot);
        Assert.Equal(playback.HoldId, recentStore.LastSnapshot!.HoldId);
        Assert.True(recentStore.LastSnapshot.PlaybackWasHeldByProvisionalAudioHold);
        Assert.False(recentStore.LastSnapshot.PlaybackWasCancelledByYieldFallback);
        Assert.Equal(FloorYieldController.PlaybackYieldMode, recentStore.LastSnapshot.YieldMode);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenHoldFails_DoesNotUseDestructivePauseFallback()
    {
        var playback = new RecordingPlaybackService
        {
            HoldShouldSucceed = false
        };
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var recentStore = new RecordingRecentlyYieldedStore();
        var controller = CreateController(monitor, playback, recentlyYieldedTurns: recentStore);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true));

        Assert.Equal(1, playback.HoldCount);
        Assert.Equal(0, playback.PauseCount);
        Assert.Null(recentStore.LastSnapshot);
        Assert.Equal(FloorYieldController.HoldUnavailableYieldMode, controller.GetDebugState().LastMode);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenLowVadCandidateReachesDefaultSustain_DoesNotYieldYet()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true, vadConfidence: 0.27));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true, vadConfidence: 0.27));

        Assert.Equal(0, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.CandidateActive);
        Assert.Equal(30, debug.CandidateDurationMs);
        Assert.Equal(60, debug.RequiredSustainedMs);
        Assert.Equal(0.27, debug.CandidatePeakVadConfidence);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenLowVadCandidateReachesAdaptiveSustain_Yields()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true, vadConfidence: 0.27));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true, vadConfidence: 0.27));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 7, shouldYield: true, vadConfidence: 0.27));

        Assert.Equal(1, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.Triggered);
        Assert.Equal(60, debug.CandidateDurationMs);
        Assert.Equal(60, debug.RequiredSustainedMs);
        Assert.Equal(0.27, debug.CandidatePeakVadConfidence);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenVeryLowVadCandidateReachesSixtyMs_DoesNotYieldYet()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true, vadConfidence: 0.10));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 7, shouldYield: true, vadConfidence: 0.10));

        Assert.Equal(0, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.CandidateActive);
        Assert.Equal(60, debug.CandidateDurationMs);
        Assert.Equal(90, debug.RequiredSustainedMs);
        Assert.Equal(0.10, debug.CandidatePeakVadConfidence);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenVadPeakImproves_AdaptiveSustainShortens()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true, vadConfidence: 0.27));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 2, shouldYield: true, vadConfidence: 0.45));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true, vadConfidence: 0.20));

        Assert.Equal(1, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.Triggered);
        Assert.Equal(30, debug.CandidateDurationMs);
        Assert.Equal(30, debug.RequiredSustainedMs);
        Assert.Equal(0.45, debug.CandidatePeakVadConfidence);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenCandidateResets_ClearsPeakVad()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true, vadConfidence: 0.80));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 2, shouldYield: false));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 3, shouldYield: true, vadConfidence: 0.27));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 6, shouldYield: true, vadConfidence: 0.27));

        Assert.Equal(0, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.CandidateActive);
        Assert.Equal(3, debug.CandidateStartFrameId);
        Assert.Equal(30, debug.CandidateDurationMs);
        Assert.Equal(60, debug.RequiredSustainedMs);
        Assert.Equal(0.27, debug.CandidatePeakVadConfidence);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenDecisionIsNotAuthoritative_DoesNotYield()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);
        var decision = CreateOfficialDecision(shouldYield: true).WithAuthoritative(false);

        await controller.HandleOfficialDecisionAsync(decision);

        Assert.Equal(0, playback.HoldCount);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenDecisionIsNotAuthoritative_DoesNotContinueCandidate()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(
            CreateOfficialDecision(frameId: 4, shouldYield: true)
                .WithAuthoritative(false)
                .WithSourcePath("fast_hard_stop_candidate"));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 5, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 6, shouldYield: true));

        Assert.Equal(0, playback.HoldCount);
        Assert.True(controller.GetDebugState().CandidateActive);
        Assert.Equal(5, controller.GetDebugState().CandidateStartFrameId);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenRepeatedYieldFramesDuringSamePlayback_YieldsOnce()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 8, shouldYield: true));

        Assert.Equal(1, playback.HoldCount);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenPlaybackBecomesInactive_ResetsDuplicateGuard()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true));
        monitor.IsPlaybackActive = false;
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 2, shouldYield: false));
        monitor.IsPlaybackActive = true;
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 10, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 13, shouldYield: true));

        Assert.Equal(2, playback.HoldCount);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenNoDecisionArrivesBeforeRequiredTime_ResetsCandidate()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 2, shouldYield: false));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 3, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 5, shouldYield: true));

        Assert.Equal(0, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.CandidateActive);
        Assert.Equal(3, debug.CandidateStartFrameId);
        Assert.Equal(20, debug.CandidateDurationMs);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenPlaybackBecomesInactiveBeforeRequiredTime_ResetsCandidate()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 1, shouldYield: true));
        monitor.IsPlaybackActive = false;
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 4, shouldYield: true));
        monitor.IsPlaybackActive = true;
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 5, shouldYield: true));
        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(frameId: 7, shouldYield: true));

        Assert.Equal(0, playback.HoldCount);
        var debug = controller.GetDebugState();
        Assert.True(debug.CandidateActive);
        Assert.Equal(5, debug.CandidateStartFrameId);
        Assert.Equal(20, debug.CandidateDurationMs);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenPlaybackInactive_DoesNotYield()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = false };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(shouldYield: true));

        Assert.Equal(0, playback.HoldCount);
    }

    [Fact]
    public async Task HandleOfficialDecisionAsync_WhenShouldYieldFalse_DoesNotYield()
    {
        var playback = new RecordingPlaybackService();
        var monitor = new TestPlaybackMonitor { IsPlaybackActive = true };
        var controller = CreateController(monitor, playback);

        await controller.HandleOfficialDecisionAsync(CreateOfficialDecision(shouldYield: false));

        Assert.Equal(0, playback.HoldCount);
    }

    [Fact]
    public void PublicConstructor_DoesNotDependOnIntentRoutingOrStt()
    {
        var forbidden = new[]
        {
            "LiveUtteranceGate",
            "InterruptionClassifier",
            "CommandRouter",
            "Stt",
            "Transcription"
        };
        var parameterTypeNames = typeof(FloorYieldController)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name);

        foreach (var name in parameterTypeNames)
        {
            Assert.DoesNotContain(forbidden, value => name.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void HandleOfficialDecisionAsync_DoesNotAcceptBranchObservations()
    {
        var parameterTypes = typeof(FloorYieldController)
            .GetMethod(nameof(FloorYieldController.HandleOfficialDecisionAsync))!
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(typeof(SpeechPresenceOfficialDecision), parameterTypes);
        Assert.DoesNotContain(typeof(SpeechPresenceBranchObservation), parameterTypes);
    }

    private static FloorYieldController CreateController(
        TestPlaybackMonitor monitor,
        RecordingPlaybackService playback,
        int floorYieldMinSustainedMs = 30,
        IRecentlyYieldedSpokenTurnStore? recentlyYieldedTurns = null)
    {
        return new FloorYieldController(
            monitor,
            playback,
            new TestOptionsMonitor<SpeechPresenceOptions>(new SpeechPresenceOptions
            {
                EnableFloorYield = true,
                FloorYieldRequiresOfficialDecision = true,
                FloorYieldMinSustainedMs = floorYieldMinSustainedMs
            }),
            NullLogger<FloorYieldController>.Instance,
            recentlyYieldedTurns);
    }

    private static SpeechPresenceOfficialDecision CreateOfficialDecision(
        long frameId = 1,
        bool shouldYield = true,
        double? vadConfidence = null)
    {
        var state = shouldYield ? SpeechPresenceState.Yes : SpeechPresenceState.No;
        var resolvedVadConfidence = vadConfidence ?? (shouldYield ? 0.7 : 0.0);
        var evidence = new SpeechPresenceEvidence
        {
            FrameId = frameId,
            TimestampUtc = DateTimeOffset.UnixEpoch.AddMilliseconds(frameId * 10),
            AssistantPlaybackActive = true,
            RawMicRms = shouldYield ? 0.03 : 0.001,
            EchoReducedRms = shouldYield ? 0.02 : 0.001,
            PlaybackReferenceRms = 0.04,
            VadConfidence = resolvedVadConfidence,
            VadSpeechDetected = shouldYield,
            SourcePath = "official_frame_decision"
        };
        return new SpeechPresenceOfficialDecision
        {
            FrameId = frameId,
            TimestampUtc = evidence.TimestampUtc,
            Result = new SpeechPresenceResult
            {
                State = state,
                Confidence = shouldYield ? 0.9 : 0.0,
                IsUserSpeaking = shouldYield,
                ShouldYieldPlayback = shouldYield,
                Reason = shouldYield ? "residual_speech_detected" : "no_speech_evidence",
                Evidence = evidence
            }
        };
    }

    private sealed class TestPlaybackMonitor : IAssistantPlaybackMonitor
    {
        public bool IsPlaybackActive { get; set; }

        public DateTimeOffset? PlaybackStartedAt { get; set; } = DateTimeOffset.UnixEpoch;

        public double CurrentPlaybackEnergy { get; set; } = 0.1;

        public double RecentPlaybackEnergy { get; set; } = 0.1;
    }

    private sealed class RecordingPlaybackService : IAssistantSpeechPlaybackService
    {
        public int PauseCount { get; private set; }

        public int HoldCount { get; private set; }

        public string HoldId { get; } = "hold-1";

        public bool HoldShouldSucceed { get; init; } = true;

        public ActiveSpeechPlaybackSnapshot Snapshot { get; private set; } = new()
        {
            CorrelationId = "backend_voice:abc",
            AssistantTurnId = "backend_voice:abc",
            SpeechType = SpeechPlaybackItemType.FinalAnswer.ToString(),
            ItemType = SpeechPlaybackItemType.FinalAnswer.ToString(),
            IsActive = true,
            IsAudiblePlaybackActive = true,
            StartedAtUtc = DateTimeOffset.UtcNow
        };

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
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            PauseCount++;
            return Task.CompletedTask;
        }

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ProvisionalAudioHoldResult> BeginProvisionalAudioHoldAsync(
            string turnId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            HoldCount++;
            if (!HoldShouldSucceed)
            {
                return Task.FromResult(ProvisionalAudioHoldResult.Failed(turnId, reason, "test hold failure"));
            }

            Snapshot = new ActiveSpeechPlaybackSnapshot
            {
                CorrelationId = Snapshot.CorrelationId,
                AssistantTurnId = Snapshot.AssistantTurnId,
                SpeechType = Snapshot.SpeechType,
                ItemType = Snapshot.ItemType,
                IsActive = Snapshot.IsActive,
                IsHeld = true,
                IsAudiblePlaybackActive = false,
                HoldId = HoldId,
                StartedAtUtc = Snapshot.StartedAtUtc
            };
            return Task.FromResult(new ProvisionalAudioHoldResult(
                Success: true,
                HoldId: HoldId,
                TurnId: turnId,
                Reason: reason));
        }

        public ActiveSpeechPlaybackSnapshot? GetActivePlaybackSnapshot() => Snapshot;
    }

    private sealed class RecordingRecentlyYieldedStore : IRecentlyYieldedSpokenTurnStore
    {
        public RecentlyYieldedSpokenTurnSnapshot? LastSnapshot { get; private set; }

        public void Record(RecentlyYieldedSpokenTurnSnapshot snapshot)
        {
            LastSnapshot = snapshot;
        }

        public RecentlyYieldedSpokenTurnSnapshot? TryGetFreshSnapshot(DateTimeOffset? nowUtc = null) => LastSnapshot;
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}

file static class SpeechPresenceOfficialDecisionTestExtensions
{
    public static SpeechPresenceOfficialDecision WithAuthoritative(
        this SpeechPresenceOfficialDecision decision,
        bool isAuthoritative)
    {
        return new SpeechPresenceOfficialDecision
        {
            FrameId = decision.FrameId,
            TimestampUtc = decision.TimestampUtc,
            Result = decision.Result,
            IsAuthoritative = isAuthoritative,
            SourcePath = decision.SourcePath
        };
    }

    public static SpeechPresenceOfficialDecision WithSourcePath(
        this SpeechPresenceOfficialDecision decision,
        string sourcePath)
    {
        return new SpeechPresenceOfficialDecision
        {
            FrameId = decision.FrameId,
            TimestampUtc = decision.TimestampUtc,
            Result = decision.Result,
            IsAuthoritative = decision.IsAuthoritative,
            SourcePath = sourcePath
        };
    }
}
