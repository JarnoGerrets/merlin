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
    [InlineData("""{"type":"vision.pinch_calibration_started","message":"Open hand, then pinch, then release."}""", "vision.pinch_calibration_started")]
    [InlineData("""{"type":"vision.pinch_calibration_completed","status":"success","pinchStartRatio":0.21,"pinchHoldRatio":0.27,"pinchReleaseRatio":0.34,"openSamples":40,"pinchSamples":42,"releaseSamples":35}""", "vision.pinch_calibration_completed")]
    [InlineData("""{"type":"vision.motion_region_calibration_started","message":"Move to the corners."}""", "vision.motion_region_calibration_started")]
    [InlineData("""{"type":"vision.motion_region_calibration_completed","status":"success","controlRegionLeft":0.08,"controlRegionTop":0.06,"controlRegionRight":0.94,"controlRegionBottom":0.82,"topLeftSamples":40,"topRightSamples":41,"bottomRightSamples":42,"bottomLeftSamples":39}""", "vision.motion_region_calibration_completed")]
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
    public void TryParseMessage_ParsesPinchCalibrationResult()
    {
        var client = new VisionSidecarClient();

        var parsed = client.TryParseMessage(
            """{"type":"vision.pinch_calibration_completed","status":"success","pinchStartRatio":0.21,"pinchHoldRatio":0.27,"pinchReleaseRatio":0.34,"openSamples":40,"pinchSamples":42,"releaseSamples":35,"calibrationPath":"Logs/VisionCalibration/pinch-calibration-latest.json"}""",
            out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal("success", message.Status);
        Assert.Equal(0.21, message.PinchStartRatio);
        Assert.Equal(0.27, message.PinchHoldRatio);
        Assert.Equal(0.34, message.PinchReleaseRatio);
        Assert.Equal(40, message.OpenSamples);
        Assert.Equal(42, message.PinchSamples);
        Assert.Equal(35, message.ReleaseSamples);
        Assert.Equal("Logs/VisionCalibration/pinch-calibration-latest.json", message.CalibrationPath);
    }

    [Fact]
    public void TryParseMessage_ParsesMotionRegionCalibrationResult()
    {
        var client = new VisionSidecarClient();

        var parsed = client.TryParseMessage(
            """{"type":"vision.motion_region_calibration_completed","status":"success","controlRegionLeft":0.08,"controlRegionTop":0.06,"controlRegionRight":0.94,"controlRegionBottom":0.82,"topLeftSamples":40,"topRightSamples":41,"bottomRightSamples":42,"bottomLeftSamples":39,"calibrationPath":"Logs/VisionCalibration/motion-region-calibration-latest.json"}""",
            out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal("success", message.Status);
        Assert.Equal(0.08, message.ControlRegionLeft);
        Assert.Equal(0.06, message.ControlRegionTop);
        Assert.Equal(0.94, message.ControlRegionRight);
        Assert.Equal(0.82, message.ControlRegionBottom);
        Assert.Equal(40, message.TopLeftSamples);
        Assert.Equal(41, message.TopRightSamples);
        Assert.Equal(42, message.BottomRightSamples);
        Assert.Equal(39, message.BottomLeftSamples);
        Assert.Equal("Logs/VisionCalibration/motion-region-calibration-latest.json", message.CalibrationPath);
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

    [Fact]
    public void VisionWorker_UsesAdaptiveCaptureProfileSelectionWithManualOverride()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.Contains("backend_preference = str(self.config.get(\"backend\", \"Auto\") or \"Auto\").strip()", source);
        Assert.Contains("profile_preference = str(self.config.get(\"captureProfile\", \"Auto\") or \"Auto\").strip()", source);
        Assert.Contains("def select_camera_backend", source);
        Assert.Contains("def capture_profile_candidates", source);
        Assert.Contains("DSHOW_MJPG_CONSTRUCTOR", source);
        Assert.Contains("DSHOW_MJPG_SET_BEFORE_AFTER", source);
        Assert.Contains("VisionCameraProfileBenchmarkResult", source);
        Assert.Contains("VisionCameraBackendSelectionCompleted", source);
        Assert.Contains("selectedProfile", source);
        Assert.Contains("selectedFourcc", source);
        Assert.Contains("measuredFps", source);
        Assert.Contains("averageReadMs", source);
        Assert.Contains("startupMs", source);
        Assert.Contains("acceptable_measured_fps", source);
        Assert.Contains("slow_average_read_threshold_ms", source);
        Assert.Contains("blackFrameCount", source);
        Assert.Contains("\"MSMF\"", source);
        Assert.Contains("\"DSHOW\"", source);
        Assert.Contains("\"DEFAULT\"", source);
        Assert.Contains("cv2.VideoCapture(source, backend, [", source);
        Assert.Contains("cv2.VideoWriter_fourcc(*\"MJPG\")", source);
        Assert.Contains("max(pool, key=self.capture_profile_score)", source);
    }

    [Fact]
    public void VisionWorker_ResolvesPreferredCameraNameToIndexFallback()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.Contains("preferred_camera_name = str(self.config.get(\"cameraName\", \"\") or \"\").strip()", source);
        Assert.Contains("preferred_index = self.find_directshow_camera_index(preferred_camera_name)", source);
        Assert.Contains("VisionCameraPreferredNameResolved", source);
        Assert.Contains("return capture, self.camera_label(0, capture, metrics), metrics", source);
        Assert.Contains("VisionCameraFirstFrameRead", source);
    }

    [Fact]
    public void VisionWorker_AppliesPointerControlRegionAndGainBeforeSmoothing()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.Contains("VisionPointerMappingConfigured", source);
        Assert.Contains("def map_pointer_position", source);
        Assert.Contains("controlRegionLeft", source);
        Assert.Contains("controlRegionRight", source);
        Assert.Contains("pointerGainX", source);
        Assert.Contains("pointerGainY", source);
        Assert.Contains("return self.map_pointer_position(clamp01(index_tip.x), clamp01(index_tip.y))", source);
        Assert.Contains("x, y = self.smooth_position(pointer_id, x, y)", source);
    }

    [Fact]
    public void VisionWorker_SupportsPinchCalibrationFlow()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.Contains("vision.calibrate_pinch", source);
        Assert.Contains("def start_pinch_calibration", source);
        Assert.Contains("def update_pinch_calibration", source);
        Assert.Contains("def complete_pinch_calibration", source);
        Assert.Contains("VisionPinchCalibrationThresholdsApplied", source);
        Assert.Contains("cue_elapsed is not None and cue_elapsed > 0.0", source);
        Assert.Contains("return 0.0", source);
        Assert.Contains("def valid_calibration_confidence", source);
        Assert.Contains("VisionPinchCalibrationSampleSkipped", source);
        Assert.Contains("reason=missing_confidence", source);
        Assert.Contains("def fail_pinch_calibration", source);
        Assert.Contains("VisionPinchCalibrationFailedGracefully", source);
        Assert.Contains("VisionPinchCalibrationSaved", source);
        Assert.Contains("VisionPinchCalibrationLoaded", source);
        Assert.Contains("\"type\": \"vision.pinch_calibration_completed\"", source);
        Assert.Contains("pinchStartRatio", source);
        Assert.Contains("pinchHoldRatio", source);
        Assert.Contains("pinchReleaseRatio", source);
    }

    [Fact]
    public void VisionWorker_SupportsMotionRegionCalibrationFlow()
    {
        var workerPath = FindRepositoryFile("Merlin.Backend", "VisionScripts", "vision_worker.py");
        var source = File.ReadAllText(workerPath);

        Assert.Contains("vision.calibrate_motion_region", source);
        Assert.Contains("def start_motion_region_calibration", source);
        Assert.Contains("def update_motion_region_calibration", source);
        Assert.Contains("def complete_motion_region_calibration", source);
        Assert.Contains("VisionMotionRegionCalibrationRegionApplied", source);
        Assert.Contains("VisionMotionRegionCalibrationSaved", source);
        Assert.Contains("VisionMotionRegionCalibrationLoaded", source);
        Assert.Contains("\"type\": \"vision.motion_region_calibration_completed\"", source);
        Assert.Contains("motionRegionCalibrationPath", source);
        Assert.Contains("controlRegionLeft", source);
        Assert.Contains("controlRegionBottom", source);
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
