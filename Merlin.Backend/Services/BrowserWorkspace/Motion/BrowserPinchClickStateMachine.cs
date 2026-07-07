namespace Merlin.Backend.Services.BrowserWorkspace.Motion;

public sealed class BrowserPinchClickStateMachine
{
    private readonly TimeSpan _armDuration;
    private readonly TimeSpan _scrollHoldDuration;
    private readonly TimeSpan _cooldown;
    private readonly double _scrollMovementThresholdPixels;
    private bool _pinchDown;
    private bool _scrolling;
    private bool _releaseRequiredAfterCancel;
    private DateTimeOffset? _candidateSince;
    private double? _pinchStartY;
    private double? _lastScrollY;
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;

    public BrowserPinchClickStateMachine()
        : this(
            TimeSpan.FromMilliseconds(120),
            TimeSpan.FromMilliseconds(250),
            scrollMovementThresholdPixels: 28,
            TimeSpan.FromMilliseconds(320))
    {
    }

    public BrowserPinchClickStateMachine(TimeSpan armDuration, TimeSpan cooldown)
        : this(armDuration, TimeSpan.FromMilliseconds(250), scrollMovementThresholdPixels: 28, cooldown)
    {
    }

    public BrowserPinchClickStateMachine(
        TimeSpan armDuration,
        TimeSpan scrollHoldDuration,
        double scrollMovementThresholdPixels,
        TimeSpan cooldown)
    {
        _armDuration = armDuration;
        _scrollHoldDuration = scrollHoldDuration;
        _scrollMovementThresholdPixels = Math.Max(1, scrollMovementThresholdPixels);
        _cooldown = cooldown;
    }

    public BrowserPinchClickStateSnapshot Current { get; private set; } =
        new(BrowserPinchClickPhase.OpenHand, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);

    public BrowserPinchClickStateSnapshot Update(
        bool pinchDetected,
        bool eligible,
        DateTimeOffset now,
        double? overlayY = null)
    {
        if (!eligible)
        {
            Cancel(BrowserPointerClickVisualStates.LowConfidence);
            return Current;
        }

        if (!pinchDetected)
        {
            var wasScrolling = _scrolling;
            var shouldClick = _pinchDown
                && !_scrolling
                && _candidateSince is { } candidateSince
                && now >= _cooldownUntil
                && now - candidateSince >= _armDuration;

            _pinchDown = false;
            _scrolling = false;
            _releaseRequiredAfterCancel = false;
            _candidateSince = null;
            _pinchStartY = null;
            _lastScrollY = null;

            if (shouldClick)
            {
                _cooldownUntil = now + _cooldown;
                Current = new(BrowserPinchClickPhase.ClickSent, ShouldClick: true, ShouldScroll: false, ScrollDeltaY: 0);
                return Current;
            }

            if (wasScrolling)
            {
                _cooldownUntil = now + _cooldown;
                Current = new(BrowserPinchClickPhase.Cooldown, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
                return Current;
            }

            Current = now < _cooldownUntil
                ? new(BrowserPinchClickPhase.Cooldown, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0)
                : new(BrowserPinchClickPhase.OpenHand, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
            return Current;
        }

        if (_releaseRequiredAfterCancel)
        {
            Current = new(BrowserPinchClickPhase.LowConfidence, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
            return Current;
        }

        if (now < _cooldownUntil)
        {
            _pinchDown = true;
            Current = new(BrowserPinchClickPhase.Cooldown, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
            return Current;
        }

        if (!_pinchDown)
        {
            _pinchDown = true;
            _scrolling = false;
            _candidateSince = now;
            _pinchStartY = overlayY;
            _lastScrollY = overlayY;
            Current = new(BrowserPinchClickPhase.PinchCandidate, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
            return Current;
        }

        var candidateStart = _candidateSince ?? now;
        if (_scrolling)
        {
            var deltaY = CalculateDeltaY(overlayY);
            Current = new(BrowserPinchClickPhase.Scrolling, ShouldClick: false, ShouldScroll: Math.Abs(deltaY) > 0.01, ScrollDeltaY: deltaY);
            return Current;
        }

        var movementFromStart = overlayY is { } currentY && _pinchStartY is { } startY
            ? currentY - startY
            : 0;
        var heldLongEnoughForScroll = now - candidateStart >= _scrollHoldDuration;
        if (heldLongEnoughForScroll && Math.Abs(movementFromStart) >= _scrollMovementThresholdPixels)
        {
            _scrolling = true;
            _lastScrollY = overlayY;
            Current = new(BrowserPinchClickPhase.Scrolling, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
            return Current;
        }

        Current = now - candidateStart >= _armDuration
            ? new(BrowserPinchClickPhase.PinchArmed, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0)
            : new(BrowserPinchClickPhase.PinchCandidate, ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
        return Current;
    }

    public void Cancel(string visualState = BrowserPointerClickVisualStates.Normal)
    {
        var requireRelease = _pinchDown || _candidateSince is not null;
        _pinchDown = false;
        _scrolling = false;
        _releaseRequiredAfterCancel = requireRelease;
        _candidateSince = null;
        _pinchStartY = null;
        _lastScrollY = null;
        Current = new(PhaseFromVisualState(visualState), ShouldClick: false, ShouldScroll: false, ScrollDeltaY: 0);
    }

    private double CalculateDeltaY(double? overlayY)
    {
        if (overlayY is not { } currentY)
        {
            return 0;
        }

        var previousY = _lastScrollY ?? currentY;
        _lastScrollY = currentY;
        return currentY - previousY;
    }

    private static BrowserPinchClickPhase PhaseFromVisualState(string visualState) =>
        string.Equals(visualState, BrowserPointerClickVisualStates.LowConfidence, StringComparison.OrdinalIgnoreCase)
            ? BrowserPinchClickPhase.LowConfidence
            : BrowserPinchClickPhase.OpenHand;
}

public sealed record BrowserPinchClickStateSnapshot(
    BrowserPinchClickPhase Phase,
    bool ShouldClick,
    bool ShouldScroll,
    double ScrollDeltaY)
{
    public string VisualState => Phase switch
    {
        BrowserPinchClickPhase.PinchCandidate => BrowserPointerClickVisualStates.PinchCandidate,
        BrowserPinchClickPhase.PinchArmed => BrowserPointerClickVisualStates.PinchArmed,
        BrowserPinchClickPhase.ClickSent => BrowserPointerClickVisualStates.ClickSent,
        BrowserPinchClickPhase.ScrollCandidate => BrowserPointerClickVisualStates.ScrollCandidate,
        BrowserPinchClickPhase.Scrolling => BrowserPointerClickVisualStates.Scrolling,
        BrowserPinchClickPhase.PinchHeld => BrowserPointerClickVisualStates.PinchArmed,
        BrowserPinchClickPhase.Cooldown => BrowserPointerClickVisualStates.Cooldown,
        BrowserPinchClickPhase.LowConfidence => BrowserPointerClickVisualStates.LowConfidence,
        _ => BrowserPointerClickVisualStates.Normal
    };
}

public enum BrowserPinchClickPhase
{
    OpenHand,
    PinchCandidate,
    PinchArmed,
    ClickSent,
    PinchHeld,
    ScrollCandidate,
    Scrolling,
    Released,
    Cooldown,
    LowConfidence
}
