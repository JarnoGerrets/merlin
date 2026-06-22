using System.Collections.Concurrent;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.Acknowledgement;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Feedback;

public sealed class ResponsiveFeedbackOrchestrator : IResponsiveFeedbackOrchestrator
{
    private readonly ConcurrentDictionary<string, FeedbackEmissionResult> _emittedImmediateFeedback = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _immediateFeedbackEmittedAtUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _mainResponseReadyAtUtc = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ProgressSuppression> _suppressedProgressTurns = new(StringComparer.OrdinalIgnoreCase);
    private readonly IFeedbackSelector _selector;
    private readonly ILogger<ResponsiveFeedbackOrchestrator> _logger;
    private readonly ResponsiveFeedbackOptions _options;
    private readonly IRequestProgressSpeechService _progressSpeechService;
    private readonly IAssistantSpeechPlaybackService _speechPlaybackService;

    public ResponsiveFeedbackOrchestrator(
        IFeedbackSelector selector,
        IAssistantSpeechPlaybackService speechPlaybackService,
        IRequestProgressSpeechService progressSpeechService,
        IOptions<ResponsiveFeedbackOptions> options,
        ILogger<ResponsiveFeedbackOrchestrator> logger)
    {
        _selector = selector;
        _speechPlaybackService = speechPlaybackService;
        _progressSpeechService = progressSpeechService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FeedbackEmissionResult> TryEmitImmediateFeedbackAsync(
        FeedbackContext context,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.EnableSpeechFeedback || !_options.UseCardSelectorForImmediateFeedback)
        {
            LogSuppressed(context, "disabled");
            return Result(context, "disabled");
        }

        if (!context.AllowSpeech)
        {
            LogSuppressed(context, "speech_not_allowed");
            return Result(context, "speech_not_allowed");
        }

        try
        {
            CleanupState();

            if (_options.ImmediateFeedbackDelayMs > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_options.ImmediateFeedbackDelayMs), cancellationToken);
            }

            if (IsMainResponseReady(context.CorrelationId))
            {
                LogSuppressed(context, "main_response_ready");
                return Result(context, "main_response_ready");
            }

            var selection = _selector.Select(context);
            if (selection is null)
            {
                LogSuppressed(context, "no_selection");
                return Result(context, "no_selection");
            }

            if (IsMainResponseReady(context.CorrelationId))
            {
                LogSuppressed(context, "main_response_ready");
                return Result(context, "main_response_ready");
            }

            await QueueSpeechAsync(context, selection, sendEventAsync, cancellationToken);

            var emitted = Result(
                context,
                "emitted",
                emitted: true,
                cardId: selection.Card.Id,
                text: selection.Card.Text);
            _emittedImmediateFeedback[context.CorrelationId] = emitted;
            _immediateFeedbackEmittedAtUtc[context.CorrelationId] = DateTimeOffset.UtcNow;

            if (_options.EnableDiagnosticsLogging)
            {
                _logger.LogInformation(
                    "Responsive feedback selected. CorrelationId: {CorrelationId}. TurnId: {TurnId}. Phase: {Phase}. Domain: {Domain}. CardId: {CardId}. Score: {Score}. Reason: {Reason}.",
                    context.CorrelationId,
                    context.TurnId,
                    context.Phase,
                    context.Domain,
                    selection.Card.Id,
                    selection.Score,
                    selection.Reason);
            }

            return emitted;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Responsive feedback cancelled before playback queueing. CorrelationId: {CorrelationId}.",
                context.CorrelationId);
            return Result(context, "cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Responsive feedback failed. CorrelationId: {CorrelationId}.",
                context.CorrelationId);
            return Result(context, "queue_failed");
        }
    }

    public async Task<FeedbackEmissionResult> TryEmitInterruptionBridgeAsync(
        FeedbackContext context,
        CancellationToken cancellationToken)
    {
        if (context.Domain != FeedbackDomain.Interruption && !context.IsInterruptionFeedback)
        {
            LogSuppressed(context, "not_interruption_context");
            return Result(context, "not_interruption_context");
        }

        if (!_options.Enabled || !_options.EnableSpeechFeedback || !_options.UseCardSelectorForInterruptionBridgeFeedback)
        {
            LogSuppressed(context, "disabled");
            return Result(context, "disabled");
        }

        if (!context.AllowSpeech)
        {
            LogSuppressed(context, "speech_not_allowed");
            return Result(context, "speech_not_allowed");
        }

        var bridgeContext = ForceInterruptionContext(context);

        try
        {
            CleanupState();

            var selection = _selector.Select(bridgeContext);
            if (selection is null)
            {
                LogSuppressed(bridgeContext, "no_selection");
                return Result(bridgeContext, "no_selection");
            }

            await QueueSpeechAsync(
                bridgeContext,
                selection,
                static (_, _) => Task.CompletedTask,
                cancellationToken);

            if (_options.EnableDiagnosticsLogging)
            {
                _logger.LogInformation(
                    "Responsive interruption bridge selected. CorrelationId: {CorrelationId}. TurnId: {TurnId}. Phase: {Phase}. CardId: {CardId}. Score: {Score}. Reason: {Reason}.",
                    bridgeContext.CorrelationId,
                    bridgeContext.TurnId,
                    bridgeContext.Phase,
                    selection.Card.Id,
                    selection.Score,
                    selection.Reason);
            }

            return Result(
                bridgeContext,
                "emitted",
                emitted: true,
                cardId: selection.Card.Id,
                text: selection.Card.Text);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "Responsive interruption bridge cancelled before playback queueing. CorrelationId: {CorrelationId}.",
                bridgeContext.CorrelationId);
            return Result(bridgeContext, "cancelled");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Responsive interruption bridge failed. CorrelationId: {CorrelationId}.",
                bridgeContext.CorrelationId);
            return Result(bridgeContext, "queue_failed");
        }
    }

    public IRequestProgressSpeechHandle? StartProgressFeedback(
        FeedbackContext context,
        RequestProgressSpeechRequest request,
        CancellationToken cancellationToken)
    {
        CleanupState();

        if (IsNormalProgressSuppressed(context, out var suppressionReason))
        {
            LogSuppressed(context, $"normal_progress_suppressed:{suppressionReason}");
            return null;
        }

        if (!_options.Enabled || !_options.EnableSpeechFeedback)
        {
            return _progressSpeechService.Start(request, cancellationToken);
        }

        if (_options.SuppressNormalProgressDuringInterruptionHandling && context.SuppressNormalProgressFeedback)
        {
            LogSuppressed(context, "normal_progress_suppressed");
            return null;
        }

        return _progressSpeechService.Start(request, cancellationToken);
    }

    public void MarkMainResponseReady(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return;
        }

        CleanupState();
        _mainResponseReadyAtUtc[correlationId] = DateTimeOffset.UtcNow;
    }

    public bool WasImmediateFeedbackEmitted(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return false;
        }

        CleanupState();
        return _emittedImmediateFeedback.TryGetValue(correlationId, out var result)
            && result.Emitted;
    }

    public void SuppressNormalProgressForTurn(string turnId, string reason)
    {
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        CleanupState();
        _suppressedProgressTurns[turnId] = new ProgressSuppression(
            string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason,
            DateTimeOffset.UtcNow);

        if (!_options.EnableDiagnosticsLogging)
        {
            return;
        }

        _logger.LogInformation(
            "Responsive feedback progress suppression requested. TurnId: {TurnId}. Reason: {Reason}.",
            turnId,
            reason);
    }

    private async Task QueueSpeechAsync(
        FeedbackContext context,
        FeedbackSelection selection,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        CancellationToken cancellationToken)
    {
        var cacheKey = _options.UseStableCardIdAsSpeechCacheKey
            ? $"feedback:{selection.Card.Id}"
            : selection.Card.Id;

        await _speechPlaybackService.EnqueueAsync(
            selection.Card.Text,
            context.CorrelationId,
            sendEventAsync,
            cacheKey,
            _options.MarkFeedbackAsReplayableSpeech ? selection.Card.IsReplayableSpeech : null,
            cancellationToken,
            SpeechPlaybackItemType.Acknowledgement,
            cancelOnlyBeforePlayback: selection.Card.InterruptibleBeforePlayback);
    }

    private static FeedbackContext ForceInterruptionContext(FeedbackContext context)
    {
        return new FeedbackContext
        {
            CorrelationId = context.CorrelationId,
            TurnId = context.TurnId,
            RawUserText = context.RawUserText,
            NormalizedUserText = context.NormalizedUserText,
            Phase = context.Phase is FeedbackPhase.Unknown ? FeedbackPhase.HandlingInterruption : context.Phase,
            Domain = FeedbackDomain.Interruption,
            DurationEstimate = context.DurationEstimate,
            Confidence = context.Confidence,
            Urgency = context.Urgency,
            Intent = context.Intent,
            ToolName = context.ToolName,
            TargetName = context.TargetName,
            IsVoiceInteraction = context.IsVoiceInteraction,
            IsOrbClient = context.IsOrbClient,
            IsExternalAction = context.IsExternalAction,
            NeedsConfirmation = context.NeedsConfirmation,
            IsUserWaiting = context.IsUserWaiting,
            AllowSpeech = context.AllowSpeech,
            AllowVisualFeedback = context.AllowVisualFeedback,
            IsInterruptionFeedback = true,
            InterruptionType = context.InterruptionType,
            InterruptionStrategy = context.InterruptionStrategy,
            IsRecompositionFeedback = context.IsRecompositionFeedback,
            SuppressNormalProgressFeedback = context.SuppressNormalProgressFeedback,
            Tags = context.Tags,
            CreatedAtUtc = context.CreatedAtUtc
        };
    }

    private bool IsNormalProgressSuppressed(FeedbackContext context, out string reason)
    {
        reason = string.Empty;
        if (!_options.SuppressNormalProgressDuringInterruptionHandling)
        {
            return false;
        }

        if (context.SuppressNormalProgressFeedback)
        {
            reason = "context";
            return true;
        }

        var turnId = !string.IsNullOrWhiteSpace(context.TurnId)
            ? context.TurnId
            : context.CorrelationId;
        if (string.IsNullOrWhiteSpace(turnId))
        {
            return false;
        }

        if (!_suppressedProgressTurns.TryGetValue(turnId, out var suppression))
        {
            return false;
        }

        reason = suppression.Reason;
        return true;
    }

    private void LogSuppressed(FeedbackContext context, string reason)
    {
        if (!_options.EnableDiagnosticsLogging)
        {
            return;
        }

        _logger.LogInformation(
            "Responsive feedback suppressed. CorrelationId: {CorrelationId}. TurnId: {TurnId}. Phase: {Phase}. Domain: {Domain}. Reason: {Reason}.",
            context.CorrelationId,
            context.TurnId,
            context.Phase,
            context.Domain,
            reason);
    }

    private bool IsMainResponseReady(string correlationId)
    {
        return _options.SuppressIfMainResponseReady
            && !string.IsNullOrWhiteSpace(correlationId)
            && _mainResponseReadyAtUtc.ContainsKey(correlationId);
    }

    private void CleanupState()
    {
        var retention = TimeSpan.FromSeconds(Math.Max(1, _options.MainResponseReadyStateRetentionSeconds));
        var cutoff = DateTimeOffset.UtcNow - retention;

        foreach (var (correlationId, readyAtUtc) in _mainResponseReadyAtUtc)
        {
            if (readyAtUtc < cutoff)
            {
                _mainResponseReadyAtUtc.TryRemove(correlationId, out _);
            }
        }

        foreach (var (correlationId, emittedAtUtc) in _immediateFeedbackEmittedAtUtc)
        {
            if (emittedAtUtc < cutoff)
            {
                _immediateFeedbackEmittedAtUtc.TryRemove(correlationId, out _);
                _emittedImmediateFeedback.TryRemove(correlationId, out _);
            }
        }

        foreach (var (turnId, suppression) in _suppressedProgressTurns)
        {
            if (suppression.CreatedAtUtc < cutoff)
            {
                _suppressedProgressTurns.TryRemove(turnId, out _);
            }
        }
    }

    private static FeedbackEmissionResult Result(
        FeedbackContext context,
        string reason,
        bool emitted = false,
        string? cardId = null,
        string? text = null)
    {
        return new FeedbackEmissionResult
        {
            Emitted = emitted,
            CardId = cardId,
            Text = text,
            Domain = context.Domain,
            Reason = reason
        };
    }

    private sealed record ProgressSuppression(string Reason, DateTimeOffset CreatedAtUtc);
}
