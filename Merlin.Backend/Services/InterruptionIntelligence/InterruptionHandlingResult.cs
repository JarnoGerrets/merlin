namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class InterruptionHandlingResult
{
    public InterruptionHandlingResultType Type { get; init; } = InterruptionHandlingResultType.Unknown;

    public ConversationalInterruptionDecision Decision { get; init; } = new();

    public ConversationFocusAction FocusAction { get; init; } = new();

    public SpokenAnswerCheckpoint? Checkpoint { get; init; }

    public ClarificationRequest? ClarificationRequest { get; init; }

    public ClarificationResult? ClarificationResult { get; init; }

    public ContinuationRecompositionRequest? ContinuationRequest { get; init; }

    public ContinuationRecompositionResult? ContinuationResult { get; init; }

    public string? RedirectedRequest { get; init; }

    public string? QueuedFollowUpId { get; init; }

    public bool PlaybackPaused { get; init; }

    public bool PlaybackCancelled { get; init; }

    public bool OriginalTurnCancelled { get; init; }

    public bool BridgeFeedbackRequested { get; init; }

    public bool NormalProgressSuppressed { get; init; }

    public string Reason { get; init; } = "";
}
