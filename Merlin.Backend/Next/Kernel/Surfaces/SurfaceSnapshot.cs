namespace Merlin.Backend.Next.Kernel.Surfaces;

public sealed record SurfaceSnapshot(
    string SurfaceId,
    string Kind,
    string Source,
    double Confidence,
    IReadOnlySet<string> Capabilities);
