namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ContinuationRecompositionRequest
{
    public string OriginalUserQuestion { get; init; } = "";

    public string SpokenAnswerSoFar { get; init; } = "";

    public string LastCompletedSentence { get; init; } = "";

    public string DiscardedPartialSentence { get; init; } = "";

    public string UserInterruption { get; init; } = "";

    public string ClarificationReply { get; init; } = "";

    public string ClarificationContext { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }

    public string? OriginalPlanOrIntent { get; init; }

    public int MaxTokens { get; init; } = 500;
}
