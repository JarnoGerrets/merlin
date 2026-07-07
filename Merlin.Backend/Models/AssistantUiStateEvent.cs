namespace Merlin.Backend.Models;

public sealed record AssistantUiStateEvent(
    string Type,
    long Sequence,
    string BaseState,
    string OverlayState,
    string Reason,
    string? CorrelationId,
    string? TurnId,
    string SpeechItemType,
    bool AudiblePlaybackActive,
    string InterruptionState,
    DateTimeOffset TimestampUtc)
{
    public const string EventType = "assistant_ui_state";
    public const string InterruptionStateNone = "none";
    public const string InterruptionStateCapturing = "capturing";
    public const string InterruptionStateHandling = "handling";
    public const string InterruptionStateHeldForUserSpeech = "held_for_user_speech";
    public const string InterruptionStateAwaitingClarification = "awaiting_interruption_clarification";

    public static AssistantUiStateEvent Create(
        string baseState,
        string reason,
        string? correlationId = null,
        string? turnId = null,
        string overlayState = "none",
        string speechItemType = "none",
        bool audiblePlaybackActive = false,
        string interruptionState = InterruptionStateNone,
        DateTimeOffset? timestampUtc = null) =>
        new(
            EventType,
            0,
            baseState,
            overlayState,
            reason,
            correlationId,
            turnId,
            speechItemType,
            audiblePlaybackActive,
            interruptionState,
            timestampUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow);
}
