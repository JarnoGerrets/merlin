using System.Text.Json.Serialization;
using Merlin.Backend.Services.Context.ActiveSurface;

namespace Merlin.Backend.Models;

public sealed class AssistantRequest
{
    public string Message { get; init; } = string.Empty;

    public string? CorrelationId { get; init; }

    public string? CaptureId { get; init; }

    public bool? SpeakResponse { get; init; }

    public string? InteractionSource { get; init; }

    public string? ClientMode { get; init; }

    [JsonIgnore]
    public Func<AssistantVisualEvent, CancellationToken, Task>? SpeechEventSender { get; init; }

    [JsonIgnore]
    public DateTimeOffset? ReceivedAtUtc { get; init; }

    [JsonIgnore]
    public ActiveSurfaceSnapshot? ActiveSurface { get; init; }
}
