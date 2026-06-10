namespace Merlin.Backend.Models;

public sealed class ConversationSession
{
    public string SessionId { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; }

    public IReadOnlyList<ConversationMessage> Messages { get; init; } = [];

    public string RunningSummary { get; init; } = string.Empty;
}
