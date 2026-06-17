namespace Merlin.Backend.Services;

public interface IChatProvider
{
    string Name { get; }

    Task<LlmProviderResult> GenerateAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default);
}
