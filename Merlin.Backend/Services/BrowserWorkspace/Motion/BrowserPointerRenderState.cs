namespace Merlin.Backend.Services.BrowserWorkspace.Motion;

using Merlin.Backend.Services.BrowserWorkspace;

public sealed record BrowserPointerRenderState(
    bool IsActive,
    bool IsTrackingReliable,
    bool IsHandInFrame,
    double NormalizedX,
    double NormalizedY,
    double OverlayX,
    double OverlayY,
    double Confidence,
    DateTimeOffset LastUpdatedUtc,
    BrowserWorkspaceBounds? Bounds,
    bool CanClickEventually = false,
    string ClickVisualState = BrowserPointerClickVisualStates.Normal);

public static class BrowserPointerClickVisualStates
{
    public const string Normal = "normal";
    public const string PinchCandidate = "pinch_candidate";
    public const string PinchArmed = "pinch_armed";
    public const string ClickSent = "click_sent";
    public const string ScrollCandidate = "scroll_candidate";
    public const string Scrolling = "scrolling";
    public const string Cooldown = "cooldown";
    public const string LowConfidence = "low_confidence";
}
