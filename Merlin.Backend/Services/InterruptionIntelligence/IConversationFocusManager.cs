namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IConversationFocusManager
{
    ConversationThreadState? GetCurrentState();

    ConversationThreadState StartMainTurn(
        string threadId,
        string turnId,
        string originalUserQuestion,
        string? currentAnswerPurpose = null,
        SpokenAnswerState? spokenAnswerState = null);

    ConversationThreadState UpdateSpokenAnswer(SpokenAnswerState spokenAnswerState);

    ConversationThreadState SetAssistantSpeaking(bool isSpeaking);

    ConversationFocusAction ApplyInterruptionDecision(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision);

    ConversationThreadState MarkRecomposing(bool isRecomposing);

    ConversationThreadState CompleteCurrentTurn();

    ConversationThreadState StopCurrentTurn(string reason);

    void Clear();
}
