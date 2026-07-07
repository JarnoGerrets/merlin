namespace Merlin.Backend.Next.Kernel.Requests;

public sealed record MerlinRequest(
    string RequestId,
    string? UserText,
    string Source,
    string? SourceSessionId,
    string? RequestedSurfaceId,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string?> Metadata);
