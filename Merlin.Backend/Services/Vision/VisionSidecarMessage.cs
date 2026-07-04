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
}
