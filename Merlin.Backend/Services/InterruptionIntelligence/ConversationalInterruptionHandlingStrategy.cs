namespace Merlin.Backend.Services.InterruptionIntelligence;

public enum ConversationalInterruptionHandlingStrategy
{
    Unknown = 0,

    IgnoreAndContinue,
    ContinueWithoutResponse,
    LocalBridgeAndRecomposeFromCheckpoint,
    ClarifyThenRecomposeFromCheckpoint,
    CancelAndRedirect,
    QueueFollowUpAfterCurrent,
    StopPlayback,
    AskUserToClarifyInterruption
}
