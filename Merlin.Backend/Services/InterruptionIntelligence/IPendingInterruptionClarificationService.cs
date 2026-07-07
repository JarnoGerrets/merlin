namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IPendingInterruptionClarificationService
{
    PendingInterruptionClarification CreatePending(
        PendingInterruptionClarificationCreateRequest request,
        DateTimeOffset? nowUtc = null);

    PendingInterruptionClarification? TryGetLatestPending(DateTimeOffset? nowUtc = null);

    PendingInterruptionClarification? TryGetForTurn(string activeTurnId, DateTimeOffset? nowUtc = null);

    bool HasActivePendingForTurn(string activeTurnId, DateTimeOffset? nowUtc = null);

    PendingInterruptionClarificationResponse? TryConsumeResponse(
        string responseText,
        string? captureId = null,
        string? correlationId = null,
        DateTimeOffset? nowUtc = null);

    bool CancelForTurn(string activeTurnId, string reason, DateTimeOffset? nowUtc = null);

    int ExpireDue(DateTimeOffset? nowUtc = null);
}
