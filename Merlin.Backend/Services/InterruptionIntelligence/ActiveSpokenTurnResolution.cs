namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ActiveSpokenTurnResolution
{
    public string ActiveTurnId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public bool IsActiveAnswerTurn { get; init; }

    public string? OriginalObservedTurnId { get; init; }

    public string? Reason { get; init; }

    public string? ActivePlaybackCorrelationId { get; init; }

    public string? ActivePlaybackSpeechType { get; init; }

    public string? ProvisionalAudioHoldId { get; init; }

    public bool WasHeldByProvisionalAudioHold { get; init; }

    public bool RecentlyYieldedSnapshotFound { get; init; }

    public double? RecentlyYieldedSnapshotAgeMs { get; init; }
}
