using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Conversation;

public interface IAssistantTurnTracker
{
    Task<AssistantTurnRecord> StartTurnAsync(string originalUserMessage, string? topicId, CancellationToken cancellationToken = default);
    Task AppendGeneratedTextAsync(string turnId, string textDelta, CancellationToken cancellationToken = default);
    Task AppendSpokenTextAsync(string turnId, string spokenTextDelta, CancellationToken cancellationToken = default);
    Task MarkTurnStateAsync(string turnId, string state, CancellationToken cancellationToken = default);
    Task MarkInterruptedAsync(string turnId, string reason, string interruptedByUserMessage, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(string turnId, CancellationToken cancellationToken = default);
}
