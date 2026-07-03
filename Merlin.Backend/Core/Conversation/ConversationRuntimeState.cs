using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Conversation;

public sealed class ConversationRuntimeState : IConversationRuntimeState
{
    private readonly IConversationStateStore _conversationStateStore;
    private readonly IRuntimeTopicSession? _runtimeTopicSession;
    private readonly ILogger<ConversationRuntimeState>? _logger;

    public ConversationRuntimeState(
        IConversationStateStore conversationStateStore,
        IRuntimeTopicSession? runtimeTopicSession = null,
        ILogger<ConversationRuntimeState>? logger = null)
    {
        _conversationStateStore = conversationStateStore;
        _runtimeTopicSession = runtimeTopicSession;
        _logger = logger;
    }

    public Task<ConversationRecord> GetCurrentConversationAsync(CancellationToken cancellationToken = default) =>
        _conversationStateStore.GetOrCreateActiveConversationAsync(cancellationToken);

    public async Task<ConversationTopicRecord?> GetCurrentTopicAsync(CancellationToken cancellationToken = default)
    {
        var conversation = await GetCurrentConversationAsync(cancellationToken);
        var topic = await _conversationStateStore.GetActiveTopicAsync(conversation.Id, cancellationToken);
        if (topic is not null && _runtimeTopicSession is not null && !_runtimeTopicSession.IsTopicTouchedInCurrentProcess(topic.Id))
        {
            _logger?.LogInformation(
                "stale_current_topic_ignored. TopicId: {TopicId}. TopicTitle: {TopicTitle}. TopicUpdatedAt: {TopicUpdatedAt}. BackendStartedAt: {BackendStartedAt}. Reason: {Reason}. ConversationId: {ConversationId}. CorrelationId: {CorrelationId}.",
                topic.Id,
                topic.Title,
                topic.StartedAt,
                _runtimeTopicSession.BackendStartedAtUtc,
                "topic_not_touched_in_current_process",
                conversation.Id,
                null);
            return null;
        }

        return topic;
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

        var topic = await _conversationStateStore.StartTopicAsync(conversation.Id, topicTitle, cancellationToken);
        _runtimeTopicSession?.MarkTopicTouched(topic.Id);
        return topic;
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
