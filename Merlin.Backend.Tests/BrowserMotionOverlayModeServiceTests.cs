using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.BrowserWorkspace.PageControl;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.Vision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class BrowserMotionOverlayModeServiceTests
{
    [Fact]
    public async Task EnableAsync_WhenBrowserWorkspaceInactive_DoesNotStart()
    {
        var workspace = new FakeBrowserWorkspaceService { IsActive = false };
        var service = CreateService(workspace);

        var result = await service.EnableAsync();

        Assert.Equal(BrowserMotionOverlayStartResult.BrowserNotOpen, result);
        Assert.False(service.IsActive);
    }

    [Fact]
    public async Task EnableAsync_WhenBrowserWorkspaceActive_Starts()
    {
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            Bounds = new BrowserWorkspaceBounds(100, 120, 800, 600, false, true)
        };
        var service = CreateService(workspace);

        var result = await service.EnableAsync();

        Assert.Equal(BrowserMotionOverlayStartResult.Started, result);
        Assert.True(service.IsActive);
        Assert.Equal(400, service.CurrentState.OverlayX);
        Assert.Equal(300, service.CurrentState.OverlayY);
        Assert.Equal(1, workspace.PointerOverlayUpdateCount);
        Assert.True(workspace.LastPointerState?.IsActive);
    }

    [Fact]
    public async Task BrowserWorkspaceClosed_DisablesOverlay()
    {
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            Bounds = new BrowserWorkspaceBounds(0, 0, 800, 600, false, true)
        };
        var service = CreateService(workspace);
        await service.EnableAsync();

        await workspace.CloseAsync();

        Assert.False(service.IsActive);
        Assert.False(service.CurrentState.IsTrackingReliable);
    }

    [Fact]
    public async Task BrowserWorkspaceMinimized_HidesOverlayButKeepsModeActive()
    {
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            Bounds = new BrowserWorkspaceBounds(0, 0, 800, 600, false, true)
        };
        var service = CreateService(workspace);
        await service.EnableAsync();

        await workspace.PublishBoundsAsync(new BrowserWorkspaceBounds(0, 0, 800, 600, true, false));

        Assert.True(service.IsActive);
        Assert.False(service.CurrentState.IsHandInFrame);
        Assert.False(service.CurrentState.IsTrackingReliable);
        Assert.Equal(2, workspace.PointerOverlayUpdateCount);
        Assert.True(workspace.LastPointerState?.IsActive);
    }

    [Fact]
    public async Task BrowserWorkspaceRestored_RestoresOverlayWhileModeRemainsActive()
    {
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            Bounds = new BrowserWorkspaceBounds(0, 0, 800, 600, false, true)
        };
        var service = CreateService(workspace);
        await service.EnableAsync();
        await workspace.PublishBoundsAsync(new BrowserWorkspaceBounds(0, 0, 800, 600, true, false));

        await workspace.PublishBoundsAsync(new BrowserWorkspaceBounds(50, 80, 1000, 700, false, true));

        Assert.True(service.IsActive);
        Assert.Equal(1000, service.CurrentState.Bounds?.Width);
        Assert.Equal(700, service.CurrentState.Bounds?.Height);
        Assert.Equal(3, workspace.PointerOverlayUpdateCount);
        Assert.True(workspace.LastPointerState?.IsActive);
    }

    [Fact]
    public void Mapper_ClampsCoordinatesInsideBrowserBounds()
    {
        var mapper = new BrowserPointerMapper();

        var state = mapper.Map(new BrowserPointerMappingInput(
            true,
            new BrowserWorkspaceBounds(0, 0, 320, 200, false, true),
            2.0,
            -1.0,
            0.9));

        Assert.Equal(320, state.OverlayX);
        Assert.Equal(0, state.OverlayY);
        Assert.True(state.IsTrackingReliable);
    }

    [Fact]
    public void Mapper_LowConfidenceMarksPointerUnreliable()
    {
        var mapper = new BrowserPointerMapper();

        var state = mapper.Map(new BrowserPointerMappingInput(
            true,
            new BrowserWorkspaceBounds(0, 0, 320, 200, false, true),
            0.5,
            0.5,
            0.1));

        Assert.False(state.IsTrackingReliable);
        Assert.True(state.IsHandInFrame);
        Assert.False(state.CanClickEventually);
    }

    [Fact]
    public void Mapper_HandLostPreventsReliablePointer()
    {
        var mapper = new BrowserPointerMapper();

        var state = mapper.Map(new BrowserPointerMappingInput(
            true,
            new BrowserWorkspaceBounds(0, 0, 320, 200, false, true),
            null,
            null,
            0.9));

        Assert.False(state.IsTrackingReliable);
        Assert.False(state.IsHandInFrame);
        Assert.False(state.CanClickEventually);
    }

    private static BrowserMotionOverlayModeService CreateService(FakeBrowserWorkspaceService workspace) =>
        new(
            workspace,
            new BrowserPointerMapper(),
            NullLogger<BrowserMotionOverlayModeService>.Instance);

    private sealed class FakeBrowserWorkspaceService : IBrowserWorkspaceService
    {
        public event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

        public bool IsActive { get; set; }

        public BrowserWorkspaceBounds? Bounds { get; set; }

        public int PointerOverlayUpdateCount { get; private set; }

        public BrowserPointerRenderState? LastPointerState { get; private set; }

        public BrowserWorkspaceBounds? CurrentBounds => Bounds;

        public BrowserPageSnapshot? LatestSnapshot => null;

        public bool OpenUrlsInsideWorkspaceWhenActive => true;

        public Task OpenAsync(string? initialUrl = null, CancellationToken cancellationToken = default)
        {
            IsActive = true;
            Bounds ??= new BrowserWorkspaceBounds(0, 0, 800, 600, false, true);
            return StateChanged?.Invoke(new BrowserWorkspaceStateChanged(true, Bounds, "test"), cancellationToken)
                ?? Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            IsActive = false;
            Bounds = null;
            return StateChanged?.Invoke(new BrowserWorkspaceStateChanged(false, null, "test"), cancellationToken)
                ?? Task.CompletedTask;
        }

        public Task PublishBoundsAsync(BrowserWorkspaceBounds bounds, CancellationToken cancellationToken = default)
        {
            Bounds = bounds;
            return StateChanged?.Invoke(new BrowserWorkspaceStateChanged(true, bounds, "test_bounds"), cancellationToken)
                ?? Task.CompletedTask;
        }

        public Task NavigateAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task BackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ForwardAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ScrollAsync(BrowserScrollDirection direction, BrowserScrollAmount amount, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ScrollToTopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ScrollToBottomAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ZoomInAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ZoomOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetZoomAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SearchAsync(string query, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<BrowserPageActionResult> SearchCurrentPageAsync(string query, string? preferredElementId = null, CancellationToken cancellationToken = default) => Task.FromResult(new BrowserPageActionResult { Success = true });
        public Task<BrowserPageActionResult> ClickVisibleElementAsync(string? query, string? targetKind = null, int? ordinal = null, CancellationToken cancellationToken = default) => Task.FromResult(new BrowserPageActionResult { Success = true });
        public Task<BrowserPageActionResult> PerformCommonActionAsync(string action, CancellationToken cancellationToken = default) => Task.FromResult(new BrowserPageActionResult { Success = true });
        public Task<BrowserPageActionResult> ConfirmBrowserPageClickAsync(BrowserPagePendingConfirmation pending, CancellationToken cancellationToken = default) => Task.FromResult(new BrowserPageActionResult { Success = true });
        public Task UpdateBrowserPointerOverlayAsync(BrowserPointerRenderState state, CancellationToken cancellationToken = default)
        {
            PointerOverlayUpdateCount++;
            LastPointerState = state;
            return Task.CompletedTask;
        }

        public Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult<BrowserPageSnapshot?>(null);
        public Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(BrowserSnapshotFreshnessPolicy policy, CancellationToken cancellationToken = default) => Task.FromResult<BrowserPageSnapshot?>(null);
    }
}
