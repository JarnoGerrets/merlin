namespace Merlin.Backend.Services.InterruptionIntelligence;

// Transitional PR6 name. This represents a yielded utterance from Layer 1, not raw maybe-speech.
public sealed class LiveInterruptionContext
{
    public string Transcript { get; init; } = string.Empty;

    public double TranscriptConfidence { get; init; }

    public string CorrelationId { get; init; } = string.Empty;

    public string ActiveTurnId { get; init; } = string.Empty;

    public bool AssistantWasSpeaking { get; init; }

    public bool PlaybackWasPausedForCapture { get; init; }

    public bool IsLikelySelfEcho { get; init; }

    public bool IsLikelyUserSpeech { get; init; }

    public TimeSpan AssistantPlaybackPosition { get; init; }

    public string? OriginalUserQuestion { get; init; }

    public string? CurrentAssistantSentence { get; init; }

    public string? LastCompletedAssistantSentence { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset EndedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
