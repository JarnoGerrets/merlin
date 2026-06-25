using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class LiveSpokenAnswerTrackingService : ILiveSpokenAnswerTrackingService
{
    private readonly ISpokenAnswerTracker _tracker;
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<LiveSpokenAnswerTrackingService> _logger;

    public LiveSpokenAnswerTrackingService(
        ISpokenAnswerTracker tracker,
        IOptions<InterruptionHandlingOptions> options,
        ILogger<LiveSpokenAnswerTrackingService> logger)
    {
        _tracker = tracker;
        _options = options.Value;
        _logger = logger;
    }

    public void StartAnswer(
        string turnId,
        string correlationId,
        string originalUserQuestion,
        string? originalAssistantDraft = null,
        string? currentTopicLabel = null)
    {
        if (!IsEnabled())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        try
        {
            var state = _tracker.StartAnswer(
                turnId,
                correlationId,
                originalUserQuestion,
                originalAssistantDraft,
                currentTopicLabel);
            LogDiagnostics(
                "spoken_answer_tracking_started",
                state,
                $"DraftLength: {originalAssistantDraft?.Length ?? 0}. QuestionLength: {originalUserQuestion?.Length ?? 0}.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "spoken_answer_tracking_start_failed TurnId: {TurnId}. CorrelationId: {CorrelationId}.", turnId, correlationId);
        }
    }

    public void MarkChunkStarted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null)
    {
        if (!IsEnabled() || string.IsNullOrWhiteSpace(turnId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_tracker.GetState(turnId) is null)
        {
            return;
        }

        try
        {
            var state = _tracker.MarkChunkStarted(turnId, text, playbackPosition);
            LogDiagnostics("spoken_answer_tracking_chunk_started", state, $"ChunkLength: {text.Length}.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "spoken_answer_tracking_chunk_start_failed TurnId: {TurnId}.", turnId);
        }
    }

    public void MarkChunkCompleted(
        string turnId,
        string text,
        TimeSpan? playbackPosition = null)
    {
        if (!IsEnabled() || string.IsNullOrWhiteSpace(turnId) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_tracker.GetState(turnId) is null)
        {
            return;
        }

        try
        {
            var state = _tracker.MarkChunkCompleted(turnId, text, playbackPosition);
            LogDiagnostics("spoken_answer_tracking_chunk_completed", state, $"ChunkLength: {text.Length}.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "spoken_answer_tracking_chunk_complete_failed TurnId: {TurnId}.", turnId);
        }
    }

    public void MarkPlaybackCancelled(string turnId, string reason)
    {
        if (!IsEnabled() || string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        var state = _tracker.GetState(turnId);
        if (state is null)
        {
            return;
        }

        LogDiagnostics("spoken_answer_tracking_playback_cancelled", state, $"Reason: {reason}.");
    }

    public void CompleteAnswer(string turnId)
    {
        if (!IsEnabled() || string.IsNullOrWhiteSpace(turnId))
        {
            return;
        }

        var state = _tracker.GetState(turnId);
        if (state is not null)
        {
            LogDiagnostics("spoken_answer_tracking_completed", state, "Clearing spoken answer state.");
        }

        _tracker.Clear(turnId);
    }

    public SpokenAnswerCheckpoint? TryCreateCheckpoint(
        string turnId,
        bool discardCurrentPartialSentence = true)
    {
        if (!IsEnabled() || string.IsNullOrWhiteSpace(turnId))
        {
            return null;
        }

        if (_tracker.GetState(turnId) is null)
        {
            return null;
        }

        try
        {
            return _tracker.CreateCheckpoint(turnId, discardCurrentPartialSentence);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "spoken_answer_tracking_checkpoint_failed TurnId: {TurnId}.", turnId);
            return null;
        }
    }

    private bool IsEnabled() => _options.EnableLiveSpokenAnswerTracking;

    private void LogDiagnostics(string eventName, SpokenAnswerState state, string detail)
    {
        if (!_options.EnableSpokenAnswerTrackingDiagnostics)
        {
            return;
        }

        _logger.LogInformation(
            "{EventName} TurnId: {TurnId}. CorrelationId: {CorrelationId}. CanRecompose: {CanRecompose}. SpokenLength: {SpokenLength}. LastCompletedSentenceLength: {LastCompletedSentenceLength}. CurrentPartialSentenceLength: {CurrentPartialSentenceLength}. Detail: {Detail}",
            eventName,
            state.TurnId,
            state.CorrelationId,
            state.CanRecompose,
            state.SpokenSoFar.Length,
            state.LastCompletedSentence.Length,
            state.CurrentPartialSentence.Length,
            detail);
    }
}
