namespace Merlin.Backend.Models;

public sealed record CapabilityCandidate(
    string Id,
    IntentDomain Domain,
    string Description,
    string HandlerName,
    double Score,
    double MinimumConfidence);
