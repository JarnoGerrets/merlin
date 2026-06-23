namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class SpokenAnswerCheckpoint
{
    public string TurnId { get; init; } = "";

    public string CorrelationId { get; init; } = "";

    public string OriginalUserQuestion { get; init; } = "";

    public string SafeSpokenPrefix { get; init; } = "";

    public string LastCompletedSentence { get; init; } = "";

    public string DiscardedPartialSentence { get; init; } = "";

    public string? CurrentTopicLabel { get; init; }

    public TimeSpan PlaybackPosition { get; init; }

    public string? OriginalPlanOrIntent { get; init; }
}
