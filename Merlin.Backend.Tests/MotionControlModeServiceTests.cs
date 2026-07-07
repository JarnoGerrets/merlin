using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Motion;
using Merlin.Backend.Services.Vision;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MotionControlModeServiceTests
{
    [Fact]
    public async Task EnableAsync_WhenDashboardSurface_ActivatesDashboardAndStartsTracking()
    {
        var harness = CreateHarness();

        var snapshot = await harness.Service.EnableAsync("test_enable");

        Assert.True(snapshot.IsEnabled);
        Assert.Equal(MotionControlProfileId.Dashboard, snapshot.ActiveProfileId);
        Assert.Equal(1, harness.Dashboard.ActivateCalls);
        Assert.Equal(0, harness.Browser.ActivateCalls);
        Assert.Equal(1, harness.Vision.StartTrackingCalls);
    }

    [Fact]
    public async Task ActiveSurfaceChanged_WhenEnabled_SwitchesProfiles()
    {
        var harness = CreateHarness();
        await harness.Service.EnableAsync("test_enable");

        await harness.ActiveSurface.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = ActiveSurfaceKind.BrowserWorkspace,
            SurfaceId = "browser.workspace.main",
            DisplayName = "Browser Workspace",
            Source = ActiveSurfaceSource.BrowserWorkspace,
            Confidence = 1,
            Reason = "browser_opened"
        });

        Assert.Equal(MotionControlProfileId.BrowserWorkspace, harness.Service.Current.ActiveProfileId);
        Assert.Equal(1, harness.Dashboard.DeactivateCalls);
        Assert.Equal(1, harness.Browser.ActivateCalls);
        Assert.Equal(1, harness.Vision.StartTrackingCalls);
    }

    [Fact]
    public async Task ActiveSurfaceChanged_ToUnknown_SwitchesToNeutralAndStopsTracking()
    {
        var harness = CreateHarness();
        await harness.Service.EnableAsync("test_enable");

        await harness.ActiveSurface.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = ActiveSurfaceKind.Unknown,
            SurfaceId = "unknown",
            DisplayName = "Unknown",
            Source = ActiveSurfaceSource.Unknown,
            Confidence = 0,
            Reason = "lost_surface"
        });

        Assert.Equal(MotionControlProfileId.Neutral, harness.Service.Current.ActiveProfileId);
        Assert.Equal(1, harness.Neutral.ActivateCalls);
        Assert.Equal(1, harness.Vision.StopTrackingCalls);
    }

    [Fact]
    public async Task HandleGestureAsync_WhenDashboardProfileActive_ForwardsDashboardGesture()
    {
        var harness = CreateHarness();
        await harness.Service.EnableAsync("test_enable");
        VisionGestureEvent? forwarded = null;
        harness.Service.DashboardGestureForwarded += (gestureEvent, _) =>
        {
            forwarded = gestureEvent;
            return Task.CompletedTask;
        };

        await harness.Service.HandleGestureAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = 0.25,
            Y = 0.75,
            Source = "test"
        });

        Assert.NotNull(forwarded);
        Assert.Equal("gesture.pointer.move", forwarded.Type);
        Assert.Equal(1, harness.Dashboard.HandleGestureCalls);
    }

    [Fact]
    public async Task HandleGestureAsync_WhenBrowserProfileActive_DoesNotForwardDashboardGesture()
    {
        var harness = CreateHarness();
        await harness.ActiveSurface.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = ActiveSurfaceKind.BrowserWorkspace,
            SurfaceId = "browser.workspace.main",
            DisplayName = "Browser Workspace",
            Source = ActiveSurfaceSource.BrowserWorkspace,
            Confidence = 1,
            Reason = "browser_opened"
        });
        await harness.Service.EnableAsync("test_enable");
        var forwarded = false;
        harness.Service.DashboardGestureForwarded += (_, _) =>
        {
            forwarded = true;
            return Task.CompletedTask;
        };

        await harness.Service.HandleGestureAsync(new VisionGestureEvent
        {
            Type = "gesture.pointer.move",
            PointerId = "primary",
            X = 0.25,
            Y = 0.75,
            Source = "test"
        });

        Assert.False(forwarded);
        Assert.Equal(1, harness.Browser.HandleGestureCalls);
    }

    [Fact]
    public async Task DisableAsync_DeactivatesActiveProfileAndStopsTracking()
    {
        var harness = CreateHarness();
        await harness.Service.EnableAsync("test_enable");

        var snapshot = await harness.Service.DisableAsync("test_disable");

        Assert.False(snapshot.IsEnabled);
        Assert.Equal(1, harness.Dashboard.DeactivateCalls);
        Assert.Equal(1, harness.Vision.StopTrackingCalls);
    }

    private static TestHarness CreateHarness()
    {
        var activeSurface = new ActiveSurfaceService(NullLogger<ActiveSurfaceService>.Instance);
        var dashboard = new FakeMotionProfile(MotionControlProfileId.Dashboard, ActiveSurfaceKind.Dashboard, priority: 100);
        var browser = new FakeMotionProfile(MotionControlProfileId.BrowserWorkspace, ActiveSurfaceKind.BrowserWorkspace, priority: 90);
        var neutral = new FakeMotionProfile(MotionControlProfileId.Neutral, ActiveSurfaceKind.Unknown, priority: -100);
        var registry = new MotionControlProfileRegistry(
            new IMotionControlProfile[] { dashboard, browser, neutral },
            NullLogger<MotionControlProfileRegistry>.Instance);
        var vision = new FakeVisionSidecarHost();
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IVisionSidecarHost>(vision)
            .BuildServiceProvider();
        var service = new MotionControlModeService(
            activeSurface,
            registry,
            serviceProvider,
            NullLogger<MotionControlModeService>.Instance);

        return new TestHarness(activeSurface, service, dashboard, browser, neutral, vision);
    }

    private sealed record TestHarness(
        ActiveSurfaceService ActiveSurface,
        MotionControlModeService Service,
        FakeMotionProfile Dashboard,
        FakeMotionProfile Browser,
        FakeMotionProfile Neutral,
        FakeVisionSidecarHost Vision);

    private sealed class FakeMotionProfile : IMotionControlProfile
    {
        private readonly ActiveSurfaceKind _surfaceKind;

        public FakeMotionProfile(string profileId, ActiveSurfaceKind surfaceKind, int priority)
        {
            _surfaceKind = surfaceKind;
            Descriptor = new MotionControlProfileDescriptor(
                profileId,
                profileId,
                surfaceKind,
                priority,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        public MotionControlProfileDescriptor Descriptor { get; }

        public int ActivateCalls { get; private set; }

        public int DeactivateCalls { get; private set; }

        public int HandleGestureCalls { get; private set; }

        public bool CanHandle(ActiveSurfaceSnapshot surface) => surface.Kind == _surfaceKind;

        public Task ActivateAsync(MotionControlProfileActivationContext context, CancellationToken cancellationToken = default)
        {
            ActivateCalls++;
            return Task.CompletedTask;
        }

        public Task DeactivateAsync(string reason, CancellationToken cancellationToken = default)
        {
            DeactivateCalls++;
            return Task.CompletedTask;
        }

        public Task HandleGestureAsync(MotionControlGestureContext context, CancellationToken cancellationToken = default)
        {
            HandleGestureCalls++;
            return Task.CompletedTask;
        }

        public Task OnActiveSurfaceChangedAsync(ActiveSurfaceSnapshot surface, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeVisionSidecarHost : IVisionSidecarHost
    {
        public int StartTrackingCalls { get; private set; }

        public int StopTrackingCalls { get; private set; }

        public VisionHealthState State { get; private set; } = VisionHealthState.Ready;

        public Task WarmAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StartTrackingAsync(CancellationToken cancellationToken = default)
        {
            StartTrackingCalls++;
            State = VisionHealthState.Tracking;
            return Task.CompletedTask;
        }

        public Task StopTrackingAsync(CancellationToken cancellationToken = default)
        {
            StopTrackingCalls++;
            State = VisionHealthState.Ready;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<VisionPinchCalibrationResult> CalibratePinchAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VisionPinchCalibrationResult { Success = true });

        public Task<VisionMotionRegionCalibrationResult> CalibrateMotionRegionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new VisionMotionRegionCalibrationResult { Success = true });
    }
}
