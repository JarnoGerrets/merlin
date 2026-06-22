namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackContext
{
    public string CorrelationId { get; init; } = string.Empty;

    public string TurnId { get; init; } = string.Empty;

    public string RawUserText { get; init; } = string.Empty;

    public string NormalizedUserText { get; init; } = string.Empty;

    public FeedbackPhase Phase { get; init; } = FeedbackPhase.Unknown;

    public FeedbackDomain Domain { get; init; } = FeedbackDomain.General;

    public FeedbackDurationEstimate DurationEstimate { get; init; } = FeedbackDurationEstimate.Unknown;

    public FeedbackConfidence Confidence { get; init; } = FeedbackConfidence.Unknown;

    public FeedbackUrgency Urgency { get; init; } = FeedbackUrgency.Normal;

    public string? Intent { get; init; }

    public string? ToolName { get; init; }

    public string? TargetName { get; init; }

    public bool IsVoiceInteraction { get; init; }

    public bool IsOrbClient { get; init; }

    public bool IsExternalAction { get; init; }

    public bool NeedsConfirmation { get; init; }

    public bool IsUserWaiting { get; init; } = true;

    public bool AllowSpeech { get; init; }

    public bool AllowVisualFeedback { get; init; }

    public bool IsInterruptionFeedback { get; init; }

    public string? InterruptionType { get; init; }

    public string? InterruptionStrategy { get; init; }

    public bool IsRecompositionFeedback { get; init; }

    public bool SuppressNormalProgressFeedback { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
