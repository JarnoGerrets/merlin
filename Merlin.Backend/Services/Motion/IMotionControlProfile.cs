using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion;

public interface IMotionControlProfile
{
    MotionControlProfileDescriptor Descriptor { get; }

    bool CanHandle(ActiveSurfaceSnapshot surface);

    Task ActivateAsync(
        MotionControlProfileActivationContext context,
        CancellationToken cancellationToken = default);

    Task DeactivateAsync(
        string reason,
        CancellationToken cancellationToken = default);

    Task HandleGestureAsync(
        MotionControlGestureContext context,
        CancellationToken cancellationToken = default);

    Task OnActiveSurfaceChangedAsync(
        ActiveSurfaceSnapshot surface,
        CancellationToken cancellationToken = default);
}
