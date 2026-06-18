namespace Merlin.Backend.Models;

public sealed record CapabilityRouteResult(
    string Intent,
    string Action,
    string TargetScope,
    string RecommendedCapability,
    double Confidence,
    bool RequiresExternalInfo,
    bool RequiresRepoContext,
    CapabilitySafetyLevel SafetyLevel,
    string? ClarifyingQuestion,
    IReadOnlyList<CapabilityScore> CandidateScores,
    string? NormalizedCommand,
    IReadOnlyDictionary<string, string> Arguments,
    bool ShouldExecuteTool,
    string Reason,
    string? CapabilityName,
    CapabilityAvailability Availability);
