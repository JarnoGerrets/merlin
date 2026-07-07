namespace Merlin.Backend.Services.Context.ActiveSurface;

public sealed record ActiveSurfaceSnapshot(
    ActiveSurfaceKind Kind,
    string SurfaceId,
    string DisplayName,
    double Confidence,
    ActiveSurfaceSource Source,
    DateTimeOffset UpdatedUtc,
    IReadOnlySet<string> Capabilities,
    IReadOnlyDictionary<string, string> Metadata);
