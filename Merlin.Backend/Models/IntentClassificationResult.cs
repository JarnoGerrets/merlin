namespace Merlin.Backend.Models;

public sealed record IntentClassificationResult(
    string CapabilityId,
    double Confidence,
    string Reason);
