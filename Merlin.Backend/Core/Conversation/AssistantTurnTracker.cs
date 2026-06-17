using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Conversation;

public sealed class AssistantTurnTracker : IAssistantTurnTracker
{
    private readonly IConversationRuntimeState _conversationRuntimeState;
    private readonly ITurnStateStore _turnStateStore;

    public AssistantTurnTracker(
        IConversationRuntimeState conversationRuntimeState,
        ITurnStateStore turnStateStore)
    {
        _conversationRuntimeState = conversationRuntimeState;
        _turnStateStore = turnStateStore;
    }

    public async Task<AssistantTurnRecord> StartTurnAsync(
        string originalUserMessage,
        string? topicId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRuntimeState.GetCurrentConversationAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var turn = new AssistantTurnRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversation.Id,
            TopicId = topicId,
            OriginalUserMessage = originalUserMessage,
            State = AssistantTurnStates.Created,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _turnStateStore.CreateTurnAsync(turn, cancellationToken);
        return turn;
    }

    public async Task AppendGeneratedTextAsync(
        string turnId,
        string textDelta,
        CancellationToken cancellationToken = default)
    {
        var turn = await RequireTurnAsync(turnId, cancellationToken);
        await _turnStateStore.UpdateGeneratedTextAsync(
            turnId,
            string.Concat(turn.GeneratedTextSoFar, textDelta),
            cancellationToken);
    }

    public async Task AppendSpokenTextAsync(
        string turnId,
        string spokenTextDelta,
        CancellationToken cancellationToken = default)
    {
        var turn = await RequireTurnAsync(turnId, cancellationToken);
        await _turnStateStore.UpdateSpokenTextAsync(
            turnId,
            string.Concat(turn.SpokenTextSoFar, spokenTextDelta),
            cancellationToken);
    }

    public Task MarkTurnStateAsync(string turnId, string state, CancellationToken cancellationToken = default) =>
        _turnStateStore.UpdateStateAsync(turnId, state, cancellationToken);

    public Task MarkInterruptedAsync(
        string turnId,
        string reason,
        string interruptedByUserMessage,
        CancellationToken cancellationToken = default) =>
        _turnStateStore.MarkInterruptedAsync(turnId, reason, interruptedByUserMessage, cancellationToken);

    public Task MarkCompletedAsync(string turnId, CancellationToken cancellationToken = default) =>
        _turnStateStore.MarkCompletedAsync(turnId, cancellationToken);

    private async Task<AssistantTurnRecord> RequireTurnAsync(string turnId, CancellationToken cancellationToken) =>
        await _turnStateStore.GetTurnAsync(turnId, cancellationToken)
        ?? throw new InvalidOperationException($"Assistant turn '{turnId}' was not found.");
}
