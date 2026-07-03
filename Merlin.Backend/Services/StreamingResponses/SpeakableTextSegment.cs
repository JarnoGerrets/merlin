namespace Merlin.Backend.Services.StreamingResponses;

public sealed record SpeakableTextSegment(
    string Text,
    int SequenceNumber,
    bool IsFinalSegment = false,
    bool WasForcedFlush = false,
    SpeakableBoundaryKind BoundaryKind = SpeakableBoundaryKind.Unknown);

public enum SpeakableBoundaryKind
{
    Unknown,
    Sentence,
    Clause,
    Paragraph,
    FinalFlush,
    ForcedLongBufferFlush
}
