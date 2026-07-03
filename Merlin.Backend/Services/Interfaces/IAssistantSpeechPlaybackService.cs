using Merlin.Backend.Models;
using Merlin.Backend.Services.StreamingResponses;

namespace Merlin.Backend.Services;

public interface IAssistantSpeechPlaybackService
{
    Task EnqueueAsync(
        string text,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? speechCacheKey,
        bool? isReplayableSpeech,
        CancellationToken cancellationToken,
        SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
        bool cancelOnlyBeforePlayback = false);

    Task StopCurrentAsync(CancellationToken cancellationToken = default);

    Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default);

    Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default);

    Task ClearQueueAsync(CancellationToken cancellationToken = default);

    Task<ProvisionalAudioHoldResult> BeginProvisionalAudioHoldAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProvisionalAudioHoldResult.Failed(turnId, reason, "Provisional audio hold is not supported."));

    Task<ProvisionalAudioHoldResult> ResumeProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProvisionalAudioHoldResult.Failed(null, reason, "Provisional audio hold is not supported.", holdId));

    Task<ProvisionalAudioHoldResult> FlushProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProvisionalAudioHoldResult.Failed(null, reason, "Provisional audio hold is not supported.", holdId));

    Task FlushFinalAnswerSpeechForTurnAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<IStreamingFinalAnswerPlaybackSession> BeginStreamingFinalAnswerAsync(
        string turnId,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        string? originalUserQuestion = null,
        CancellationToken cancellationToken = default) =>
        Task.FromException<IStreamingFinalAnswerPlaybackSession>(
            new NotSupportedException("Streaming final-answer playback is not supported."));

    ActiveSpeechPlaybackSnapshot? GetActivePlaybackSnapshot() => null;
}

public interface IStreamingFinalAnswerPlaybackSession : IAsyncDisposable
{
    string SessionId { get; }

    string TurnId { get; }

    string CorrelationId { get; }

    long GenerationId { get; }

    Task EnqueueTextSegmentAsync(
        StreamingFinalAnswerTextSegment segment,
        CancellationToken cancellationToken = default);

    Task CompleteInputAsync(CancellationToken cancellationToken = default);

    Task CancelAsync(string reason, CancellationToken cancellationToken = default);
}
