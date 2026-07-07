namespace Merlin.Backend.Services.Context.ActiveSurface;

public sealed record ActiveSurfaceUpdate
{
    public required ActiveSurfaceKind Kind { get; init; }

    public required string SurfaceId { get; init; }

    public required string DisplayName { get; init; }

    public required ActiveSurfaceSource Source { get; init; }

    public required double Confidence { get; init; }

    public string? Reason { get; init; }

    public string? CorrelationId { get; init; }

    public IReadOnlySet<string> Capabilities { get; init; } = new HashSet<string>();

    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
