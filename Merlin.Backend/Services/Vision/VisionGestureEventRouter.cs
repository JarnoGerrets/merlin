namespace Merlin.Backend.Services.Vision;

using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.Motion;

public sealed class VisionGestureEventRouter
{
    private readonly ILogger<VisionGestureEventRouter> _logger;
    private readonly UiControlModeController _uiControlModeController;
    private readonly BrowserMotionOverlayModeService? _browserMotionOverlayModeService;
    private readonly BrowserPinchClickController? _browserPinchClickController;
    private readonly IMotionControlModeService? _motionControlModeService;

    public VisionGestureEventRouter(
        UiControlModeController uiControlModeController,
        ILogger<VisionGestureEventRouter> logger,
        BrowserMotionOverlayModeService? browserMotionOverlayModeService = null,
        BrowserPinchClickController? browserPinchClickController = null,
        IMotionControlModeService? motionControlModeService = null)
    {
        _uiControlModeController = uiControlModeController;
        _logger = logger;
        _browserMotionOverlayModeService = browserMotionOverlayModeService;
        _browserPinchClickController = browserPinchClickController;
        _motionControlModeService = motionControlModeService;
    }

    public event Func<VisionGestureEvent, CancellationToken, Task>? GestureEventForwarded;

    public async Task RouteAsync(VisionGestureEvent gestureEvent, CancellationToken cancellationToken = default)
    {
        if (_motionControlModeService is not null)
        {
            await _motionControlModeService.HandleGestureAsync(gestureEvent, cancellationToken);
            return;
        }

        var browserPointerActive = _browserMotionOverlayModeService?.IsActive == true;
        if (!_uiControlModeController.IsActive && !browserPointerActive)
        {
            _logger.LogInformation(
                "VisionGestureEventIgnoredBecauseControlModesOff Type: {Type}. PointerId: {PointerId}.",
                gestureEvent.Type,
                gestureEvent.PointerId);
            return;
        }

        if (browserPointerActive)
        {
            if (!string.Equals(gestureEvent.Type, "gesture.pinch.end", StringComparison.OrdinalIgnoreCase))
            {
                await _browserMotionOverlayModeService!.UpdatePointerAsync(gestureEvent, cancellationToken);
            }

            if (_browserPinchClickController is not null)
            {
                await _browserPinchClickController.HandleGestureAsync(gestureEvent, cancellationToken);
            }
        }

        _logger.LogDebug(
            "VisionGestureEventForwarded Type: {Type}. PointerId: {PointerId}. Source: {Source}.",
            gestureEvent.Type,
            gestureEvent.PointerId,
            gestureEvent.Source);

        var handlers = GestureEventForwarded;
        if (handlers is not null)
        {
            await handlers.Invoke(gestureEvent, cancellationToken);
        }
    }
}
