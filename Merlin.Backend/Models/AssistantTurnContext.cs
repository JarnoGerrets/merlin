namespace Merlin.Backend.Models;

public sealed record AssistantTurnContext(
    string ConversationId,
    string CorrelationId,
    string AssistantTurnId,
    CancellationToken CancellationToken);
