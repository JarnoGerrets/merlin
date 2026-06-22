namespace Merlin.Backend.Services.Feedback;

public enum FeedbackPhase
{
    Unknown = 0,
    Starting,
    Interpreting,
    Planning,
    Executing,
    Waiting,
    StillWorking,
    NeedsConfirmation,
    Completing,
    Failed,
    HandlingInterruption,
    ClarifyingInterruption,
    RecomposingContinuation,
    Redirecting,
    QueueingFollowUp
}
