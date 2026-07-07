using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Services.Motion.Profiles;

public sealed class BrowserWorkspaceMotionProfile : IMotionControlProfile
{
    private readonly IBrowserWorkspaceService _browserWorkspace;
    private readonly BrowserMotionOverlayModeService _browserMotionOverlayModeService;
    private readonly BrowserPinchClickController _browserPinchClickController;
    private readonly ILogger<BrowserWorkspaceMotionProfile> _logger;

    public BrowserWorkspaceMotionProfile(
        IBrowserWorkspaceService browserWorkspace,
        BrowserMotionOverlayModeService browserMotionOverlayModeService,
        BrowserPinchClickController browserPinchClickController,
        ILogger<BrowserWorkspaceMotionProfile> logger)
    {
        _browserWorkspace = browserWorkspace;
        _browserMotionOverlayModeService = browserMotionOverlayModeService;
        _browserPinchClickController = browserPinchClickController;
        _logger = logger;
    }

    public MotionControlProfileDescriptor Descriptor { get; } = new(
        MotionControlProfileId.BrowserWorkspace,
        "Browser Workspace",
        ActiveSurfaceKind.BrowserWorkspace,
        Priority: 90,
        Capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            MotionControlProfileCapabilities.Pointer,
            MotionControlProfileCapabilities.BrowserPointerOverlay,
            MotionControlProfileCapabilities.BrowserClick,
            MotionControlProfileCapabilities.BrowserScroll
        });

    public bool CanHandle(ActiveSurfaceSnapshot surface) => surface.Kind is ActiveSurfaceKind.BrowserWorkspace;

    public async Task ActivateAsync(
        MotionControlProfileActivationContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_browserWorkspace.IsActive)
        {
            throw new InvalidOperationException("Browser workspace is not active.");
        }

        var result = await _browserMotionOverlayModeService.EnableAsync(cancellationToken);
        if (result is not BrowserMotionOverlayStartResult.Started)
        {
            throw new InvalidOperationException($"Browser pointer could not start: {result}.");
        }

        _logger.LogInformation(
            "MotionProfileActivated ProfileId: {ProfileId}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. Reason: {Reason}.",
            Descriptor.ProfileId,
            context.ActiveSurface.Kind,
            context.ActiveSurface.SurfaceId,
            context.Reason);
    }

    public async Task DeactivateAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _browserPinchClickController.ResetAsync(reason, cancellationToken);
        await _browserMotionOverlayModeService.DisableAsync(reason, cancellationToken);
        _logger.LogInformation(
            "MotionProfileDeactivated ProfileId: {ProfileId}. Reason: {Reason}.",
            Descriptor.ProfileId,
            reason);
    }

    public async Task HandleGestureAsync(
        MotionControlGestureContext context,
        CancellationToken cancellationToken = default)
    {
        var gesture = context.GestureEvent;
        if (!string.Equals(gesture.Type, "gesture.pinch.end", StringComparison.OrdinalIgnoreCase))
        {
            await _browserMotionOverlayModeService.UpdatePointerAsync(gesture, cancellationToken);
        }

        await _browserPinchClickController.HandleGestureAsync(gesture, cancellationToken);
        _logger.LogDebug(
            "MotionProfileAction ProfileId: {ProfileId}. Action: browser_pointer. GestureType: {GestureType}. PointerId: {PointerId}.",
            Descriptor.ProfileId,
            gesture.Type,
            gesture.PointerId);
    }

    public Task OnActiveSurfaceChangedAsync(
        ActiveSurfaceSnapshot surface,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
