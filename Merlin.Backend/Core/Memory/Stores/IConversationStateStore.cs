using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Stores;

public interface IConversationStateStore
{
    Task<ConversationRecord> GetOrCreateActiveConversationAsync(CancellationToken cancellationToken = default);
    Task UpdateActiveTopicAsync(string conversationId, string? activeTopic, CancellationToken cancellationToken = default);
    Task<ConversationTopicRecord> StartTopicAsync(string conversationId, string title, CancellationToken cancellationToken = default);
    Task UpdateTopicSummaryAsync(string topicId, string? summary, CancellationToken cancellationToken = default);
    Task EndTopicAsync(string topicId, string status, string? summary, CancellationToken cancellationToken = default);
    Task<ConversationTopicRecord?> GetActiveTopicAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<ConversationTopicRecord?> GetTopicAsync(string topicId, CancellationToken cancellationToken = default);
}
