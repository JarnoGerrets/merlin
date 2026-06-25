namespace Merlin.Backend.Services;

public sealed class ActiveSpeechPlaybackSnapshot
{
    public string CorrelationId { get; init; } = string.Empty;

    public string AssistantTurnId { get; init; } = string.Empty;

    public string SpeechType { get; init; } = string.Empty;

    public string ItemType { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public bool IsHeld { get; init; }

    public bool IsAudiblePlaybackActive { get; init; }

    public string? HoldId { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }
}
