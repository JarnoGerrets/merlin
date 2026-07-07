using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Vision;

namespace Merlin.Backend.Services.Motion;

public interface IMotionControlModeService
{
    event Func<VisionGestureEvent, CancellationToken, Task>? DashboardGestureForwarded;

    MotionControlModeSnapshot Current { get; }

    bool IsEnabled { get; }

    Task<MotionControlModeSnapshot> EnableAsync(
        string reason,
        MotionControlProfileOverride? profileOverride = null,
        CancellationToken cancellationToken = default);

    Task<MotionControlModeSnapshot> DisableAsync(
        string reason,
        CancellationToken cancellationToken = default);

    Task HandleGestureAsync(
        VisionGestureEvent gestureEvent,
        CancellationToken cancellationToken = default);

    Task OnActiveSurfaceChangedAsync(
        ActiveSurfaceSnapshot activeSurface,
        string reason,
        CancellationToken cancellationToken = default);
}
