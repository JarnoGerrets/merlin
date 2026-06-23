namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ConversationalInterruptionDecision
{
    public ConversationalInterruptionType Type { get; init; } = ConversationalInterruptionType.Unknown;

    public ConversationalInterruptionHandlingStrategy Strategy { get; init; } = ConversationalInterruptionHandlingStrategy.Unknown;

    public double Confidence { get; init; }

    public bool PausePlayback { get; init; }

    public bool CancelOriginalTurn { get; init; }

    public bool ResumeRawPlayback { get; init; }

    public bool DiscardCurrentPartialSentence { get; init; } = true;

    public bool RequiresBridgeFeedback { get; init; }

    public bool RequiresDeepInfraClarification { get; init; }

    public bool RequiresContinuationRecomposition { get; init; }

    public bool CanRunContinuationInParallel { get; init; }

    public bool QueueAfterCurrentTurn { get; init; }

    public bool NeedsUserConfirmation { get; init; }

    public string? RewrittenUserRequest { get; init; }

    public int ClarificationMaxTokens { get; init; } = 90;

    public int ContinuationMaxTokens { get; init; } = 500;

    public string Reason { get; init; } = "";
}
