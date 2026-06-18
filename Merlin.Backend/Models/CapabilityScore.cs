namespace Merlin.Backend.Models;

public sealed record CapabilityScore(
    string CapabilityId,
    string TargetScope,
    double Score,
    string Reason);
