using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Motion;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MotionControlProfileRegistryTests
{
    [Fact]
    public void Resolve_WhenSurfaceIsDashboard_SelectsDashboardProfile()
    {
        var dashboard = new FakeMotionProfile(MotionControlProfileId.Dashboard, ActiveSurfaceKind.Dashboard, priority: 100);
        var browser = new FakeMotionProfile(MotionControlProfileId.BrowserWorkspace, ActiveSurfaceKind.BrowserWorkspace, priority: 90);
        var neutral = new FakeMotionProfile(MotionControlProfileId.Neutral, ActiveSurfaceKind.Unknown, priority: -100);
        var registry = CreateRegistry(dashboard, browser, neutral);

        var result = registry.Resolve(KnownSurfaces.Dashboard(DateTimeOffset.UtcNow));

        Assert.Same(dashboard, result.Profile);
        Assert.Equal("surface_dashboard", result.Reason);
    }

    [Fact]
    public void Resolve_WhenSurfaceIsBrowserWorkspace_SelectsBrowserProfile()
    {
        var dashboard = new FakeMotionProfile(MotionControlProfileId.Dashboard, ActiveSurfaceKind.Dashboard, priority: 100);
        var browser = new FakeMotionProfile(MotionControlProfileId.BrowserWorkspace, ActiveSurfaceKind.BrowserWorkspace, priority: 90);
        var neutral = new FakeMotionProfile(MotionControlProfileId.Neutral, ActiveSurfaceKind.Unknown, priority: -100);
        var registry = CreateRegistry(dashboard, browser, neutral);

        var result = registry.Resolve(KnownSurfaces.BrowserWorkspace(DateTimeOffset.UtcNow));

        Assert.Same(browser, result.Profile);
        Assert.Equal("surface_browser_workspace", result.Reason);
    }

    [Fact]
    public void Resolve_WhenOverrideProvided_SelectsOverrideProfile()
    {
        var dashboard = new FakeMotionProfile(MotionControlProfileId.Dashboard, ActiveSurfaceKind.Dashboard, priority: 100);
        var browser = new FakeMotionProfile(MotionControlProfileId.BrowserWorkspace, ActiveSurfaceKind.BrowserWorkspace, priority: 90);
        var neutral = new FakeMotionProfile(MotionControlProfileId.Neutral, ActiveSurfaceKind.Unknown, priority: -100);
        var registry = CreateRegistry(dashboard, browser, neutral);

        var result = registry.Resolve(
            KnownSurfaces.Dashboard(DateTimeOffset.UtcNow),
            new MotionControlProfileOverride(MotionControlProfileId.BrowserWorkspace, "test_override"));

        Assert.Same(browser, result.Profile);
        Assert.Equal("override:test_override", result.Reason);
    }

    [Fact]
    public void Resolve_WhenSurfaceIsUnknown_SelectsNeutralProfile()
    {
        var dashboard = new FakeMotionProfile(MotionControlProfileId.Dashboard, ActiveSurfaceKind.Dashboard, priority: 100);
        var browser = new FakeMotionProfile(MotionControlProfileId.BrowserWorkspace, ActiveSurfaceKind.BrowserWorkspace, priority: 90);
        var neutral = new FakeMotionProfile(MotionControlProfileId.Neutral, ActiveSurfaceKind.Unknown, priority: -100);
        var registry = CreateRegistry(dashboard, browser, neutral);

        var result = registry.Resolve(KnownSurfaces.Unknown(DateTimeOffset.UtcNow));

        Assert.Same(neutral, result.Profile);
        Assert.Equal("surface_unknown", result.Reason);
    }

    private static MotionControlProfileRegistry CreateRegistry(params IMotionControlProfile[] profiles) =>
        new(profiles, NullLogger<MotionControlProfileRegistry>.Instance);

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

        public bool CanHandle(ActiveSurfaceSnapshot surface) => surface.Kind == _surfaceKind;

        public Task ActivateAsync(MotionControlProfileActivationContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeactivateAsync(string reason, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task HandleGestureAsync(MotionControlGestureContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OnActiveSurfaceChangedAsync(ActiveSurfaceSnapshot surface, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
