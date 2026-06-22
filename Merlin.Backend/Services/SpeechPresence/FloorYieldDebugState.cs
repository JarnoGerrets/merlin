namespace Merlin.Backend.Services.SpeechPresence;

public sealed record FloorYieldDebugState
{
    public bool Triggered { get; init; }

    public long? LastFrameId { get; init; }

    public string? LastReason { get; init; }

    public DateTimeOffset? LastTimestampUtc { get; init; }

    public string? LastMode { get; init; }

    public bool CandidateActive { get; init; }

    public long? CandidateStartFrameId { get; init; }

    public double? CandidateDurationMs { get; init; }

    public int RequiredSustainedMs { get; init; }
}
