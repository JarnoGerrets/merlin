namespace Merlin.Backend.Services;

using Merlin.Backend.Models;
using Merlin.Backend.Services.StreamingResponses;

public interface ILocalAIChatService
{
    Task<LocalAIChatResult> GenerateResponseAsync(
        string message,
        CancellationToken cancellationToken = default);

    async Task<StreamingConversationResult> GenerateStreamingResponseAsync(
        string message,
        string? correlationId,
        Func<AssistantVisualEvent, CancellationToken, Task>? sendEventAsync,
        bool shouldSpeak,
        Action? streamingFinalAnswerStarted = null,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateResponseAsync(message, cancellationToken);
        return new StreamingConversationResult
        {
            Success = result.Success,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            FallbackUsed = true
        };
    }
}
