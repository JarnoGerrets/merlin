namespace Merlin.Backend.Services.InterruptionIntelligence;

public enum ConversationalInterruptionType
{
    Unknown = 0,

    Backchannel,
    PassiveAgreement,
    StopRequest,
    CancelRequest,
    RepeatRequest,

    Correction,
    Redirect,
    ClarificationQuestion,
    RelatedFollowUpQuestion,
    SideComment,
    Disagreement,
    AdditionalContext,

    PlaybackControl,
    NoiseOrFalsePositive
}
