namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class NoOpInterruptionRequestRouterPort : IInterruptionRequestRouterPort
{
    public Task RouteRedirectedRequestAsync(
        string rewrittenRequest,
        string originalTurnId,
        string correlationId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
