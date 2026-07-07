namespace Merlin.Backend.Services.BrowserWorkspace.Motion;

using Merlin.Backend.Services.BrowserWorkspace;

public sealed class BrowserPointerMapper
{
    public const double DefaultMinimumReliableConfidence = 0.45;
    private const double Smoothing = 0.32;

    private double? _lastOverlayX;
    private double? _lastOverlayY;

    public BrowserPointerRenderState Map(
        BrowserPointerMappingInput input,
        BrowserPointerRenderState? previous = null)
    {
        var bounds = input.Bounds;
        var handInFrame = input.NormalizedX is not null
            && input.NormalizedY is not null
            && !bounds.IsMinimized
            && bounds.Width > 0
            && bounds.Height > 0;
        var reliable = handInFrame && input.Confidence >= input.MinimumReliableConfidence;

        var normalizedX = Clamp01(input.NormalizedX ?? previous?.NormalizedX ?? 0.5);
        var normalizedY = Clamp01(input.NormalizedY ?? previous?.NormalizedY ?? 0.5);
        var targetX = normalizedX * bounds.Width;
        var targetY = normalizedY * bounds.Height;

        var overlayX = targetX;
        var overlayY = targetY;
        if (reliable && _lastOverlayX is not null && _lastOverlayY is not null)
        {
            overlayX = _lastOverlayX.Value + ((targetX - _lastOverlayX.Value) * Smoothing);
            overlayY = _lastOverlayY.Value + ((targetY - _lastOverlayY.Value) * Smoothing);
        }
        else if (!reliable && previous is not null)
        {
            overlayX = previous.OverlayX;
            overlayY = previous.OverlayY;
        }

        overlayX = Clamp(overlayX, 0, bounds.Width);
        overlayY = Clamp(overlayY, 0, bounds.Height);

        if (reliable)
        {
            _lastOverlayX = overlayX;
            _lastOverlayY = overlayY;
        }

        return new BrowserPointerRenderState(
            IsActive: input.IsActive,
            IsTrackingReliable: reliable,
            IsHandInFrame: handInFrame,
            NormalizedX: normalizedX,
            NormalizedY: normalizedY,
            OverlayX: overlayX,
            OverlayY: overlayY,
            Confidence: input.Confidence,
            LastUpdatedUtc: DateTimeOffset.UtcNow,
            Bounds: bounds,
            CanClickEventually: reliable,
            ClickVisualState: previous?.ClickVisualState ?? BrowserPointerClickVisualStates.Normal);
    }

    public void Reset()
    {
        _lastOverlayX = null;
        _lastOverlayY = null;
    }

    private static double Clamp01(double value) => Clamp(value, 0, 1);

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return min;
        }

        return Math.Min(max, Math.Max(min, value));
    }
}

public sealed record BrowserPointerMappingInput(
    bool IsActive,
    BrowserWorkspaceBounds Bounds,
    double? NormalizedX,
    double? NormalizedY,
    double Confidence,
    double MinimumReliableConfidence = BrowserPointerMapper.DefaultMinimumReliableConfidence);
