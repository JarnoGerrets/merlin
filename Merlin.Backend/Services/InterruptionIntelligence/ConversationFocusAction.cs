namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ConversationFocusAction
{
    public ConversationFocusActionType Type { get; init; } = ConversationFocusActionType.Unknown;

    public string ThreadId { get; init; } = "";

    public string ActiveTurnId { get; init; } = "";

    public string? RewrittenRequest { get; init; }

    public string? QueuedFollowUpId { get; init; }

    public bool ShouldPausePlayback { get; init; }

    public bool ShouldCancelPlayback { get; init; }

    public bool ShouldCancelOriginalTurn { get; init; }

    public bool ShouldCreateCheckpoint { get; init; }

    public bool ShouldDiscardPartialSentence { get; init; }

    public bool RequiresBridgeFeedback { get; init; }

    public bool RequiresClarification { get; init; }

    public bool RequiresRecomposition { get; init; }

    public string Reason { get; init; } = "";
}
