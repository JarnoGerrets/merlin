namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface ISpokenAnswerTracker
{
    SpokenAnswerState? GetState(string turnId);

    SpokenAnswerState StartAnswer(
        string turnId,
        string correlationId,
        string originalUserQuestion,
        string? originalAssistantDraft = null,
        string? currentTopicLabel = null);

    SpokenAnswerState AppendSpokenText(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null);

    SpokenAnswerState MarkChunkStarted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null);

    SpokenAnswerState MarkChunkCompleted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null);

    SpokenAnswerCheckpoint CreateCheckpoint(
        string turnId,
        bool discardCurrentPartialSentence = true);

    void Clear(string turnId);
}
