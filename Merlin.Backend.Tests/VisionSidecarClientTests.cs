using Merlin.Backend.Services.Vision;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class VisionSidecarClientTests
{
    [Theory]
    [InlineData("""{"type":"vision.ready","version":1}""", "vision.ready")]
    [InlineData("""{"type":"vision.tracking_started","cameraName":"camera:0","actualWidth":1280,"actualHeight":720,"actualFps":30}""", "vision.tracking_started")]
    [InlineData("""{"type":"vision.tracking_stopped"}""", "vision.tracking_stopped")]
    [InlineData("""{"type":"gesture.pointer.move","pointerId":"primary","x":0.54,"y":0.37,"confidence":0.91,"source":"webcam"}""", "gesture.pointer.move")]
    [InlineData("""{"type":"gesture.pointer.move","pointerId":"secondary","x":0.64,"y":0.37,"confidence":0.90,"source":"webcam"}""", "gesture.pointer.move")]
    [InlineData("""{"type":"gesture.pinch.start","pointerId":"primary","x":0.54,"y":0.37,"confidence":0.88,"source":"webcam"}""", "gesture.pinch.start")]
    [InlineData("""{"type":"gesture.pinch.end","pointerId":"secondary","source":"webcam"}""", "gesture.pinch.end")]
    [InlineData("""{"type":"vision.error","error":"CAMERA_OPEN_FAILED","code":"CAMERA_OPEN_FAILED","message":"0"}""", "vision.error")]
    public void TryParseMessage_ParsesProtocolMessages(string json, string expectedType)
    {
        var client = new VisionSidecarClient();

        var parsed = client.TryParseMessage(json, out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal(expectedType, message.Type);
    }

    [Fact]
    public void TryParseMessage_ParsesVisionErrorCode()
    {
        var client = new VisionSidecarClient();

        var parsed = client.TryParseMessage(
            """{"type":"vision.error","error":"MODEL_NOT_FOUND","code":"MODEL_NOT_FOUND","message":"missing"}""",
            out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal("MODEL_NOT_FOUND", message.Error);
        Assert.Equal("MODEL_NOT_FOUND", message.Code);
        Assert.Equal("missing", message.Message);
    }

    [Theory]
    [InlineData("""{"type":"gesture.pointer.move","pointerId":"primary","x":0.32,"y":0.44,"confidence":0.91,"source":"webcam"}""", "primary")]
    [InlineData("""{"type":"gesture.pinch.start","pointerId":"secondary","x":0.71,"y":0.43,"confidence":0.88,"source":"webcam"}""", "secondary")]
    [InlineData("""{"type":"gesture.pinch.end","pointerId":"secondary","source":"webcam"}""", "secondary")]
    public void TryParseMessage_PreservesPointerId(string json, string expectedPointerId)
    {
        var client = new VisionSidecarClient();

        var parsed = client.TryParseMessage(json, out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal(expectedPointerId, message.PointerId);
    }

    [Fact]
    public void TryParseMessage_IgnoresNonJsonOutput()
    {
        var client = new VisionSidecarClient();

        var parsed = client.TryParseMessage("camera log line", out var message);

        Assert.False(parsed);
        Assert.Null(message);
    }

    [Fact]
    public void VisionWorker_DoesNotUseLegacyMediaPipeSolutionsApi()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.DoesNotContain("mp.solutions", source);
        Assert.DoesNotContain("solutions.hands", source);
        Assert.Contains("HandLandmarker", source);
        Assert.Contains("detect_for_video", source);
    }

    [Fact]
    public void VisionWorker_UsesMonotonicTimestampGuard()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.Contains("def next_timestamp_ms", source);
        Assert.Contains("self.last_timestamp_ms + 1", source);
        Assert.Contains("detect_for_video(image, timestamp_ms)", source);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(segments));
    }
}
