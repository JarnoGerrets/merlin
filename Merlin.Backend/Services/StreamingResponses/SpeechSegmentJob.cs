namespace Merlin.Backend.Services.StreamingResponses;

public enum SpeechSegmentState
{
    Generated,
    QueuedForTts,
    Synthesizing,
    ReadyToPlay,
    SubmittedToPlayback,
    AcceptedByPlaybackSession,
    Playing,
    Spoken,
    Cancelled,
    Failed
}

public sealed class SpeechSegmentJob
{
    public required Guid Id { get; init; }

    public required int SequenceNumber { get; init; }

    public required string Text { get; init; }

    public SpeechSegmentState State { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? PlaybackStartedAt { get; set; }

    public DateTimeOffset? PlaybackCompletedAt { get; set; }
}
