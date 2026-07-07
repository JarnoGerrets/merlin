using Merlin.Backend.Services;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.BrowserWorkspace.PageControl;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Motion;
using Merlin.Backend.Services.Vision;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class VisionGestureEventRouterTests
{
    [Fact]
    public async Task RouteAsync_WhenUiControlModeOff_IgnoresGestureEvent()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var router = new VisionGestureEventRouter(controller, NullLogger<VisionGestureEventRouter>.Instance);
        var forwarded = false;
        router.GestureEventForwarded += (_, _) =>
        {
            forwarded = true;
            return Task.CompletedTask;
        };

        await router.RouteAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = 0.5,
            Y = 0.5,
            Source = "webcam"
        });

        Assert.False(forwarded);
    }

    [Fact]
    public async Task RouteAsync_WhenUiControlModeActive_ForwardsGestureEvent()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        controller.Start();
        var router = new VisionGestureEventRouter(controller, NullLogger<VisionGestureEventRouter>.Instance);
        VisionGestureEvent? forwarded = null;
        router.GestureEventForwarded += (gestureEvent, _) =>
        {
            forwarded = gestureEvent;
            return Task.CompletedTask;
        };

        await router.RouteAsync(new VisionGestureEvent
        {
            Type = "gesture.pinch.start",
            PointerId = "primary",
            X = 0.42,
            Y = 0.24,
            Source = "webcam"
        });

        Assert.NotNull(forwarded);
        Assert.Equal("gesture.pinch.start", forwarded.Type);
        Assert.Equal("primary", forwarded.PointerId);
        Assert.Equal(0.42, forwarded.X);
        Assert.Equal(0.24, forwarded.Y);
    }

    [Fact]
    public async Task RouteAsync_WhenUiControlModeActive_ForwardsMultiplePointerIds()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        controller.Start();
        var router = new VisionGestureEventRouter(controller, NullLogger<VisionGestureEventRouter>.Instance);
        var forwardedPointerIds = new List<string>();
        router.GestureEventForwarded += (gestureEvent, _) =>
        {
            forwardedPointerIds.Add(gestureEvent.PointerId);
            return Task.CompletedTask;
        };

        await router.RouteAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = 0.32,
            Y = 0.44,
            Source = "webcam"
        });

        await router.RouteAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "secondary",
            X = 0.71,
            Y = 0.43,
            Source = "webcam"
        });

        Assert.Equal(new[] { "primary", "secondary" }, forwardedPointerIds);
    }

    [Fact]
    public async Task RouteAsync_WhenBrowserPointerModeActive_ForwardsWithoutUiControlMode()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            Bounds = new BrowserWorkspaceBounds(0, 0, 800, 600, false, true)
        };
        var browserPointer = new BrowserMotionOverlayModeService(
            workspace,
            new BrowserPointerMapper(),
            NullLogger<BrowserMotionOverlayModeService>.Instance);
        await browserPointer.EnableAsync();

        var router = new VisionGestureEventRouter(
            controller,
            NullLogger<VisionGestureEventRouter>.Instance,
            browserPointer);
        VisionGestureEvent? forwarded = null;
        router.GestureEventForwarded += (gestureEvent, _) =>
        {
            forwarded = gestureEvent;
            return Task.CompletedTask;
        };

        await router.RouteAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = 0.25,
            Y = 0.75,
            Confidence = 0.9,
            Source = "webcam"
        });

        Assert.NotNull(forwarded);
        Assert.True(browserPointer.CurrentState.IsTrackingReliable);
        Assert.Equal(200, browserPointer.CurrentState.OverlayX);
        Assert.Equal(450, browserPointer.CurrentState.OverlayY);
    }

    [Fact]
    public async Task RouteAsync_WhenMotionControlModeServiceProvided_DelegatesToMotionService()
    {
        var controller = new UiControlModeController(NullLogger<UiControlModeController>.Instance);
        var motionService = new FakeMotionControlModeService();
        var router = new VisionGestureEventRouter(
            controller,
            NullLogger<VisionGestureEventRouter>.Instance,
            motionControlModeService: motionService);
        var legacyForwarded = false;
        router.GestureEventForwarded += (_, _) =>
        {
            legacyForwarded = true;
            return Task.CompletedTask;
        };

        await router.RouteAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = 0.25,
            Y = 0.75,
            Source = "webcam"
        });

        Assert.False(legacyForwarded);
        Assert.NotNull(motionService.LastGesture);
        Assert.Equal("gesture.pointer.move", motionService.LastGesture.Type);
    }

    private sealed class FakeBrowserWorkspaceService : IBrowserWorkspaceService
    {
        public event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

        public bool IsActive { get; set; }

        public BrowserWorkspaceBounds? Bounds { get; set; }

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
        public Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult<BrowserPageSnapshot?>(null);
        public Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(BrowserSnapshotFreshnessPolicy policy, CancellationToken cancellationToken = default) => Task.FromResult<BrowserPageSnapshot?>(null);
    }

    private sealed class FakeMotionControlModeService : IMotionControlModeService
    {
        public event Func<VisionGestureEvent, CancellationToken, Task>? DashboardGestureForwarded;

        public MotionControlModeSnapshot Current { get; private set; } =
            MotionControlModeSnapshot.Disabled(KnownSurfaces.Dashboard(DateTimeOffset.UtcNow), "test");

        public bool IsEnabled => Current.IsEnabled;

        public VisionGestureEvent? LastGesture { get; private set; }

        public Task<MotionControlModeSnapshot> EnableAsync(
            string reason,
            MotionControlProfileOverride? profileOverride = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<MotionControlModeSnapshot> DisableAsync(
            string reason,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task HandleGestureAsync(VisionGestureEvent gestureEvent, CancellationToken cancellationToken = default)
        {
            LastGesture = gestureEvent;
            return Task.CompletedTask;
        }

        public Task OnActiveSurfaceChangedAsync(
            ActiveSurfaceSnapshot activeSurface,
            string reason,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
