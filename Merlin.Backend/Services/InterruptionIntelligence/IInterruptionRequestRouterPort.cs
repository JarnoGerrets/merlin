namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IInterruptionRequestRouterPort
{
    Task RouteRedirectedRequestAsync(
        string rewrittenRequest,
        string originalTurnId,
        string correlationId,
        CancellationToken cancellationToken = default);
}
