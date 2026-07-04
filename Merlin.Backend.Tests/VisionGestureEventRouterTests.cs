using Merlin.Backend.Services;
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
}
