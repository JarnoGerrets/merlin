namespace Merlin.Backend.Models;

public sealed record CapabilityDefinition(
    string Id,
    IntentDomain Domain,
    string Description,
    string HandlerName,
    IReadOnlyList<string> TriggerHints,
    IReadOnlyList<string> RejectHints,
    double MinimumConfidence = 0.75);
