namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ConversationThreadState
{
    public string ThreadId { get; init; } = "";

    public string ActiveTurnId { get; init; } = "";

    public string OriginalUserQuestion { get; init; } = "";

    public string? CurrentAnswerPurpose { get; init; }

    public SpokenAnswerState? ActiveSpokenAnswer { get; init; }

    public IReadOnlyList<QueuedFollowUp> FollowUpQueue { get; init; } = Array.Empty<QueuedFollowUp>();

    public bool IsAssistantSpeaking { get; init; }

    public bool IsInterrupted { get; init; }

    public bool IsRecomposing { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
