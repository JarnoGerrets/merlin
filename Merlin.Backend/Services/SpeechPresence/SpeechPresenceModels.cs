namespace Merlin.Backend.Services.SpeechPresence;

public enum SpeechPresenceState
{
    No,
    Maybe,
    Yes
}

public sealed class SpeechPresenceEvidence
{
    public long FrameId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public bool AssistantPlaybackActive { get; init; }

    public double RawMicRms { get; init; }

    public double RawMicPeak { get; init; }

    public double EchoReducedRms { get; init; }

    public double EchoReducedPeak { get; init; }

    public double PlaybackReferenceRms { get; init; }

    public double PlaybackReferencePeak { get; init; }

    public double VadConfidence { get; init; }

    public bool VadSpeechDetected { get; init; }

    public double? PlaybackCorrelationScore { get; init; }

    public bool StrongSelfEchoEvidence { get; init; }

    public double? UserSpeechScoreLegacy { get; init; }

    public string SourcePath { get; init; } = "";
}

public sealed class SpeechPresenceResult
{
    public SpeechPresenceState State { get; init; }

    public double Confidence { get; init; }

    public bool IsUserSpeaking { get; init; }

    public bool ShouldYieldPlayback { get; init; }

    public string Reason { get; init; } = "";

    public SpeechPresenceEvidence Evidence { get; init; } = new();
}

public sealed class SpeechPresenceOfficialDecision
{
    public long FrameId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public SpeechPresenceResult Result { get; init; } = new();

    public bool IsAuthoritative { get; init; } = true;

    public string SourcePath { get; init; } = "official_frame_decision";
}

public sealed class SpeechPresenceBranchObservation
{
    public long FrameId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public string SourcePath { get; init; } = "";

    public SpeechPresenceResult Result { get; init; } = new();

    public bool IsAuthoritative { get; init; }
}

public sealed class SpeechPresenceManualMarker
{
    public DateTimeOffset TimestampUtc { get; init; }

    public string MarkerType { get; init; } = "";

    public string Source { get; init; } = "";

    public DateTimeOffset? ClientTimestampUtc { get; init; }

    public string Note { get; init; } = "";
}
