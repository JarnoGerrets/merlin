using System.Text.Json;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Conversation;

public sealed class PromptCompilationLogger : IPromptCompilationLogger
{
    private readonly IPromptCompilationStore _promptCompilationStore;
    private readonly ITokenEstimator _tokenEstimator;

    public PromptCompilationLogger(
        IPromptCompilationStore promptCompilationStore,
        ITokenEstimator tokenEstimator)
    {
        _promptCompilationStore = promptCompilationStore;
        _tokenEstimator = tokenEstimator;
    }

    public async Task LogPromptAsync(
        string conversationId,
        string? turnId,
        string promptType,
        string compiledPrompt,
        int? estimatedInputTokens,
        IReadOnlyCollection<string> includedMemoryIds,
        IReadOnlyCollection<string> includedConceptIds,
        CancellationToken cancellationToken = default)
    {
        var record = new PromptCompilationRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversationId,
            TurnId = turnId,
            PromptType = promptType,
            CompiledPrompt = compiledPrompt,
            EstimatedInputTokens = estimatedInputTokens ?? _tokenEstimator.EstimateTokens(compiledPrompt),
            IncludedMemoryIdsJson = JsonSerializer.Serialize(includedMemoryIds),
            IncludedConceptIdsJson = JsonSerializer.Serialize(includedConceptIds),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _promptCompilationStore.SavePromptCompilationAsync(record, cancellationToken);
    }
}
