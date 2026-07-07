using System.Collections.Concurrent;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class PendingInterruptionClarificationService : IPendingInterruptionClarificationService
{
    private readonly ConcurrentDictionary<string, PendingInterruptionClarification> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<PendingInterruptionClarificationService> _logger;
    private readonly AssistantUiStateBroadcaster? _assistantUiStateBroadcaster;

    public PendingInterruptionClarificationService(
        IOptions<InterruptionHandlingOptions> options,
        ILogger<PendingInterruptionClarificationService> logger,
        AssistantUiStateBroadcaster? assistantUiStateBroadcaster = null)
    {
        _options = options.Value;
        _logger = logger;
        _assistantUiStateBroadcaster = assistantUiStateBroadcaster;
    }

    public PendingInterruptionClarification CreatePending(
        PendingInterruptionClarificationCreateRequest request,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedTurnId = NormalizeRequired(request.ActiveTurnId, nameof(request.ActiveTurnId));
        var normalizedCorrelationId = NormalizeRequired(request.CorrelationId, nameof(request.CorrelationId));
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        ExpireDue(now);

        CancelForTurn(normalizedTurnId, "replaced_by_new_pending_interruption_clarification", now);

        var timeoutMs = Math.Max(1, _options.PendingInterruptionClarificationTimeoutMs);
        var pending = new PendingInterruptionClarification
        {
            ClarificationId = Guid.NewGuid().ToString("N"),
            ActiveTurnId = normalizedTurnId,
            CorrelationId = normalizedCorrelationId,
            CaptureId = NormalizeOptional(request.CaptureId),
            OriginalTranscript = request.OriginalTranscript.Trim(),
            NormalizedTranscript = NormalizeOptional(request.NormalizedTranscript) ?? string.Empty,
            RouteKind = NormalizeOptional(request.RouteKind),
            RouteAction = NormalizeOptional(request.RouteAction),
            Layer1Decision = NormalizeOptional(request.Layer1Decision),
            ProvisionalAudioHoldId = NormalizeOptional(request.ProvisionalAudioHoldId),
            WasHeldByProvisionalAudioHold = request.WasHeldByProvisionalAudioHold,
            OriginalUserQuestion = NormalizeOptional(request.OriginalUserQuestion),
            SafeSpokenPrefix = NormalizeOptional(request.SafeSpokenPrefix),
            LastCompletedSentence = NormalizeOptional(request.LastCompletedSentence),
            DiscardedPartialSentence = NormalizeOptional(request.DiscardedPartialSentence),
            CurrentTopicLabel = NormalizeOptional(request.CurrentTopicLabel),
            OriginalPlanOrIntent = NormalizeOptional(request.OriginalPlanOrIntent),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMilliseconds(timeoutMs)
        };

        _pending[pending.ClarificationId] = pending;
        _logger.LogInformation(
            "pending_interruption_clarification_created ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. CaptureId: {CaptureId}. ExpiresAtUtc: {ExpiresAtUtc}. TranscriptLength: {TranscriptLength}. RouteKind: {RouteKind}. RouteAction: {RouteAction}. Layer1Decision: {Layer1Decision}.",
            pending.ClarificationId,
            pending.ActiveTurnId,
            pending.CorrelationId,
            pending.CaptureId,
            pending.ExpiresAtUtc,
            pending.OriginalTranscript.Length,
            pending.RouteKind,
            pending.RouteAction,
            pending.Layer1Decision);
        ScheduleExpiry(pending);
        return pending;
    }

    public PendingInterruptionClarification? TryGetLatestPending(DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        ExpireDue(now);
        return _pending.Values
            .OrderByDescending(pending => pending.CreatedAtUtc)
            .FirstOrDefault();
    }

    public PendingInterruptionClarification? TryGetForTurn(string activeTurnId, DateTimeOffset? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(activeTurnId))
        {
            return null;
        }

        var normalizedTurnId = activeTurnId.Trim();
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        ExpireDue(now);
        return _pending.Values
            .Where(pending => string.Equals(pending.ActiveTurnId, normalizedTurnId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(pending => pending.CreatedAtUtc)
            .FirstOrDefault();
    }

    public bool HasActivePendingForTurn(string activeTurnId, DateTimeOffset? nowUtc = null) =>
        TryGetForTurn(activeTurnId, nowUtc) is not null;

    public PendingInterruptionClarificationResponse? TryConsumeResponse(
        string responseText,
        string? captureId = null,
        string? correlationId = null,
        DateTimeOffset? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var pending = TryGetLatestPending(now);
        if (pending is null)
        {
            return null;
        }

        if (!_pending.TryRemove(pending.ClarificationId, out var removed))
        {
            return null;
        }

        var response = new PendingInterruptionClarificationResponse
        {
            Pending = removed,
            ResponseText = responseText.Trim(),
            NormalizedResponseText = NormalizeForMatching(responseText),
            CaptureId = NormalizeOptional(captureId),
            CorrelationId = NormalizeOptional(correlationId),
            ConsumedAtUtc = now
        };

        _logger.LogInformation(
            "pending_interruption_clarification_consumed ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. ResponseCaptureId: {ResponseCaptureId}. ResponseCorrelationId: {ResponseCorrelationId}. ResponseLength: {ResponseLength}. AgeMs: {AgeMs}.",
            removed.ClarificationId,
            removed.ActiveTurnId,
            removed.CorrelationId,
            response.CaptureId,
            response.CorrelationId,
            response.ResponseText.Length,
            Math.Max(0, (now - removed.CreatedAtUtc).TotalMilliseconds));
        return response;
    }

    public bool CancelForTurn(string activeTurnId, string reason, DateTimeOffset? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(activeTurnId))
        {
            return false;
        }

        var normalizedTurnId = activeTurnId.Trim();
        var removedAny = false;
        foreach (var pending in _pending.Values)
        {
            if (!string.Equals(pending.ActiveTurnId, normalizedTurnId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_pending.TryRemove(pending.ClarificationId, out var removed))
            {
                removedAny = true;
                _logger.LogInformation(
                    "pending_interruption_clarification_cancelled ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}.",
                    removed.ClarificationId,
                    removed.ActiveTurnId,
                    removed.CorrelationId,
                    reason);
                EmitAwaitingClarificationCleared(
                    removed,
                    "pending_interruption_clarification_cancelled",
                    nowUtc);
            }
        }

        return removedAny;
    }

    public int ExpireDue(DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var expired = 0;
        foreach (var pending in _pending.Values)
        {
            if (pending.ExpiresAtUtc > now)
            {
                continue;
            }

            if (_pending.TryRemove(pending.ClarificationId, out var removed))
            {
                expired++;
                _logger.LogInformation(
                    "pending_interruption_clarification_expired ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. ExpiresAtUtc: {ExpiresAtUtc}.",
                    removed.ClarificationId,
                    removed.ActiveTurnId,
                    removed.CorrelationId,
                    removed.ExpiresAtUtc);
                EmitAwaitingClarificationCleared(
                    removed,
                    "pending_interruption_clarification_timeout",
                    now);
            }
        }

        return expired;
    }

    private void ScheduleExpiry(PendingInterruptionClarification pending)
    {
        var delay = pending.ExpiresAtUtc - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.FromMilliseconds(1))
        {
            delay = TimeSpan.FromMilliseconds(1);
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(delay);
                    ExpireDue(DateTimeOffset.UtcNow);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "pending_interruption_clarification_expiry_task_failed ClarificationId: {ClarificationId}. TurnId: {TurnId}.",
                        pending.ClarificationId,
                        pending.ActiveTurnId);
                }
            });
    }

    private void EmitAwaitingClarificationCleared(
        PendingInterruptionClarification pending,
        string reason,
        DateTimeOffset? timestampUtc)
    {
        if (_assistantUiStateBroadcaster is null)
        {
            return;
        }

        _ = _assistantUiStateBroadcaster.EmitImmediateAsync(
            AssistantUiStateEvent.Create(
                "idle",
                reason,
                pending.CorrelationId,
                pending.ActiveTurnId,
                interruptionState: AssistantUiStateEvent.InterruptionStateNone,
                timestampUtc: timestampUtc),
            nameof(PendingInterruptionClarificationService));
    }

    private static string NormalizeRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeForMatching(string value) =>
        string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
