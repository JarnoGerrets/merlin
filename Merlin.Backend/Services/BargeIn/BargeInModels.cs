using Merlin.Backend.Models;

namespace Merlin.Backend.Services.BargeIn;

public enum AecMode
{
    Active,
    DegradedNoOp,
    Unavailable
}

public enum InterruptionType
{
    None,
    NoiseOrEcho,
    Backchannel,
    HardStop,
    Pause,
    Correction,
    ClarificationQuestion,
    SideComment,
    TopicChange
}

public enum BargeInState
{
    Speaking,
    SoftPausedForUserSpeech,
    CapturingInterruption,
    ClassifyingInterruption,
    ResumingPreviousSpeech,
    CancellingCurrentTurn,
    RegeneratingWithCorrection,
    AnsweringClarificationThenResume
}

public enum BargeInAction
{
    Ignore,
    Resume,
    HardCancel,
    Correction,
    Clarification,
    SideComment
}

public sealed record AecConfiguration(int SampleRate, int FrameMs, string Provider);

public sealed record AecProcessResult
{
    public required ReadOnlyMemory<float> EchoReducedFrame { get; init; }

    public required AecMode Mode { get; init; }

    public required bool IsEchoCancellationActive { get; init; }

    public required string Reason { get; init; }
}

public sealed record VadFrameInput
{
    public required ReadOnlyMemory<float> Samples { get; init; }

    public required int SampleRate { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}

public sealed record VadFrameResult
{
    public required bool IsSpeech { get; init; }

    public required bool IsTriggered { get; init; }

    public required double Energy { get; init; }

    public required double NoiseFloor { get; init; }

    public required double Confidence { get; init; }

    public required int ConsecutiveSpeechMs { get; init; }
}

public sealed record BargeInAudioFrame
{
    public required ReadOnlyMemory<float> Samples { get; init; }

    public required int SampleRate { get; init; }

    public required DateTimeOffset Timestamp { get; init; }
}

public sealed record BargeInSpeechContext
{
    public required string AssistantTurnId { get; init; }

    public required string? CorrelationId { get; init; }

    public required SpeechPlaybackItemType SpeechType { get; init; }

    public required string SpokenText { get; init; }
}

public sealed record BargeInSttResult
{
    public required string Transcript { get; init; }

    public required TimeSpan AudioDuration { get; init; }
}

public sealed record InterruptionClassificationInput
{
    public required string RawTranscript { get; init; }

    public required string NormalizedTranscript { get; init; }

    public required string AssistantTurnId { get; init; }

    public required string CurrentSpeechType { get; init; }

    public required string SpokenTextSoFar { get; init; }

    public required double VadConfidence { get; init; }

    public required bool WasWakeWordPresent { get; init; }

    public required bool IsAecDegraded { get; init; }
}

public sealed record InterruptionClassificationResult
{
    public required InterruptionType Type { get; init; }

    public required double Confidence { get; init; }

    public required string Reason { get; init; }

    public string? CorrectedUserMessage { get; init; }
}

public sealed record BargeInDecision
{
    public required bool Accepted { get; init; }

    public required BargeInAction Action { get; init; }

    public required InterruptionClassificationResult Classification { get; init; }

    public required string Reason { get; init; }
}
