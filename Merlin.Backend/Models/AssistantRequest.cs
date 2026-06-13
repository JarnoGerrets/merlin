namespace Merlin.Backend.Models;

public sealed class AssistantRequest
{
    public string Message { get; init; } = string.Empty;

    public string? CorrelationId { get; init; }

    public bool? SpeakResponse { get; init; }

    public string? InteractionSource { get; init; }

    public string? ClientMode { get; init; }
}
