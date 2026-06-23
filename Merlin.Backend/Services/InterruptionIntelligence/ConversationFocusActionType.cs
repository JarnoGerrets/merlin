namespace Merlin.Backend.Services.InterruptionIntelligence;

public enum ConversationFocusActionType
{
    Unknown = 0,

    ContinueMainAnswer,
    IgnoreAndContinue,
    StopCurrentTurn,
    CancelAndReplaceMainTurn,
    RecomposeMainAnswer,
    ClarifyThenRecomposeMainAnswer,
    QueueFollowUpAfterCurrent,
    AskUserToClarifyInterruption
}
