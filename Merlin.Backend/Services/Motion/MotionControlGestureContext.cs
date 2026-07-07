using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Vision;

namespace Merlin.Backend.Services.Motion;

public sealed record MotionControlGestureContext(
    VisionGestureEvent GestureEvent,
    ActiveSurfaceSnapshot ActiveSurface,
    MotionControlModeSnapshot ModeSnapshot,
    DateTimeOffset ReceivedUtc);
