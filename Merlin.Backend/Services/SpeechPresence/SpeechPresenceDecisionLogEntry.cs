namespace Merlin.Backend.Services.SpeechPresence;

public sealed record SpeechPresenceDecisionLogEntry
{
    public required string EventName { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required long FrameId { get; init; }

    public required string State { get; init; }

    public required double Confidence { get; init; }

    public required bool IsUserSpeaking { get; init; }

    public required bool ShouldYieldPlayback { get; init; }

    public required string Reason { get; init; }

    public required bool AssistantPlaybackActive { get; init; }

    public required double RawMicRms { get; init; }

    public required double RawMicPeak { get; init; }

    public required double EchoReducedRms { get; init; }

    public required double EchoReducedPeak { get; init; }

    public required double PlaybackReferenceRms { get; init; }

    public required double PlaybackReferencePeak { get; init; }

    public required double VadConfidence { get; init; }

    public required bool VadSpeechDetected { get; init; }

    public double? PlaybackCorrelationScore { get; init; }

    public required bool StrongSelfEchoEvidence { get; init; }

    public double? UserSpeechScoreLegacy { get; init; }

    public required string SourcePath { get; init; }

    public required bool IsAuthoritative { get; init; }

    public long DroppedEntriesBeforeThisEntry { get; init; }
}

public sealed record SpeechPresenceManualMarkerLogEntry
{
    public required string EventName { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required string MarkerType { get; init; }

    public required string Source { get; init; }

    public DateTimeOffset? ClientTimestampUtc { get; init; }

    public required string Note { get; init; }

    public long DroppedEntriesBeforeThisEntry { get; init; }
}
