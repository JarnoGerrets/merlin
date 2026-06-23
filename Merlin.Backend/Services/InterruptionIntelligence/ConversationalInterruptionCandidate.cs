namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ConversationalInterruptionCandidate
{
    public string CorrelationId { get; init; } = "";

    public string ActiveTurnId { get; init; } = "";

    public string? Transcript { get; init; } = "";

    public double TranscriptConfidence { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset EndedAtUtc { get; init; }

    public TimeSpan AssistantPlaybackPosition { get; init; }

    public bool AssistantWasSpeaking { get; init; }

    public bool PlaybackWasPausedForCapture { get; init; }

    public string? CurrentAssistantSentence { get; init; }

    public string? LastCompletedAssistantSentence { get; init; }

    public string? OriginalUserQuestion { get; init; }

    public bool IsLikelySelfEcho { get; init; }

    public bool IsLikelyUserSpeech { get; init; }
}
