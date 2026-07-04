using System.Text.Json.Serialization;

namespace Merlin.Backend.Services.Vision;

public sealed class VisionGestureEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("pointerId")]
    public string PointerId { get; init; } = "primary";

    [JsonPropertyName("x")]
    public double? X { get; init; }

    [JsonPropertyName("y")]
    public double? Y { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("source")]
    public string Source { get; init; } = "webcam";
}

