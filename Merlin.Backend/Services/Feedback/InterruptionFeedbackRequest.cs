namespace Merlin.Backend.Services.Feedback;

public sealed class InterruptionFeedbackRequest
{
    public string CorrelationId { get; init; } = string.Empty;

    public string TurnId { get; init; } = string.Empty;

    public string InterruptionType { get; init; } = string.Empty;

    public string Strategy { get; init; } = string.Empty;

    public FeedbackPhase? PhaseHint { get; init; }

    public FeedbackDurationEstimate DurationEstimate { get; init; } = FeedbackDurationEstimate.Short;

    public FeedbackConfidence Confidence { get; init; } = FeedbackConfidence.Medium;

    public bool RequiresBridgeFeedback { get; init; } = true;

    public bool IsRecompositionFeedback { get; init; }

    public bool IsWaitBridge { get; init; }

    public bool IsQueueFollowUp { get; init; }

    public bool IsRedirectOrCorrection { get; init; }

    public bool IsUnclear { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
