namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ClarificationRequest
{
    public string OriginalUserQuestion { get; init; } = "";

    public string SpokenAnswerSoFar { get; init; } = "";

    public string LastCompletedSentence { get; init; } = "";

    public string DiscardedPartialSentence { get; init; } = "";

    public string UserInterruption { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }

    public int MaxTokens { get; init; } = 90;

    public string Tone { get; init; } = "brief, natural, conversational";
}
