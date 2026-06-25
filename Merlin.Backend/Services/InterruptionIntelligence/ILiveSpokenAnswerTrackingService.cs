namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface ILiveSpokenAnswerTrackingService
{
    void StartAnswer(
        string turnId,
        string correlationId,
        string originalUserQuestion,
        string? originalAssistantDraft = null,
        string? currentTopicLabel = null);

    void MarkChunkStarted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null);

    void MarkChunkCompleted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null);

    void MarkPlaybackCancelled(
        string turnId,
        string reason);

    void CompleteAnswer(string turnId);

    SpokenAnswerCheckpoint? TryCreateCheckpoint(
        string turnId,
        bool discardCurrentPartialSentence = true);
}
