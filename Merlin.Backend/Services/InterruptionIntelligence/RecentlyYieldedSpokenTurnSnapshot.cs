namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class RecentlyYieldedSpokenTurnSnapshot
{
    public string TurnId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string SpeechType { get; init; } = string.Empty;

    public string ItemType { get; init; } = string.Empty;

    public DateTimeOffset YieldedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string YieldReason { get; init; } = string.Empty;

    public string YieldSource { get; init; } = string.Empty;

    public bool PlaybackWasCancelledByYieldFallback { get; init; }

    public bool PlaybackWasHeldByProvisionalAudioHold { get; init; }

    public string? HoldId { get; init; }

    public string? YieldMode { get; init; }

    public string? OriginalObservedTurnId { get; init; }
}
