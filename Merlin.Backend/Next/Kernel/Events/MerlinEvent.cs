namespace Merlin.Backend.Next.Kernel.Events;

public sealed record MerlinEvent(
    string EventId,
    string EventType,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string?> Metadata);
