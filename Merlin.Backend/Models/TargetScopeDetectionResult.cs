namespace Merlin.Backend.Models;

public sealed record TargetScopeDetectionResult(
    string Action,
    string TargetScope,
    double Confidence,
    IReadOnlyList<CapabilityScore> ScopeScores,
    string? ExtractedTarget,
    string Reason);
