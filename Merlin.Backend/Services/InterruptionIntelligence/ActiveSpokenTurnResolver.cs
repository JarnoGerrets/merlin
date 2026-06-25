using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ActiveSpokenTurnResolver : IActiveSpokenTurnResolver
{
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly ILiveAssistantTurnService _liveTurnService;
    private readonly IRecentlyYieldedSpokenTurnStore? _recentlyYieldedTurns;

    public ActiveSpokenTurnResolver(
        IAssistantSpeechPlaybackService playbackService,
        ILiveAssistantTurnService liveTurnService,
        IRecentlyYieldedSpokenTurnStore? recentlyYieldedTurns = null)
    {
        _playbackService = playbackService;
        _liveTurnService = liveTurnService;
        _recentlyYieldedTurns = recentlyYieldedTurns;
    }

    public ActiveSpokenTurnResolution Resolve(BargeInSpeechContext context, UserUtterance? utterance = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var observedTurnId = FirstNonEmpty(utterance?.ActiveTurnId, context.AssistantTurnId);
        var observedCorrelationId = FirstNonEmpty(utterance?.CorrelationId, context.CorrelationId);
        var playback = _playbackService.GetActivePlaybackSnapshot();
        if (playback is { IsActive: true }
            && string.Equals(playback.SpeechType, SpeechPlaybackItemType.FinalAnswer.ToString(), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(playback.AssistantTurnId))
        {
            return new ActiveSpokenTurnResolution
            {
                ActiveTurnId = playback.AssistantTurnId,
                CorrelationId = FirstNonEmpty(playback.CorrelationId, playback.AssistantTurnId),
                Source = "active_playback_snapshot",
                IsActiveAnswerTurn = true,
                OriginalObservedTurnId = observedTurnId,
                Reason = "Active final-answer playback wins over monitor context.",
                ActivePlaybackCorrelationId = playback.CorrelationId,
                ActivePlaybackSpeechType = playback.SpeechType,
                ProvisionalAudioHoldId = playback.HoldId,
                WasHeldByProvisionalAudioHold = playback.IsHeld,
                RecentlyYieldedSnapshotFound = _recentlyYieldedTurns?.TryGetFreshSnapshot() is not null
            };
        }

        var recentSnapshot = _recentlyYieldedTurns?.TryGetFreshSnapshot();
        if (recentSnapshot is not null
            && ShouldUseRecentlyYieldedSnapshot(observedTurnId, recentSnapshot))
        {
            return new ActiveSpokenTurnResolution
            {
                ActiveTurnId = recentSnapshot.TurnId,
                CorrelationId = FirstNonEmpty(recentSnapshot.CorrelationId, recentSnapshot.TurnId),
                Source = "recently_yielded_spoken_turn",
                IsActiveAnswerTurn = true,
                OriginalObservedTurnId = observedTurnId,
                Reason = recentSnapshot.PlaybackWasHeldByProvisionalAudioHold
                    ? "Fresh recently yielded held final-answer turn wins over monitor context."
                    : "Fresh recently yielded final-answer turn wins over monitor context after destructive pause fallback.",
                ActivePlaybackCorrelationId = recentSnapshot.CorrelationId,
                ActivePlaybackSpeechType = recentSnapshot.SpeechType,
                ProvisionalAudioHoldId = recentSnapshot.HoldId,
                WasHeldByProvisionalAudioHold = recentSnapshot.PlaybackWasHeldByProvisionalAudioHold,
                RecentlyYieldedSnapshotFound = true,
                RecentlyYieldedSnapshotAgeMs = Math.Max(0, (DateTimeOffset.UtcNow - recentSnapshot.YieldedAtUtc).TotalMilliseconds)
            };
        }

        if (_liveTurnService.TryGetCurrentActiveTurn(out var turn)
            && turn.State is LiveAssistantTurnState.Speaking
            && !string.IsNullOrWhiteSpace(turn.AssistantTurnId))
        {
            return new ActiveSpokenTurnResolution
            {
                ActiveTurnId = turn.AssistantTurnId,
                CorrelationId = turn.CorrelationId,
                Source = "live_assistant_turn_service",
                IsActiveAnswerTurn = true,
                OriginalObservedTurnId = observedTurnId,
                Reason = "Current live assistant turn is speaking.",
                RecentlyYieldedSnapshotFound = recentSnapshot is not null,
                RecentlyYieldedSnapshotAgeMs = recentSnapshot is null
                    ? null
                    : Math.Max(0, (DateTimeOffset.UtcNow - recentSnapshot.YieldedAtUtc).TotalMilliseconds)
            };
        }

        return new ActiveSpokenTurnResolution
        {
            ActiveTurnId = observedTurnId,
            CorrelationId = observedCorrelationId,
            Source = "observed_context",
            IsActiveAnswerTurn = false,
            OriginalObservedTurnId = observedTurnId,
            Reason = "No active spoken answer turn was available.",
            RecentlyYieldedSnapshotFound = recentSnapshot is not null,
            RecentlyYieldedSnapshotAgeMs = recentSnapshot is null
                ? null
                : Math.Max(0, (DateTimeOffset.UtcNow - recentSnapshot.YieldedAtUtc).TotalMilliseconds)
        };
    }

    private static bool ShouldUseRecentlyYieldedSnapshot(
        string observedTurnId,
        RecentlyYieldedSpokenTurnSnapshot snapshot)
    {
        return !string.IsNullOrWhiteSpace(snapshot.TurnId)
            && string.Equals(snapshot.SpeechType, SpeechPlaybackItemType.FinalAnswer.ToString(), StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(observedTurnId)
                || string.Equals(observedTurnId, "live-utterance-monitor", StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
