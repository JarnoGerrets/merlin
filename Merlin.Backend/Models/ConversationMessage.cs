namespace Merlin.Backend.Models;

public sealed class ConversationMessage
{
    public string Role { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; }
}
