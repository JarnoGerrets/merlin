namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class PendingInterruptionClarification
{
    public required string ClarificationId { get; init; }

    public required string ActiveTurnId { get; init; }

    public required string CorrelationId { get; init; }

    public string? CaptureId { get; init; }

    public required string OriginalTranscript { get; init; }

    public required string NormalizedTranscript { get; init; }

    public string? RouteKind { get; init; }

    public string? RouteAction { get; init; }

    public string? Layer1Decision { get; init; }

    public string? ProvisionalAudioHoldId { get; init; }

    public bool WasHeldByProvisionalAudioHold { get; init; }

    public string? OriginalUserQuestion { get; init; }

    public string? SafeSpokenPrefix { get; init; }

    public string? LastCompletedSentence { get; init; }

    public string? DiscardedPartialSentence { get; init; }

    public string? CurrentTopicLabel { get; init; }

    public string? OriginalPlanOrIntent { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset ExpiresAtUtc { get; init; }
}

public sealed class PendingInterruptionClarificationCreateRequest
{
    public required string ActiveTurnId { get; init; }

    public required string CorrelationId { get; init; }

    public string? CaptureId { get; init; }

    public required string OriginalTranscript { get; init; }

    public required string NormalizedTranscript { get; init; }

    public string? RouteKind { get; init; }

    public string? RouteAction { get; init; }

    public string? Layer1Decision { get; init; }

    public string? ProvisionalAudioHoldId { get; init; }

    public bool WasHeldByProvisionalAudioHold { get; init; }

    public string? OriginalUserQuestion { get; init; }

    public string? SafeSpokenPrefix { get; init; }

    public string? LastCompletedSentence { get; init; }

    public string? DiscardedPartialSentence { get; init; }

    public string? CurrentTopicLabel { get; init; }

    public string? OriginalPlanOrIntent { get; init; }
}

public sealed class PendingInterruptionClarificationResponse
{
    public required PendingInterruptionClarification Pending { get; init; }

    public required string ResponseText { get; init; }

    public required string NormalizedResponseText { get; init; }

    public string? CaptureId { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset ConsumedAtUtc { get; init; }
}
