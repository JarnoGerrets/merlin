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

    public double? Layer1Confidence { get; init; }

    public string? Layer1Decision { get; init; }

    public string? OriginalUserQuestion { get; init; }

    public string? CurrentAssistantSentence { get; init; }

    public string? LastCompletedAssistantSentence { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
