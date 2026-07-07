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

public sealed class BrowserPinchClickControllerTests
{
    [Fact]
    public void StateMachine_StablePinchProducesExactlyOneClick()
    {
        var machine = new BrowserPinchClickStateMachine(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        Assert.False(machine.Update(true, eligible: true, now, overlayY: 300).ShouldClick);
        Assert.False(machine.Update(true, eligible: true, now.AddMilliseconds(110), overlayY: 300).ShouldClick);
        Assert.True(machine.Update(false, eligible: true, now.AddMilliseconds(130), overlayY: 300).ShouldClick);
    }

    [Fact]
    public void StateMachine_ShortPinchDoesNotClick()
    {
        var machine = new BrowserPinchClickStateMachine(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        machine.Update(true, eligible: true, now, overlayY: 300);
        var release = machine.Update(false, eligible: true, now.AddMilliseconds(60), overlayY: 300);

        Assert.False(release.ShouldClick);
    }

    [Fact]
    public void StateMachine_ReleaseCooldownAndNewPinchAllowsSecondClick()
    {
        var machine = new BrowserPinchClickStateMachine(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        machine.Update(true, eligible: true, now, overlayY: 300);
        Assert.True(machine.Update(false, eligible: true, now.AddMilliseconds(130), overlayY: 300).ShouldClick);
        machine.Update(true, eligible: true, now.AddMilliseconds(200), overlayY: 300);
        Assert.False(machine.Update(false, eligible: true, now.AddMilliseconds(260), overlayY: 300).ShouldClick);
        machine.Update(true, eligible: true, now.AddMilliseconds(520), overlayY: 300);
        Assert.True(machine.Update(false, eligible: true, now.AddMilliseconds(640), overlayY: 300).ShouldClick);
    }

    [Fact]
    public void StateMachine_LowEligibilityCancelsPinch()
    {
        var machine = new BrowserPinchClickStateMachine(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        machine.Update(true, eligible: true, now, overlayY: 300);
        var blocked = machine.Update(true, eligible: false, now.AddMilliseconds(120), overlayY: 300);

        Assert.False(blocked.ShouldClick);
        Assert.Equal(BrowserPinchClickPhase.LowConfidence, blocked.Phase);
    }

    [Fact]
    public void StateMachine_CancelledPinchRequiresReleaseBeforeClicking()
    {
        var machine = new BrowserPinchClickStateMachine(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        machine.Update(true, eligible: true, now, overlayY: 300);
        machine.Update(true, eligible: false, now.AddMilliseconds(20), overlayY: 300);
        Assert.False(machine.Update(true, eligible: true, now.AddMilliseconds(200), overlayY: 300).ShouldClick);
        machine.Update(false, eligible: true, now.AddMilliseconds(220), overlayY: 300);
        machine.Update(true, eligible: true, now.AddMilliseconds(240), overlayY: 300);

        Assert.True(machine.Update(false, eligible: true, now.AddMilliseconds(360), overlayY: 300).ShouldClick);
    }

    [Fact]
    public void StateMachine_PinchHoldWithVerticalMovementEntersScrollMode()
    {
        var machine = new BrowserPinchClickStateMachine(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250),
            scrollMovementThresholdPixels: 20,
            TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        machine.Update(true, eligible: true, now, overlayY: 300);
        var startScroll = machine.Update(true, eligible: true, now.AddMilliseconds(260), overlayY: 270);
        var scrollMove = machine.Update(true, eligible: true, now.AddMilliseconds(290), overlayY: 250);

        Assert.Equal(BrowserPinchClickPhase.Scrolling, startScroll.Phase);
        Assert.Equal(BrowserPinchClickPhase.Scrolling, scrollMove.Phase);
        Assert.True(scrollMove.ShouldScroll);
        Assert.False(scrollMove.ShouldClick);
    }

    [Fact]
    public void StateMachine_ScrollReleaseDoesNotClick()
    {
        var machine = new BrowserPinchClickStateMachine(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250),
            scrollMovementThresholdPixels: 20,
            TimeSpan.FromMilliseconds(300));
        var now = DateTimeOffset.UtcNow;

        machine.Update(true, eligible: true, now, overlayY: 300);
        machine.Update(true, eligible: true, now.AddMilliseconds(260), overlayY: 270);
        var release = machine.Update(false, eligible: true, now.AddMilliseconds(300), overlayY: 270);

        Assert.False(release.ShouldClick);
        Assert.False(release.ShouldScroll);
    }

    [Fact]
    public async Task Controller_StablePinchSendsOneClickAtPointerCoordinate()
    {
        var (controller, pointerMode, workspace) = CreateActiveController();
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));

        await controller.HandleGestureAsync(PinchStart());
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove());
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Equal(1, workspace.PointerClicks);
        Assert.True(
            workspace.LastPointerState?.ClickVisualState is BrowserPointerClickVisualStates.ClickSent
                or BrowserPointerClickVisualStates.Cooldown);
    }

    [Fact]
    public async Task Controller_LowConfidencePreventsClick()
    {
        var (controller, pointerMode, workspace) = CreateActiveController();
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.2));

        await controller.HandleGestureAsync(PinchStart(confidence: 0.2));
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove(confidence: 0.2));
        await controller.HandleGestureAsync(PinchEnd(confidence: 0.2));

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_OutsideBoundsPreventsClick()
    {
        var (controller, pointerMode, workspace) = CreateActiveController();
        await pointerMode.EnableAsync();
        InjectLatestPointerState(
            controller,
            new BrowserPointerRenderState(
                IsActive: true,
                IsTrackingReliable: true,
                IsHandInFrame: true,
                NormalizedX: 1,
                NormalizedY: 0.5,
                OverlayX: 900,
                OverlayY: 300,
                Confidence: 0.9,
                LastUpdatedUtc: DateTimeOffset.UtcNow,
                Bounds: new BrowserWorkspaceBounds(100, 120, 800, 600, false, true),
                CanClickEventually: true));

        await controller.HandleGestureAsync(PinchStart());
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove());
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_InactiveBrowserPointerPreventsClick()
    {
        var (controller, _, workspace) = CreateActiveController();

        await controller.HandleGestureAsync(PinchStart());
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove());
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_InactiveBrowserWorkspacePreventsClick()
    {
        var (controller, pointerMode, workspace) = CreateActiveController();
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));
        await workspace.CloseAsync();

        await controller.HandleGestureAsync(PinchStart());
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove());
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_MinimizedBrowserPreventsClick()
    {
        var (controller, pointerMode, workspace) = CreateActiveController();
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));
        await workspace.PublishBoundsAsync(new BrowserWorkspaceBounds(100, 120, 800, 600, true, false));

        await controller.HandleGestureAsync(PinchStart());
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove());
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_ResetPreventsHeldPinchFromClicking()
    {
        var (controller, pointerMode, workspace) = CreateActiveController();
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));

        await controller.HandleGestureAsync(PinchStart());
        await controller.ResetAsync("test_stop");
        await Task.Delay(5);
        await controller.HandleGestureAsync(PinchMove());
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_PinchHoldVerticalMovementConsumesReleaseWithoutClick()
    {
        var (controller, pointerMode, workspace) = CreateActiveController(
            new BrowserPinchClickStateMachine(
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                scrollMovementThresholdPixels: 10,
                TimeSpan.FromMilliseconds(25)));
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));

        await controller.HandleGestureAsync(PinchStart(y: 0.5));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.46));
        await Task.Delay(2);
        await controller.HandleGestureAsync(PinchMove(y: 0.46));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.42));
        await controller.HandleGestureAsync(PinchMove(y: 0.42));
        await controller.HandleGestureAsync(PinchEnd());

        Assert.Empty(workspace.PointerScrolls);
        Assert.Equal(0, workspace.PointerClicks);
        Assert.True(
            workspace.LastPointerState?.ClickVisualState is BrowserPointerClickVisualStates.Scrolling
                or BrowserPointerClickVisualStates.Cooldown
                or BrowserPointerClickVisualStates.Normal);
    }

    [Fact]
    public async Task Controller_ScrollingSendsPixelDeltas()
    {
        var (controller, pointerMode, workspace) = CreateActiveController(
            new BrowserPinchClickStateMachine(
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                scrollMovementThresholdPixels: 10,
                TimeSpan.FromMilliseconds(25)));
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));

        await controller.HandleGestureAsync(PinchStart(y: 0.5));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.46));
        await Task.Delay(2);
        await controller.HandleGestureAsync(PinchMove(y: 0.46));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.40));
        await Task.Delay(2);
        await controller.HandleGestureAsync(PinchMove(y: 0.40));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.34));
        await Task.Delay(2);
        await controller.HandleGestureAsync(PinchMove(y: 0.34));

        Assert.NotEmpty(workspace.PointerScrolls);
        Assert.All(workspace.PointerScrolls, delta => Assert.True(delta > 0));
        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_LowConfidencePreventsScrolling()
    {
        var (controller, pointerMode, workspace) = CreateActiveController(
            new BrowserPinchClickStateMachine(
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                scrollMovementThresholdPixels: 10,
                TimeSpan.FromMilliseconds(25)));
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));

        await controller.HandleGestureAsync(PinchStart(y: 0.5));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.35, confidence: 0.2));
        await controller.HandleGestureAsync(PinchMove(y: 0.35, confidence: 0.2));

        Assert.Empty(workspace.PointerScrolls);
        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public async Task Controller_HandLostCancelsScrolling()
    {
        var (controller, pointerMode, workspace) = CreateActiveController(
            new BrowserPinchClickStateMachine(
                TimeSpan.FromMilliseconds(1),
                TimeSpan.FromMilliseconds(1),
                scrollMovementThresholdPixels: 10,
                TimeSpan.FromMilliseconds(25)));
        await pointerMode.EnableAsync();
        await pointerMode.UpdatePointerAsync(PointerMove(0.5, 0.5, 0.9));
        await controller.HandleGestureAsync(PinchStart(y: 0.5));
        await pointerMode.UpdatePointerAsync(PinchMove(y: 0.4));
        await Task.Delay(2);
        await controller.HandleGestureAsync(PinchMove(y: 0.4));
        await pointerMode.UpdatePointerAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = null,
            Y = null,
            Confidence = 0,
            Source = "webcam"
        });

        await controller.HandleGestureAsync(PinchMove(y: 0.3));

        Assert.Equal(0, workspace.PointerClicks);
    }

    [Fact]
    public void Backend_DoesNotDeclareWin32InputCalls()
    {
        var root = FindRepositoryDirectory();
        var backendFiles = Directory.GetFiles(Path.Combine(root, "Merlin.Backend"), "*.cs", SearchOption.AllDirectories);
        var offenders = backendFiles
            .Where(path =>
            {
                var source = File.ReadAllText(path);
                return source.Contains("SetCursorPos", StringComparison.Ordinal)
                    || source.Contains("SendInput", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Empty(offenders);
    }

    private static (BrowserPinchClickController Controller, BrowserMotionOverlayModeService PointerMode, FakeBrowserWorkspaceService Workspace) CreateActiveController(
        BrowserPinchClickStateMachine? stateMachine = null)
    {
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = true,
            Bounds = new BrowserWorkspaceBounds(100, 120, 800, 600, false, true)
        };
        var pointerMode = new BrowserMotionOverlayModeService(
            workspace,
            new BrowserPointerMapper(),
            NullLogger<BrowserMotionOverlayModeService>.Instance);
        var controller = new BrowserPinchClickController(
            workspace,
            pointerMode,
            NullLogger<BrowserPinchClickController>.Instance,
            stateMachine ?? new BrowserPinchClickStateMachine(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(25)),
            new BrowserScrollCommandService(workspace, minimumInterval: TimeSpan.Zero));
        return (controller, pointerMode, workspace);
    }

    private static VisionGestureEvent PointerMove(double x, double y, double confidence) =>
        new()
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = x,
            Y = y,
            Confidence = confidence,
            Source = "webcam"
        };

    private static VisionGestureEvent PinchStart(double confidence = 0.9, double x = 0.5, double y = 0.5) =>
        new()
        {
            Type = "gesture.pinch.start",
            PointerId = "primary",
            X = x,
            Y = y,
            Confidence = confidence,
            Source = "webcam"
        };

    private static VisionGestureEvent PinchMove(double confidence = 0.9, double x = 0.5, double y = 0.5) =>
        new()
        {
            Type = "gesture.pinch.move",
            PointerId = "primary",
            X = x,
            Y = y,
            Confidence = confidence,
            Source = "webcam"
        };

    private static VisionGestureEvent PinchEnd(double confidence = 0.9) =>
        new()
        {
            Type = "gesture.pinch.end",
            PointerId = "primary",
            Confidence = confidence,
            Source = "webcam"
        };

    private static void InjectLatestPointerState(
        BrowserPinchClickController controller,
        BrowserPointerRenderState state)
    {
        var field = typeof(BrowserPinchClickController).GetField(
            "_latestPointerState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(controller, state);
    }

    private sealed class FakeBrowserWorkspaceService : IBrowserWorkspaceService
    {
        public event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

        public bool IsActive { get; set; }

        public BrowserWorkspaceBounds? Bounds { get; set; }

        public BrowserPointerRenderState? LastPointerState { get; private set; }

        public int PointerClicks { get; private set; }

        public List<int> PointerScrolls { get; } = [];

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
            LastPointerState = state;
            return Task.CompletedTask;
        }

        public Task FireBrowserPointerClickAsync(CancellationToken cancellationToken = default)
        {
            PointerClicks++;
            return Task.CompletedTask;
        }

        public Task ScrollByPixelsAsync(int deltaY, CancellationToken cancellationToken = default)
        {
            PointerScrolls.Add(deltaY);
            return Task.CompletedTask;
        }

        public Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult<BrowserPageSnapshot?>(null);
        public Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(BrowserSnapshotFreshnessPolicy policy, CancellationToken cancellationToken = default) => Task.FromResult<BrowserPageSnapshot?>(null);
    }

    private static string FindRepositoryDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Merlin.Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
