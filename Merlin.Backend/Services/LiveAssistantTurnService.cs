using System.Collections.Concurrent;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class LiveAssistantTurnService : ILiveAssistantTurnService
{
    private readonly ConcurrentDictionary<string, LiveAssistantTurn> _turns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<LiveAssistantTurnService> _logger;

    public LiveAssistantTurnService(ILogger<LiveAssistantTurnService> logger)
    {
        _logger = logger;
    }

    public LiveAssistantTurn BeginTurn(
        string conversationId,
        string correlationId,
        string? assistantTurnId = null,
        CancellationToken requestAborted = default)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            throw new ArgumentException("Correlation id is required.", nameof(correlationId));
        }

        var normalizedCorrelationId = correlationId.Trim();
        var turn = new LiveAssistantTurn
        {
            ConversationId = string.IsNullOrWhiteSpace(conversationId) ? "default" : conversationId.Trim(),
            CorrelationId = normalizedCorrelationId,
            AssistantTurnId = string.IsNullOrWhiteSpace(assistantTurnId)
                ? normalizedCorrelationId
                : assistantTurnId.Trim(),
            StartedAt = DateTimeOffset.UtcNow,
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(requestAborted)
        };

        _turns.AddOrUpdate(
            normalizedCorrelationId,
            turn,
            (_, existing) =>
            {
                existing.MarkCancelled(LiveAssistantTurnCancelReason.SupersededByNewTurn);
                existing.Dispose();
                _logger.LogInformation(
                    "Live turn superseded. CorrelationId: {CorrelationId}.",
                    normalizedCorrelationId);
                return turn;
            });

        _logger.LogInformation(
            "Live turn started. ConversationId: {ConversationId}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}.",
            turn.ConversationId,
            turn.CorrelationId,
            turn.AssistantTurnId);
        return turn;
    }

    public bool TryGetActiveTurn(string correlationId, out LiveAssistantTurn turn)
    {
        if (_turns.TryGetValue(correlationId, out var candidate)
            && candidate.Status is LiveAssistantTurnStatus.Active)
        {
            turn = candidate;
            return true;
        }

        turn = null!;
        return false;
    }

    public CancellationToken GetTurnCancellationToken(
        string correlationId,
        CancellationToken fallback = default)
    {
        return TryGetActiveTurn(correlationId, out var turn)
            ? turn.CancellationToken
            : fallback;
    }

    public Task<bool> CancelTurnAsync(
        string correlationId,
        LiveAssistantTurnCancelReason reason,
        string? correctionText = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_turns.TryGetValue(correlationId, out var turn))
        {
            _logger.LogInformation(
                "Cancel requested for unknown live turn. CorrelationId: {CorrelationId}. Reason: {Reason}.",
                correlationId,
                reason);
            return Task.FromResult(false);
        }

        var cancelled = turn.MarkCancelled(reason, correctionText);
        if (cancelled)
        {
            _logger.LogInformation(
                "Live turn cancelled. CorrelationId: {CorrelationId}. Reason: {Reason}. CorrectionLength: {CorrectionLength}.",
                correlationId,
                reason,
                correctionText?.Length ?? 0);
        }
        else
        {
            _logger.LogInformation(
                "Live turn cancel request was idempotent. CorrelationId: {CorrelationId}. CurrentStatus: {Status}. Reason: {Reason}.",
                correlationId,
                turn.Status,
                reason);
        }

        return Task.FromResult(cancelled);
    }

    public bool IsActive(string correlationId) => TryGetActiveTurn(correlationId, out _);

    public bool IsCancelled(string correlationId)
    {
        return _turns.TryGetValue(correlationId, out var turn)
            && turn.Status is LiveAssistantTurnStatus.Cancelled;
    }

    public bool ShouldEmit(string correlationId)
    {
        return _turns.TryGetValue(correlationId, out var turn)
            && turn.Status is LiveAssistantTurnStatus.Active;
    }

    public void CompleteTurn(string correlationId)
    {
        if (!_turns.TryRemove(correlationId, out var turn))
        {
            _logger.LogDebug(
                "Complete requested for missing live turn. CorrelationId: {CorrelationId}.",
                correlationId);
            return;
        }

        turn.MarkCompleted();
        turn.Dispose();
        _logger.LogInformation(
            "Live turn completed. CorrelationId: {CorrelationId}. FinalStatus: {Status}.",
            correlationId,
            turn.Status);
    }
}
