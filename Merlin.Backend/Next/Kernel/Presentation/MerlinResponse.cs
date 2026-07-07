namespace Merlin.Backend.Next.Kernel.Presentation;

public sealed record MerlinResponse(
    MerlinResponseKind Kind,
    string? SpeakableText = null,
    string? DisplayText = null,
    bool ShouldSpeak = false,
    IReadOnlyDictionary<string, string?>? Metadata = null);
