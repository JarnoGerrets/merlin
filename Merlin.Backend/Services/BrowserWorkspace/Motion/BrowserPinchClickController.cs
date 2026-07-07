using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.Vision;

namespace Merlin.Backend.Services.BrowserWorkspace.Motion;

public sealed class BrowserPinchClickController
{
    private readonly IBrowserWorkspaceService _browserWorkspace;
    private readonly BrowserMotionOverlayModeService _browserPointerMode;
    private readonly ILogger<BrowserPinchClickController> _logger;
    private readonly BrowserPinchClickStateMachine _stateMachine;
    private readonly BrowserScrollCommandService _scrollCommandService;
    private readonly object _gate = new();
    private BrowserPointerRenderState? _latestPointerState;
    private string _lastVisualState = BrowserPointerClickVisualStates.Normal;
    private BrowserPinchClickPhase _lastPhase = BrowserPinchClickPhase.OpenHand;

    public BrowserPinchClickController(
        IBrowserWorkspaceService browserWorkspace,
        BrowserMotionOverlayModeService browserPointerMode,
        ILogger<BrowserPinchClickController> logger)
        : this(
            browserWorkspace,
            browserPointerMode,
            logger,
            new BrowserPinchClickStateMachine(),
            new BrowserScrollCommandService(browserWorkspace))
    {
    }

    public BrowserPinchClickController(
        IBrowserWorkspaceService browserWorkspace,
        BrowserMotionOverlayModeService browserPointerMode,
        ILogger<BrowserPinchClickController> logger,
        BrowserPinchClickStateMachine stateMachine,
        BrowserScrollCommandService? scrollCommandService = null)
    {
        _browserWorkspace = browserWorkspace;
        _browserPointerMode = browserPointerMode;
        _logger = logger;
        _stateMachine = stateMachine;
        _scrollCommandService = scrollCommandService ?? new BrowserScrollCommandService(browserWorkspace);
        _latestPointerState = browserPointerMode.CurrentState;
        _browserPointerMode.StateChanged += OnBrowserPointerStateChangedAsync;
        _browserWorkspace.StateChanged += OnBrowserWorkspaceStateChangedAsync;
    }

    public async Task HandleGestureAsync(VisionGestureEvent gestureEvent, CancellationToken cancellationToken = default)
    {
        if (!IsPinchGesture(gestureEvent.Type))
        {
            return;
        }

        var pinchDetected = IsPinchActive(gestureEvent.Type);
        var now = DateTimeOffset.UtcNow;
        var eligibility = GetEligibility(pinchDetected);
        if (!eligibility.Allowed)
        {
            if (pinchDetected)
            {
                _logger.LogInformation(
                    "BrowserPinchClickBlocked Reason: {Reason}. Type: {Type}. Confidence: {Confidence}.",
                    eligibility.Reason,
                    gestureEvent.Type,
                    gestureEvent.Confidence);
            }

            _stateMachine.Cancel(eligibility.VisualState);
            await SetVisualStateAsync(eligibility.VisualState, cancellationToken);
            return;
        }

        var overlayY = GetLatestOverlayY();
        var snapshot = _stateMachine.Update(pinchDetected, eligible: true, now, overlayY);
        LogPhaseTransition(snapshot.Phase);
        await SetVisualStateAsync(snapshot.VisualState, cancellationToken);

        if (snapshot.Phase is BrowserPinchClickPhase.PinchCandidate)
        {
            _logger.LogDebug("BrowserPinchCandidate");
        }

        if (snapshot.Phase is BrowserPinchClickPhase.Scrolling)
        {
            if (snapshot.ShouldScroll)
            {
                var sent = await _scrollCommandService.TrySendAsync(snapshot.ScrollDeltaY, now, cancellationToken);
                if (sent)
                {
                    _logger.LogDebug("BrowserScrollDeltaSent OverlayDeltaY: {OverlayDeltaY}.", snapshot.ScrollDeltaY);
                }
            }

            return;
        }

        if (!snapshot.ShouldClick)
        {
            return;
        }

        if (!HasClickCoordinate())
        {
            _logger.LogInformation("BrowserPinchClickBlocked Reason: outside_bounds.");
            _stateMachine.Cancel();
            await SetVisualStateAsync(BrowserPointerClickVisualStates.Normal, cancellationToken);
            return;
        }

        await _browserWorkspace.FireBrowserPointerClickAsync(cancellationToken);
        _logger.LogInformation("BrowserPinchClickSent Target: browser_host_current_pointer.");
    }

    public async Task ResetAsync(string reason, CancellationToken cancellationToken = default)
    {
        _stateMachine.Cancel();
        _scrollCommandService.Reset();
        _lastPhase = BrowserPinchClickPhase.OpenHand;
        _logger.LogInformation("BrowserPinchCancelled Reason: {Reason}.", reason);
        await SetVisualStateAsync(BrowserPointerClickVisualStates.Normal, cancellationToken);
    }

    private Task OnBrowserPointerStateChangedAsync(BrowserPointerRenderState state, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _latestPointerState = state;
        }

        if (!state.IsActive || !state.IsHandInFrame || !state.IsTrackingReliable || state.Bounds?.IsMinimized == true)
        {
            var wasScrolling = _lastPhase is BrowserPinchClickPhase.Scrolling;
            _stateMachine.Cancel(state.IsActive ? BrowserPointerClickVisualStates.LowConfidence : BrowserPointerClickVisualStates.Normal);
            _scrollCommandService.Reset();
            if (wasScrolling)
            {
                _logger.LogInformation(
                    "BrowserScrollGestureCancelled Reason: {Reason}.",
                    state.IsActive ? "low_confidence" : "overlay_inactive");
                _lastPhase = BrowserPinchClickPhase.OpenHand;
            }
        }

        return Task.CompletedTask;
    }

    private async Task OnBrowserWorkspaceStateChangedAsync(BrowserWorkspaceStateChanged state, CancellationToken cancellationToken)
    {
        if (!state.Active || state.Bounds is null || state.Bounds.IsMinimized)
        {
            await ResetAsync("browser_workspace_unavailable", cancellationToken);
        }
    }

    private BrowserPinchClickEligibility GetEligibility(bool pinchDetected)
    {
        BrowserPointerRenderState? state;
        lock (_gate)
        {
            state = _latestPointerState;
        }

        if (!_browserWorkspace.IsActive)
        {
            return BrowserPinchClickEligibility.Blocked("browser_inactive");
        }

        if (!_browserPointerMode.IsActive || state is null || !state.IsActive)
        {
            return BrowserPinchClickEligibility.Blocked("overlay_inactive");
        }

        if (state.Bounds is null)
        {
            return BrowserPinchClickEligibility.Blocked("bounds_unavailable");
        }

        if (state.Bounds.IsMinimized)
        {
            return BrowserPinchClickEligibility.Blocked("browser_minimized");
        }

        if (!state.IsHandInFrame)
        {
            return BrowserPinchClickEligibility.Blocked("hand_lost");
        }

        if (!state.IsTrackingReliable || state.Confidence < BrowserPointerMapper.DefaultMinimumReliableConfidence)
        {
            return BrowserPinchClickEligibility.Blocked("low_confidence", BrowserPointerClickVisualStates.LowConfidence);
        }

        if (!pinchDetected)
        {
            return BrowserPinchClickEligibility.Permit();
        }

        if (!IsInsideBounds(state))
        {
            return BrowserPinchClickEligibility.Blocked("outside_bounds");
        }

        return BrowserPinchClickEligibility.Permit();
    }

    private bool HasClickCoordinate()
    {
        BrowserPointerRenderState? state;
        lock (_gate)
        {
            state = _latestPointerState;
        }

        return state?.Bounds is not null && IsInsideBounds(state);
    }

    private double? GetLatestOverlayY()
    {
        BrowserPointerRenderState? state;
        lock (_gate)
        {
            state = _latestPointerState;
        }

        return state?.OverlayY;
    }

    private static bool IsInsideBounds(BrowserPointerRenderState state)
    {
        if (state.Bounds is null)
        {
            return false;
        }

        return state.OverlayX >= 0
            && state.OverlayY >= 0
            && state.OverlayX <= state.Bounds.Width
            && state.OverlayY <= state.Bounds.Height;
    }

    private async Task SetVisualStateAsync(string visualState, CancellationToken cancellationToken)
    {
        if (string.Equals(_lastVisualState, visualState, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastVisualState = visualState;
        await _browserPointerMode.SetClickVisualStateAsync(visualState, cancellationToken);
    }

    private void LogPhaseTransition(BrowserPinchClickPhase phase)
    {
        if (phase == _lastPhase)
        {
            return;
        }

        if (phase is BrowserPinchClickPhase.Scrolling)
        {
            _logger.LogInformation("BrowserScrollGestureStarted.");
        }
        else if (_lastPhase is BrowserPinchClickPhase.Scrolling)
        {
            _logger.LogInformation("BrowserScrollGestureStopped Reason: pinch_released.");
        }

        _lastPhase = phase;
    }

    private static bool IsPinchGesture(string type) =>
        string.Equals(type, "gesture.pinch.start", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "gesture.pinch.move", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "gesture.pinch.end", StringComparison.OrdinalIgnoreCase);

    private static bool IsPinchActive(string type) =>
        string.Equals(type, "gesture.pinch.start", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "gesture.pinch.move", StringComparison.OrdinalIgnoreCase);

    private readonly record struct BrowserPinchClickEligibility(
        bool Allowed,
        string Reason,
        string VisualState)
    {
        public static BrowserPinchClickEligibility Permit() =>
            new(true, string.Empty, BrowserPointerClickVisualStates.Normal);

        public static BrowserPinchClickEligibility Blocked(
            string reason,
            string visualState = BrowserPointerClickVisualStates.Normal) =>
            new(false, reason, visualState);
    }
}
