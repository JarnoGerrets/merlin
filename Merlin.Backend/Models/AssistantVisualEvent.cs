namespace Merlin.Backend.Models;

using System.Text.Json.Serialization;

public sealed class AssistantVisualEvent
{
    public string Event { get; init; } = string.Empty;

    public double? Value { get; init; }

    public string? CorrelationId { get; init; }

    public string? Detail { get; init; }

    [JsonIgnore]
    public AssistantUiStateEvent? AssistantUiState { get; init; }

    [JsonIgnore]
    public string? AssistantUiStateSource { get; init; }

    public static AssistantVisualEvent FromUiState(
        AssistantUiStateEvent uiState,
        string source) =>
        new()
        {
            Event = AssistantUiStateEvent.EventType,
            CorrelationId = uiState.CorrelationId,
            Detail = uiState.Reason,
            AssistantUiState = uiState,
            AssistantUiStateSource = source
        };
}
