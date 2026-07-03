using System.Runtime.CompilerServices;

namespace Merlin.Backend.Services.StreamingResponses;

public sealed class NonStreamingGenerationAdapter : IAssistantTextGenerationService
{
    private readonly IChatProvider _provider;

    public NonStreamingGenerationAdapter(IChatProvider provider)
    {
        _provider = provider;
    }

    public async IAsyncEnumerable<ModelTextDelta> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _provider.GenerateAsync(messages, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!result.Success || string.IsNullOrWhiteSpace(result.Message))
        {
            throw new InvalidOperationException(result.ErrorMessage ?? result.ErrorCode ?? "Model generation failed.");
        }

        yield return new ModelTextDelta(
            result.Message,
            IsFinal: true,
            Provider: _provider.Name);
    }
}
