namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class QueuedFollowUp
{
    public string Id { get; init; } = "";

    public string UserText { get; init; } = "";

    public string RelatedTurnId { get; init; } = "";

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool RequiresDeepInfra { get; init; } = true;

    public string? Reason { get; init; }

    public string? CurrentTopicLabel { get; init; }
}
