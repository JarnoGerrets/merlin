using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.BrowserWorkspace.PageControl;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Motion;

namespace Merlin.Backend.Services.BrowserWorkspace;

public interface IBrowserWorkspaceService : IBrowserPageSnapshotService
{
    event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

    bool IsActive { get; }

    BrowserWorkspaceBounds? CurrentBounds { get; }

    bool OpenUrlsInsideWorkspaceWhenActive { get; }

    Task OpenAsync(string? initialUrl = null, CancellationToken cancellationToken = default);

    Task NavigateAsync(string url, CancellationToken cancellationToken = default);

    Task BackAsync(CancellationToken cancellationToken = default);

    Task ForwardAsync(CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    Task ScrollAsync(
        BrowserScrollDirection direction,
        BrowserScrollAmount amount,
        CancellationToken cancellationToken = default);

    Task ScrollToTopAsync(CancellationToken cancellationToken = default);

    Task ScrollToBottomAsync(CancellationToken cancellationToken = default);

    Task ZoomInAsync(CancellationToken cancellationToken = default);

    Task ZoomOutAsync(CancellationToken cancellationToken = default);

    Task ResetZoomAsync(CancellationToken cancellationToken = default);

    Task SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<BrowserPageActionResult> SearchCurrentPageAsync(
        string query,
        string? preferredElementId = null,
        CancellationToken cancellationToken = default);

    Task<BrowserPageActionResult> ClickVisibleElementAsync(
        string? query,
        string? targetKind = null,
        int? ordinal = null,
        CancellationToken cancellationToken = default);

    Task<BrowserPageActionResult> PerformCommonActionAsync(
        string action,
        CancellationToken cancellationToken = default);

    Task<BrowserPageActionResult> ConfirmBrowserPageClickAsync(
        BrowserPagePendingConfirmation pending,
        CancellationToken cancellationToken = default);

    Task UpdateBrowserPointerOverlayAsync(
        BrowserPointerRenderState state,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task FireBrowserPointerClickAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task ScrollByPixelsAsync(int deltaY, CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task CloseAsync(CancellationToken cancellationToken = default);
}
