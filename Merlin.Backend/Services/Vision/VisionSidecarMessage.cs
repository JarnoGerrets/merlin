using System.Text.Json.Serialization;

namespace Merlin.Backend.Services.Vision;

public sealed class VisionSidecarMessage
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("cameraName")]
    public string? CameraName { get; init; }

    [JsonPropertyName("actualWidth")]
    public int ActualWidth { get; init; }

    [JsonPropertyName("actualHeight")]
    public int ActualHeight { get; init; }

    [JsonPropertyName("actualFps")]
    public double ActualFps { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("pointerId")]
    public string? PointerId { get; init; }

    [JsonPropertyName("x")]
    public double? X { get; init; }

    [JsonPropertyName("y")]
    public double? Y { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("pinchStartRatio")]
    public double PinchStartRatio { get; init; }

    [JsonPropertyName("pinchHoldRatio")]
    public double PinchHoldRatio { get; init; }

    [JsonPropertyName("pinchReleaseRatio")]
    public double PinchReleaseRatio { get; init; }

    [JsonPropertyName("openSamples")]
    public int OpenSamples { get; init; }

    [JsonPropertyName("pinchSamples")]
    public int PinchSamples { get; init; }

    [JsonPropertyName("releaseSamples")]
    public int ReleaseSamples { get; init; }

    [JsonPropertyName("calibrationPath")]
    public string? CalibrationPath { get; init; }

    [JsonPropertyName("controlRegionLeft")]
    public double ControlRegionLeft { get; init; }

    [JsonPropertyName("controlRegionTop")]
    public double ControlRegionTop { get; init; }

    [JsonPropertyName("controlRegionRight")]
    public double ControlRegionRight { get; init; }

    [JsonPropertyName("controlRegionBottom")]
    public double ControlRegionBottom { get; init; }

    [JsonPropertyName("topLeftSamples")]
    public int TopLeftSamples { get; init; }

    [JsonPropertyName("topRightSamples")]
    public int TopRightSamples { get; init; }

    [JsonPropertyName("bottomRightSamples")]
    public int BottomRightSamples { get; init; }

    [JsonPropertyName("bottomLeftSamples")]
    public int BottomLeftSamples { get; init; }
}
