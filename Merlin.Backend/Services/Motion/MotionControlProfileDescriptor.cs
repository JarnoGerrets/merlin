using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion;

public sealed record MotionControlProfileDescriptor(
    string ProfileId,
    string DisplayName,
    ActiveSurfaceKind SurfaceKind,
    int Priority,
    IReadOnlySet<string> Capabilities,
    IReadOnlyDictionary<string, string>? Metadata = null);
