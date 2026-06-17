using Merlin.Backend.Models;

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
}
