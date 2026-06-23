namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class SpokenAnswerState
{
    public string TurnId { get; init; } = "";

    public string CorrelationId { get; init; } = "";

    public string OriginalUserQuestion { get; init; } = "";

    public string? OriginalAssistantDraft { get; init; }

    public string SpokenSoFar { get; init; } = "";

    public string LastCompletedSentence { get; init; } = "";

    public string CurrentPartialSentence { get; init; } = "";

    public string UnspokenRemainder { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }

    public TimeSpan PlaybackPosition { get; init; }

    public bool CanRecompose { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
