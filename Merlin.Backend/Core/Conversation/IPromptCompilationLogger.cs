namespace Merlin.Backend.Core.Conversation;

public interface IPromptCompilationLogger
{
    Task LogPromptAsync(
        string conversationId,
        string? turnId,
        string promptType,
        string compiledPrompt,
        int? estimatedInputTokens,
        IReadOnlyCollection<string> includedMemoryIds,
        IReadOnlyCollection<string> includedConceptIds,
        CancellationToken cancellationToken = default);
}
