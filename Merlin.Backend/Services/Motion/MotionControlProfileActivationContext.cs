using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion;

public sealed record MotionControlProfileActivationContext(
    ActiveSurfaceSnapshot ActiveSurface,
    string Reason,
    DateTimeOffset ActivatedUtc);
