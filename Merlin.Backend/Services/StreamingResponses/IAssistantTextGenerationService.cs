namespace Merlin.Backend.Services.StreamingResponses;

public interface IAssistantTextGenerationService
{
    IAsyncEnumerable<ModelTextDelta> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
