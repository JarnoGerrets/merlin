using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IAssistantSpeechPlaybackService
{
    Task EnqueueAsync(
        string text,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
        CancellationToken cancellationToken);

    Task StopCurrentAsync(CancellationToken cancellationToken = default);

    Task ClearQueueAsync(CancellationToken cancellationToken = default);
}
