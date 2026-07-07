using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Vision;

namespace Merlin.Backend.Services.Motion.Profiles;

public sealed class DashboardMotionProfile : IMotionControlProfile
{
    private readonly UiControlModeController _uiControlModeController;
    private readonly ILogger<DashboardMotionProfile> _logger;

    public DashboardMotionProfile(
        UiControlModeController uiControlModeController,
        ILogger<DashboardMotionProfile> logger)
    {
        _uiControlModeController = uiControlModeController;
        _logger = logger;
    }

    public MotionControlProfileDescriptor Descriptor { get; } = new(
        MotionControlProfileId.Dashboard,
        "Dashboard",
        ActiveSurfaceKind.Dashboard,
        Priority: 100,
        Capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MotionControlProfileCapabilities.Pointer,
            MotionControlProfileCapabilities.Hover,
            MotionControlProfileCapabilities.Select,
            MotionControlProfileCapabilities.Drag,
            MotionControlProfileCapabilities.Resize,
            MotionControlProfileCapabilities.Dismiss
        });

    public bool CanHandle(ActiveSurfaceSnapshot surface) => surface.Kind is ActiveSurfaceKind.Dashboard;

    public Task ActivateAsync(
        MotionControlProfileActivationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _uiControlModeController.Start();
        _logger.LogInformation(
            "MotionProfileActivated ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
            Descriptor.ProfileId,
            context.ActiveSurface.Kind,
            context.ActiveSurface.SurfaceId,
            context.Reason);
        return Task.CompletedTask;
    }

    public Task DeactivateAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _uiControlModeController.Stop();
        _logger.LogInformation(
            "MotionProfileDeactivated ProfileId: {ProfileId}. Reason: {Reason}.",
            Descriptor.ProfileId,
            reason);
        return Task.CompletedTask;
    }

    public Task HandleGestureAsync(
        MotionControlGestureContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug(
            "MotionProfileAction ProfileId: {ProfileId}. Action: dashboard_forward. GestureType: {GestureType}. PointerId: {PointerId}.",
            Descriptor.ProfileId,
            context.GestureEvent.Type,
            context.GestureEvent.PointerId);
        return Task.CompletedTask;
    }

    public Task OnActiveSurfaceChangedAsync(
        ActiveSurfaceSnapshot surface,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
