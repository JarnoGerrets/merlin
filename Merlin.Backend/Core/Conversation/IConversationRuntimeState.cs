using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Conversation;

public interface IConversationRuntimeState
{
    Task<ConversationRecord> GetCurrentConversationAsync(CancellationToken cancellationToken = default);
    Task<ConversationTopicRecord?> GetCurrentTopicAsync(CancellationToken cancellationToken = default);
    Task<ConversationTopicRecord> StartOrSwitchTopicAsync(string topicTitle, CancellationToken cancellationToken = default);
    Task CompleteCurrentTopicAsync(string? summary, CancellationToken cancellationToken = default);
}
