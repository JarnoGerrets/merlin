namespace Merlin.Backend.Models;

public sealed class AssistantRequest
{
    public string Message { get; init; } = string.Empty;

    public string? CorrelationId { get; init; }
}
