namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ConversationalInterruptionCandidateFactory : IConversationalInterruptionCandidateFactory
{
    public ConversationalInterruptionCandidate CreateFromYieldedInterruption(YieldedInterruptionUtterance utterance)
    {
        ArgumentNullException.ThrowIfNull(utterance);

        // Layer 1 already decided whether Merlin should yield.
        // ConversationalInterruption does not re-run acoustic user/echo detection.
        // TranscriptConfidence is set high for yielded utterances unless a real STT confidence exists.
        var startedAt = utterance.StartedAtUtc == default
            ? DateTimeOffset.UtcNow
            : utterance.StartedAtUtc;

        return new ConversationalInterruptionCandidate
        {
            Transcript = utterance.Transcript.Trim(),
            TranscriptConfidence = 1.0,
            CorrelationId = NullIfWhiteSpace(utterance.CorrelationId),
            ActiveTurnId = NullIfWhiteSpace(utterance.ActiveTurnId),
            AssistantWasSpeaking = true,
            PlaybackWasPausedForCapture = true,
            IsLikelySelfEcho = false,
            IsLikelyUserSpeech = utterance.YieldedByLayer1,
            AssistantPlaybackPosition = TimeSpan.Zero,
            OriginalUserQuestion = NullIfWhiteSpace(utterance.OriginalUserQuestion),
            CurrentAssistantSentence = NullIfWhiteSpace(utterance.CurrentAssistantSentence),
            LastCompletedAssistantSentence = NullIfWhiteSpace(utterance.LastCompletedAssistantSentence),
            StartedAtUtc = startedAt,
            EndedAtUtc = utterance.EndedAtUtc == default ? startedAt : utterance.EndedAtUtc
        };
    }

    public ConversationalInterruptionCandidate CreateFromLiveBargeIn(LiveInterruptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CreateFromYieldedInterruption(new YieldedInterruptionUtterance
        {
            Transcript = context.Transcript,
            YieldedByLayer1 = context.IsLikelyUserSpeech && !context.IsLikelySelfEcho,
            YieldReason = "legacy_live_interruption_context",
            ActiveTurnId = context.ActiveTurnId,
            CorrelationId = context.CorrelationId,
            Layer1Confidence = context.TranscriptConfidence,
            OriginalUserQuestion = context.OriginalUserQuestion,
            CurrentAssistantSentence = context.CurrentAssistantSentence,
            LastCompletedAssistantSentence = context.LastCompletedAssistantSentence,
            StartedAtUtc = context.StartedAtUtc,
            EndedAtUtc = context.EndedAtUtc
        });
    }

    public ConversationalInterruptionCandidate CreateFromLiveBargeIn(
        string transcript,
        double transcriptConfidence,
        string correlationId,
        string activeTurnId,
        bool assistantWasSpeaking,
        bool playbackWasPausedForCapture,
        bool isLikelySelfEcho,
        bool isLikelyUserSpeech,
        TimeSpan assistantPlaybackPosition,
        string? originalUserQuestion = null,
        string? currentAssistantSentence = null,
        string? lastCompletedAssistantSentence = null,
        DateTimeOffset? startedAtUtc = null,
        DateTimeOffset? endedAtUtc = null)
    {
        var trimmedTranscript = transcript.Trim();
        var hasTranscript = !string.IsNullOrWhiteSpace(trimmedTranscript);
        var likelyUserSpeech = hasTranscript && !isLikelySelfEcho && isLikelyUserSpeech;
        var startedAt = startedAtUtc ?? DateTimeOffset.UtcNow;

        return new ConversationalInterruptionCandidate
        {
            Transcript = trimmedTranscript,
            TranscriptConfidence = transcriptConfidence,
            CorrelationId = NullIfWhiteSpace(correlationId),
            ActiveTurnId = NullIfWhiteSpace(activeTurnId),
            AssistantWasSpeaking = assistantWasSpeaking,
            PlaybackWasPausedForCapture = playbackWasPausedForCapture,
            IsLikelySelfEcho = isLikelySelfEcho,
            IsLikelyUserSpeech = likelyUserSpeech,
            AssistantPlaybackPosition = assistantPlaybackPosition,
            OriginalUserQuestion = NullIfWhiteSpace(originalUserQuestion),
            CurrentAssistantSentence = NullIfWhiteSpace(currentAssistantSentence),
            LastCompletedAssistantSentence = NullIfWhiteSpace(lastCompletedAssistantSentence),
            StartedAtUtc = startedAt,
            EndedAtUtc = endedAtUtc ?? startedAt
        };
    }

    private static string NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
