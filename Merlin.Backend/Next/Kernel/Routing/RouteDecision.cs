namespace Merlin.Backend.Next.Kernel.Routing;

public sealed record RouteDecision(
    RouteDecisionKind Kind,
    string? CapabilityId = null,
    string? TargetSurfaceId = null,
    double Confidence = 0,
    string? Reason = null,
    IReadOnlyDictionary<string, string?>? Arguments = null)
{
    public static RouteDecision NoDecision(string reason) =>
        new(RouteDecisionKind.NoDecision, Reason: reason);
}
