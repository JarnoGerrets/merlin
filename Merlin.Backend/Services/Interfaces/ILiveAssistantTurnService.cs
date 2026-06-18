using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ILiveAssistantTurnService
{
    LiveAssistantTurn BeginTurn(
        string conversationId,
        string correlationId,
        string? assistantTurnId = null,
        CancellationToken requestAborted = default);

    bool TryGetActiveTurn(string correlationId, out LiveAssistantTurn turn);

    CancellationToken GetTurnCancellationToken(
        string correlationId,
        CancellationToken fallback = default);

    Task<bool> CancelTurnAsync(
        string correlationId,
        LiveAssistantTurnCancelReason reason,
        string? correctionText = null,
        CancellationToken cancellationToken = default);

    bool IsActive(string correlationId);

    bool IsCancelled(string correlationId);

    bool ShouldEmit(string correlationId);

    void CompleteTurn(string correlationId);
}
