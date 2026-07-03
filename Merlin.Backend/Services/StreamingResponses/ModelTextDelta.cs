namespace Merlin.Backend.Services.StreamingResponses;

public sealed record ModelTextDelta(
    string Text,
    bool IsFinal = false,
    string? Provider = null,
    string? Model = null,
    int? SequenceNumber = null);
