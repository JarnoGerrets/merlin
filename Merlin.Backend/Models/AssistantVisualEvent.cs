namespace Merlin.Backend.Models;

public sealed class AssistantVisualEvent
{
    public string Event { get; init; } = string.Empty;

    public double? Value { get; init; }

    public string? CorrelationId { get; init; }

    public string? Detail { get; init; }
}
