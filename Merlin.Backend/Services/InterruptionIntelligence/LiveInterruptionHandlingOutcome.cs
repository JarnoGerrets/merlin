namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class LiveInterruptionHandlingOutcome
{
    public bool WasHandled { get; init; }

    public bool ShouldContinueOldPath { get; init; } = true;

    public InterruptionHandlingResult? Result { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string? RedirectedRequest { get; init; }

    public bool ShouldCancelActiveTurn { get; init; }

    public bool IsCorrectionRedirect { get; init; }
}
