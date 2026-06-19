namespace Merlin.Backend.Services.LiveUtterance;

public sealed class LiveUtteranceGateOptions
{
    public int ActiveFlowHoldWindowMs { get; init; } = 1500;

    public int IdleHoldWindowMs { get; init; } = 900;

    public int PausedClarificationHoldWindowMs { get; init; } = 5000;

    public bool RouteClearIdleRequestsToCommandRouter { get; init; } = true;
}
