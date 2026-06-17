using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Conversation;

public sealed class ConversationRuntimeState : IConversationRuntimeState
{
    private readonly IConversationStateStore _conversationStateStore;

    public ConversationRuntimeState(IConversationStateStore conversationStateStore)
    {
        _conversationStateStore = conversationStateStore;
    }

    public Task<ConversationRecord> GetCurrentConversationAsync(CancellationToken cancellationToken = default) =>
        _conversationStateStore.GetOrCreateActiveConversationAsync(cancellationToken);

    public async Task<ConversationTopicRecord?> GetCurrentTopicAsync(CancellationToken cancellationToken = default)
    {
        var conversation = await GetCurrentConversationAsync(cancellationToken);
        return await _conversationStateStore.GetActiveTopicAsync(conversation.Id, cancellationToken);
    }

    public async Task<ConversationTopicRecord> StartOrSwitchTopicAsync(
        string topicTitle,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetCurrentConversationAsync(cancellationToken);
        var currentTopic = await _conversationStateStore.GetActiveTopicAsync(conversation.Id, cancellationToken);
        if (currentTopic is not null && !string.Equals(currentTopic.Title, topicTitle, StringComparison.OrdinalIgnoreCase))
        {
            await _conversationStateStore.EndTopicAsync(
                currentTopic.Id,
                ConversationTopicStatuses.Paused,
                currentTopic.Summary,
                cancellationToken);
        }

        return await _conversationStateStore.StartTopicAsync(conversation.Id, topicTitle, cancellationToken);
    }

    public async Task CompleteCurrentTopicAsync(string? summary, CancellationToken cancellationToken = default)
    {
        var conversation = await GetCurrentConversationAsync(cancellationToken);
        var currentTopic = await _conversationStateStore.GetActiveTopicAsync(conversation.Id, cancellationToken);
        if (currentTopic is null)
        {
            return;
        }

        await _conversationStateStore.EndTopicAsync(
            currentTopic.Id,
            ConversationTopicStatuses.Completed,
            summary,
            cancellationToken);
    }
}
