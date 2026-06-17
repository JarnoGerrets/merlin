namespace Merlin.Backend.Models;

public sealed record RouteDecision(
    string? CapabilityId,
    IntentDomain Domain,
    double Confidence,
    bool ShouldExecuteTool,
    string Reason)
{
    public static RouteDecision Tool(
        string capabilityId,
        IntentDomain domain,
        double confidence,
        string reason)
    {
        return new RouteDecision(capabilityId, domain, confidence, true, reason);
    }

    public static RouteDecision NoTool(
        IntentDomain domain,
        string reason,
        double confidence = 0)
    {
        return new RouteDecision("no_tool", domain, confidence, false, reason);
    }
}
