namespace Merlin.Backend.Services.StreamingResponses;

public sealed record StreamingFinalAnswerTextSegment
{
    public required string TurnId { get; init; }

    public required string CorrelationId { get; init; }

    public required long GenerationId { get; init; }

    public required int SegmentIndex { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset EmittedAtUtc { get; init; }

    public bool IsFinalSegment { get; init; }

    public string BoundaryKind { get; init; } = string.Empty;
}
