namespace Merlin.Backend.Services.Vision;

public sealed class VisionGestureEventRouter
{
    private readonly ILogger<VisionGestureEventRouter> _logger;
    private readonly UiControlModeController _uiControlModeController;

    public VisionGestureEventRouter(
        UiControlModeController uiControlModeController,
        ILogger<VisionGestureEventRouter> logger)
    {
        _uiControlModeController = uiControlModeController;
        _logger = logger;
    }

    public event Func<VisionGestureEvent, CancellationToken, Task>? GestureEventForwarded;

    public async Task RouteAsync(VisionGestureEvent gestureEvent, CancellationToken cancellationToken = default)
    {
        if (!_uiControlModeController.IsActive)
        {
            _logger.LogInformation(
                "VisionGestureEventIgnoredBecauseUiControlModeOff Type: {Type}. PointerId: {PointerId}.",
                gestureEvent.Type,
                gestureEvent.PointerId);
            return;
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
