namespace Merlin.Backend.Next.Kernel.Runtime;

public sealed record MerlinNextShadowTrace
{
    public required string RequestId { get; init; }

    public required string Source { get; init; }

    public string? NormalizedInputText { get; init; }

    public string? ActiveSurfaceId { get; init; }

    public string? RoutePrediction { get; init; }

    public string? CapabilityId { get; init; }

    public double? Confidence { get; init; }

    public string? SafetyPrediction { get; init; }

    public required string ExecutionDisabledReason { get; init; }

    public long ElapsedMs { get; init; }

    public string? Exception { get; init; }
}
