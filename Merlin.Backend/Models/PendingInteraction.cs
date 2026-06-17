namespace Merlin.Backend.Models;

public sealed class PendingInteraction
{
    public string InteractionId { get; init; } = Guid.NewGuid().ToString("N");

    public string Type { get; init; } = string.Empty;

    public string Prompt { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Context { get; init; } = new Dictionary<string, string>();

    public string OriginalUserCommand { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAtUtc { get; init; } = DateTimeOffset.UtcNow.AddSeconds(30);
}
