using Merlin.Backend.Services.BrowserWorkspace;

namespace Merlin.Backend.Services.BrowserWorkspace.Motion;

public sealed class BrowserScrollCommandService
{
    private readonly IBrowserWorkspaceService _browserWorkspace;
    private readonly double _pixelsPerOverlayPixel;
    private readonly TimeSpan _minimumInterval;
    private DateTimeOffset _lastSentAt = DateTimeOffset.MinValue;
    private double _accumulatedOverlayDeltaY;

    public BrowserScrollCommandService(
        IBrowserWorkspaceService browserWorkspace,
        double pixelsPerOverlayPixel = 3.0,
        TimeSpan? minimumInterval = null)
    {
        _browserWorkspace = browserWorkspace;
        _pixelsPerOverlayPixel = pixelsPerOverlayPixel;
        _minimumInterval = minimumInterval ?? TimeSpan.FromMilliseconds(25);
    }

    public async Task<bool> TrySendAsync(
        double overlayDeltaY,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (Math.Abs(overlayDeltaY) < 0.01)
        {
            return false;
        }

        _accumulatedOverlayDeltaY += overlayDeltaY;
        if (now - _lastSentAt < _minimumInterval)
        {
            return false;
        }

        // Touch-style mapping: hand up (negative overlay delta) scrolls the page down.
        var deltaY = (int)Math.Round(-_accumulatedOverlayDeltaY * _pixelsPerOverlayPixel);
        _accumulatedOverlayDeltaY = 0;
        _lastSentAt = now;
        if (deltaY == 0)
        {
            return false;
        }

        await _browserWorkspace.ScrollByPixelsAsync(deltaY, cancellationToken);
        return true;
    }

    public void Reset()
    {
        _accumulatedOverlayDeltaY = 0;
        _lastSentAt = DateTimeOffset.MinValue;
    }
}
