namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class YieldedInterruptionUtterance
{
    public string Transcript { get; init; } = string.Empty;

    public bool YieldedByLayer1 { get; init; } = true;

    public string YieldReason { get; init; } = string.Empty;

    public string CaptureKind { get; init; } = string.Empty;

    public string RouteKind { get; init; } = string.Empty;

    public string ActiveTurnId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? OriginalObservedTurnId { get; init; }

    public string? TurnBindingSource { get; init; }

    public string? ActivePlaybackCorrelationId { get; init; }

    public string? ActivePlaybackSpeechType { get; init; }

    public string? ProvisionalAudioHoldId { get; init; }

    public bool WasHeldByProvisionalAudioHold { get; init; }

    public bool? AssistantWasSpeakingOriginal { get; init; }

    public bool? AssistantWasSpeakingResolved { get; init; }

    public bool RecentlyYieldedSnapshotFound { get; init; }

    public double? RecentlyYieldedSnapshotAgeMs { get; init; }

    public double? Layer1Confidence { get; init; }

    public string? Layer1Decision { get; init; }

    public string? OriginalUserQuestion { get; init; }

    public string? CurrentAssistantSentence { get; init; }

    public string? LastCompletedAssistantSentence { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
