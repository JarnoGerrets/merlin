namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IConversationalInterruptionCandidateFactory
{
    ConversationalInterruptionCandidate CreateFromYieldedInterruption(YieldedInterruptionUtterance utterance);

    ConversationalInterruptionCandidate CreateFromLiveBargeIn(
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
        DateTimeOffset? endedAtUtc = null);

    ConversationalInterruptionCandidate CreateFromLiveBargeIn(LiveInterruptionContext context);
}
