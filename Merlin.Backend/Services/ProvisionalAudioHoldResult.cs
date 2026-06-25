namespace Merlin.Backend.Services;

public sealed record ProvisionalAudioHoldResult(
    bool Success,
    string? HoldId,
    string? TurnId,
    string? Reason,
    bool WasAlreadyHeld = false,
    bool WasFlushed = false,
    bool WasResumed = false,
    string? FailureReason = null)
{
    public static ProvisionalAudioHoldResult Failed(
        string? turnId,
        string? reason,
        string failureReason,
        string? holdId = null) =>
        new(
            Success: false,
            HoldId: holdId,
            TurnId: turnId,
            Reason: reason,
            FailureReason: failureReason);
}
