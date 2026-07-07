namespace Merlin.Backend.Services.BrowserWorkspace.Motion;

using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.Vision;

public sealed class BrowserMotionOverlayModeService
{
    private readonly IBrowserWorkspaceService _browserWorkspace;
    private readonly BrowserPointerMapper _mapper;
    private readonly ILogger<BrowserMotionOverlayModeService> _logger;
    private readonly object _gate = new();
    private BrowserPointerRenderState _state;
    private string _clickVisualState = BrowserPointerClickVisualStates.Normal;

    public BrowserMotionOverlayModeService(
        IBrowserWorkspaceService browserWorkspace,
        BrowserPointerMapper mapper,
        ILogger<BrowserMotionOverlayModeService> logger)
    {
        _browserWorkspace = browserWorkspace;
        _mapper = mapper;
        _logger = logger;
        _state = InactiveState(browserWorkspace.CurrentBounds);
        _browserWorkspace.StateChanged += OnBrowserWorkspaceStateChangedAsync;
    }

    public event Func<BrowserPointerRenderState, CancellationToken, Task>? StateChanged;

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _state.IsActive;
            }
        }
    }

    public BrowserPointerRenderState CurrentState
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public async Task<BrowserMotionOverlayStartResult> EnableAsync(CancellationToken cancellationToken = default)
    {
        if (!_browserWorkspace.IsActive)
        {
            _logger.LogInformation("BrowserMotionOverlayEnableRejected Reason: browser_not_open.");
            return BrowserMotionOverlayStartResult.BrowserNotOpen;
        }

        var bounds = _browserWorkspace.CurrentBounds;
        if (bounds is null || bounds.IsMinimized || bounds.Width <= 0 || bounds.Height <= 0)
        {
            _logger.LogInformation("BrowserMotionOverlayEnableRejected Reason: browser_unavailable.");
            return BrowserMotionOverlayStartResult.BrowserUnavailable;
        }

        BrowserPointerRenderState state;
        lock (_gate)
        {
            _mapper.Reset();
            _state = new BrowserPointerRenderState(
                IsActive: true,
                IsTrackingReliable: false,
                IsHandInFrame: false,
                NormalizedX: 0.5,
                NormalizedY: 0.5,
                OverlayX: bounds.Width / 2.0,
                OverlayY: bounds.Height / 2.0,
                Confidence: 0,
                LastUpdatedUtc: DateTimeOffset.UtcNow,
                Bounds: bounds,
                CanClickEventually: false,
                ClickVisualState: BrowserPointerClickVisualStates.Normal);
            state = _state;
        }

        _logger.LogInformation("BrowserMotionOverlayEnabled Bounds: {Bounds}.", bounds);
        await _browserWorkspace.UpdateBrowserPointerOverlayAsync(state, cancellationToken);
        await RaiseStateChangedAsync(state, cancellationToken);
        return BrowserMotionOverlayStartResult.Started;
    }

    public async Task DisableAsync(string reason = "disabled", CancellationToken cancellationToken = default)
    {
        BrowserPointerRenderState state;
        lock (_gate)
        {
            if (!_state.IsActive && string.Equals(reason, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _mapper.Reset();
            _clickVisualState = BrowserPointerClickVisualStates.Normal;
            _state = InactiveState(_browserWorkspace.CurrentBounds);
            state = _state;
        }

        _logger.LogInformation("BrowserMotionOverlayDisabled Reason: {Reason}.", reason);
        await _browserWorkspace.UpdateBrowserPointerOverlayAsync(state, cancellationToken);
        await RaiseStateChangedAsync(state, cancellationToken);
    }

    public async Task UpdatePointerAsync(VisionGestureEvent gestureEvent, CancellationToken cancellationToken = default)
    {
        BrowserPointerRenderState? stateToEmit = null;
        lock (_gate)
        {
            if (!_state.IsActive)
            {
                return;
            }

            var bounds = _browserWorkspace.CurrentBounds ?? _state.Bounds;
            if (bounds is null || bounds.IsMinimized || bounds.Width <= 0 || bounds.Height <= 0)
            {
                _state = _state with
                {
                    IsTrackingReliable = false,
                    IsHandInFrame = false,
                    Confidence = gestureEvent.Confidence,
                    LastUpdatedUtc = DateTimeOffset.UtcNow,
                    Bounds = bounds,
                    CanClickEventually = false,
                    ClickVisualState = _clickVisualState
                };
                stateToEmit = _state;
                return;
            }

            _state = _mapper.Map(
                new BrowserPointerMappingInput(
                    IsActive: true,
                    Bounds: bounds,
                    NormalizedX: gestureEvent.X,
                    NormalizedY: gestureEvent.Y,
                    Confidence: gestureEvent.Confidence),
                _state);
            _state = _state with { ClickVisualState = _clickVisualState };
            stateToEmit = _state;
        }

        if (stateToEmit is not null)
        {
            await _browserWorkspace.UpdateBrowserPointerOverlayAsync(stateToEmit, cancellationToken);
            await RaiseStateChangedAsync(stateToEmit, cancellationToken);
        }
    }

    private async Task OnBrowserWorkspaceStateChangedAsync(
        BrowserWorkspaceStateChanged state,
        CancellationToken cancellationToken)
    {
        if (!state.Active || state.Bounds is null)
        {
            lock (_gate)
            {
                if (!_state.IsActive)
                {
                    return;
                }
            }

            await DisableAsync("browser_workspace_unavailable", cancellationToken);
            return;
        }

        BrowserPointerRenderState? stateToEmit = null;
        lock (_gate)
        {
            if (!_state.IsActive)
            {
                return;
            }

            _state = state.Bounds.IsMinimized
                ? _state with
                {
                    IsTrackingReliable = false,
                    IsHandInFrame = false,
                    Confidence = 0,
                    Bounds = state.Bounds,
                    CanClickEventually = false,
                    ClickVisualState = _clickVisualState,
                    LastUpdatedUtc = DateTimeOffset.UtcNow
                }
                : _state with
            {
                Bounds = state.Bounds,
                OverlayX = Math.Clamp(_state.OverlayX, 0, state.Bounds.Width),
                OverlayY = Math.Clamp(_state.OverlayY, 0, state.Bounds.Height),
                ClickVisualState = _clickVisualState,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            stateToEmit = _state;
        }

        if (stateToEmit is not null)
        {
            await _browserWorkspace.UpdateBrowserPointerOverlayAsync(stateToEmit, cancellationToken);
            await RaiseStateChangedAsync(stateToEmit, cancellationToken);
        }
    }

    public async Task SetClickVisualStateAsync(string visualState, CancellationToken cancellationToken = default)
    {
        BrowserPointerRenderState? stateToEmit = null;
        lock (_gate)
        {
            if (!_state.IsActive)
            {
                _clickVisualState = BrowserPointerClickVisualStates.Normal;
                return;
            }

            _clickVisualState = string.IsNullOrWhiteSpace(visualState)
                ? BrowserPointerClickVisualStates.Normal
                : visualState;
            _state = _state with
            {
                ClickVisualState = _clickVisualState,
                LastUpdatedUtc = DateTimeOffset.UtcNow
            };
            stateToEmit = _state;
        }

        await _browserWorkspace.UpdateBrowserPointerOverlayAsync(stateToEmit, cancellationToken);
        await RaiseStateChangedAsync(stateToEmit, cancellationToken);
    }

    private async Task RaiseStateChangedAsync(
        BrowserPointerRenderState state,
        CancellationToken cancellationToken)
    {
        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        await handlers.Invoke(state, cancellationToken);
    }

    private static BrowserPointerRenderState InactiveState(BrowserWorkspaceBounds? bounds) =>
        new(
            IsActive: false,
            IsTrackingReliable: false,
            IsHandInFrame: false,
            NormalizedX: 0.5,
            NormalizedY: 0.5,
            OverlayX: bounds?.Width / 2.0 ?? 0,
            OverlayY: bounds?.Height / 2.0 ?? 0,
            Confidence: 0,
            LastUpdatedUtc: DateTimeOffset.UtcNow,
            Bounds: bounds,
            CanClickEventually: false,
            ClickVisualState: BrowserPointerClickVisualStates.Normal);
}

public enum BrowserMotionOverlayStartResult
{
    Started,
    BrowserNotOpen,
    BrowserUnavailable
}
