namespace Merlin.Backend.Models;

public sealed class ToolExecutionContext
{
    public string? CorrelationId { get; init; }

    public string OriginalMessage { get; init; } = string.Empty;

    public string NormalizedCommand { get; init; } = string.Empty;

    public string? Intent { get; init; }

    public CapabilityRouteResult? Route { get; init; }

    public bool ShouldSpeak { get; init; }

    public Func<AssistantVisualEvent, CancellationToken, Task>? SpeechEventSender { get; init; }

    public Action? StreamingFinalAnswerStarted { get; init; }
}
