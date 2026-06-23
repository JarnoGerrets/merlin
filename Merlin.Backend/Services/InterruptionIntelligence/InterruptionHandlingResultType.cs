namespace Merlin.Backend.Services.InterruptionIntelligence;

public enum InterruptionHandlingResultType
{
    Unknown = 0,

    Ignored,
    Continued,
    Stopped,
    CancelledAndRedirected,
    ClarificationPrepared,
    RecompositionPrepared,
    ClarificationAndRecompositionPrepared,
    FollowUpQueued,
    AskedUserToClarify,
    Failed
}
