namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class LiveInterruptionHandlingOutcome
{
    public bool WasEvaluatedByConversationalInterruption { get; init; }

    public bool WasHandledByConversationalInterruption { get; init; }

    public bool AllowLegacyCleanup { get; init; } = true;

    public bool AllowLegacySemanticRouting { get; init; } = true;

    public bool ShouldResumeOrContinuePlaybackIfPossible { get; init; }

    public bool ShouldCancelPlayback { get; init; }

    public bool ShouldCancelCurrentTurn { get; init; }

    public bool ShouldRouteReplacementRequest { get; init; }

    public string? RewrittenRequest { get; init; }

    public InterruptionHandlingResult? Result { get; init; }

    public ConversationalInterruptionType? InterruptionType { get; init; }

    public ConversationalInterruptionHandlingStrategy? Strategy { get; init; }

    public string Reason { get; init; } = string.Empty;
}
