using Merlin.Backend.Models;

namespace Merlin.Backend.Services.BargeIn;

public enum AecMode
{
    Active,
    DegradedNoOp,
    Unavailable
}

public enum AcousticCaptureMode
{
    IdleUserRequest,
    AssistantInterruption
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

public enum SelfSpeechDecision
{
    Allow,
    SuppressAsSelfEcho,
    Uncertain
}

public static class SelfSpeechCorrelationDecision
{
    public const string Unavailable = "Unavailable";
    public const string SelfEcho = "SelfEcho";
    public const string LikelyUser = "LikelyUser";
    public const string WeakCorrelation = "WeakCorrelation";
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

    public long SequenceNumber { get; init; }

    public int DurationMs { get; init; }

    public int AnalysisQueueDepth { get; init; }

    public long AnalysisFramesDropped { get; init; }
}

public sealed record ContinuousMicAudioRange
{
    public required IReadOnlyList<BargeInAudioFrame> Frames { get; init; }

    public required int RequestedPreRollMs { get; init; }

    public required int ActualPreRollMsAvailable { get; init; }

    public required int ActualPreRollMsIncluded { get; init; }

    public required int PreRollFramesIncluded { get; init; }

    public required int? OldestBufferedFrameAgeMs { get; init; }

    public required int ContinuousRecorderBufferMs { get; init; }

    public required long ContinuousFramesDropped { get; init; }

    public required int FrameGapCount { get; init; }

    public required int MaxCaptureFrameGapMs { get; init; }
}

public sealed record BargeInTriggeredCapture
{
    public required IReadOnlyList<BargeInAudioFrame> Frames { get; init; }

    public required int RequestedPreRollMs { get; init; }

    public required int ActualPreRollMsAvailable { get; init; }

    public required int ActualPreRollMsIncluded { get; init; }

    public required int PreRollFramesIncluded { get; init; }

    public required int? OldestBufferedFrameAgeMs { get; init; }

    public required string BufferResetReason { get; init; }

    public required string? BufferOwnerAssistantTurnId { get; init; }

    public required string? CurrentAssistantTurnId { get; init; }
}

public sealed record BargeInSpeechContext
{
    public required string AssistantTurnId { get; init; }

    public required string? CorrelationId { get; init; }

    public required SpeechPlaybackItemType SpeechType { get; init; }

    public required string SpokenText { get; init; }
}

public sealed record SelfSpeechGateInput
{
    public required bool AssistantPlaybackActive { get; init; }

    public required double MicEnergy { get; init; }

    public required double PlaybackEnergy { get; init; }

    public double? CurrentPlaybackEnergy { get; init; }

    public double? RecentPlaybackEnergy { get; init; }

    public required bool AecVerified { get; init; }

    public required bool VadSaysSpeech { get; init; }

    public double? VadConfidence { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required TimeSpan? PlaybackAge { get; init; }

    public required string Reason { get; init; }

    public string? CorrelationId { get; init; }

    public double? CorrelationScore { get; init; }

    public double? BestDelayMs { get; init; }

    public string? CorrelationDecision { get; init; }

    public bool? CorrelationAvailable { get; init; }

    public string? CorrelationReason { get; init; }

    public bool? ReferenceWindowAvailable { get; init; }

    public double? ReferenceWindowEnergy { get; init; }

    public int? ReferenceWindowSampleCount { get; init; }

    public int? RequestedMicSampleCount { get; init; }

    public int? RequestedDelayMinMs { get; init; }

    public int? RequestedDelayMaxMs { get; init; }

    public int? RequestedDelayStepMs { get; init; }

    public int? PlaybackRingBufferedSamples { get; init; }

    public int? PlaybackRingCapacitySamples { get; init; }

    public double? PlaybackRingBufferedMs { get; init; }

    public int? PlaybackTapSampleRate { get; init; }

    public int? MicSampleRate { get; init; }

    public bool? SampleRateMatches { get; init; }

    public int? PlaybackWritePosition { get; init; }

    public int? NumberOfDelayWindowsChecked { get; init; }

    public int? NumberOfDelayWindowsAvailable { get; init; }

    public int? NumberOfDelayWindowsSkippedLowEnergy { get; init; }

    public double? MaxReferenceEnergySeen { get; init; }

    public string? CorrelationUnavailableReason { get; init; }

    public string? PlaybackReferenceSource { get; init; }

    public bool? PlaybackReferenceIsConsumptionAligned { get; init; }

    public long? PlaybackConsumedSamplesTotal { get; init; }

    public double? ReferenceBufferedMs { get; init; }

    public double? ReferenceNewestAgeMs { get; init; }

    public double? ReferenceOldestAgeMs { get; init; }

    public int? OutputReadSamples { get; init; }

    public double? OutputReadDurationMs { get; init; }

    public DateTimeOffset? LastOutputReadAtUtc { get; init; }
}

public sealed record SelfSpeechGateResult
{
    public required SelfSpeechDecision Decision { get; init; }

    public required double Confidence { get; init; }

    public required string Reason { get; init; }

    public required double MicEnergy { get; init; }

    public required double PlaybackEnergy { get; init; }

    public required double EstimatedEchoEnergy { get; init; }

    public required double UserSpeechScore { get; init; }

    public required int SustainedUncertainFrames { get; init; }

    public double? CorrelationScore { get; init; }

    public double? BestDelayMs { get; init; }

    public string? CorrelationDecision { get; init; }

    public bool? CorrelationAvailable { get; init; }

    public string? CorrelationReason { get; init; }

    public bool? ReferenceWindowAvailable { get; init; }

    public double? ReferenceWindowEnergy { get; init; }

    public int? ReferenceWindowSampleCount { get; init; }

    public int? RequestedMicSampleCount { get; init; }

    public int? RequestedDelayMinMs { get; init; }

    public int? RequestedDelayMaxMs { get; init; }

    public int? RequestedDelayStepMs { get; init; }

    public int? PlaybackRingBufferedSamples { get; init; }

    public int? PlaybackRingCapacitySamples { get; init; }

    public double? PlaybackRingBufferedMs { get; init; }

    public int? PlaybackTapSampleRate { get; init; }

    public int? MicSampleRate { get; init; }

    public bool? SampleRateMatches { get; init; }

    public int? PlaybackWritePosition { get; init; }

    public int? NumberOfDelayWindowsChecked { get; init; }

    public int? NumberOfDelayWindowsAvailable { get; init; }

    public int? NumberOfDelayWindowsSkippedLowEnergy { get; init; }

    public double? MaxReferenceEnergySeen { get; init; }

    public string? CorrelationUnavailableReason { get; init; }

    public string? PlaybackReferenceSource { get; init; }

    public bool? PlaybackReferenceIsConsumptionAligned { get; init; }

    public long? PlaybackConsumedSamplesTotal { get; init; }

    public double? ReferenceBufferedMs { get; init; }

    public double? ReferenceNewestAgeMs { get; init; }

    public double? ReferenceOldestAgeMs { get; init; }

    public int? OutputReadSamples { get; init; }

    public double? OutputReadDurationMs { get; init; }

    public DateTimeOffset? LastOutputReadAtUtc { get; init; }
}

public sealed record SelfSpeechCorrelationResult
{
    public required bool IsAvailable { get; init; }

    public required double? CorrelationScore { get; init; }

    public required double? BestDelayMs { get; init; }

    public required string Decision { get; init; }

    public required string Reason { get; init; }

    public bool ReferenceWindowAvailable { get; init; }

    public double? ReferenceWindowEnergy { get; init; }

    public int ReferenceWindowSampleCount { get; init; }

    public int RequestedMicSampleCount { get; init; }

    public int RequestedDelayMinMs { get; init; }

    public int RequestedDelayMaxMs { get; init; }

    public int RequestedDelayStepMs { get; init; }

    public int PlaybackRingBufferedSamples { get; init; }

    public int PlaybackRingCapacitySamples { get; init; }

    public double PlaybackRingBufferedMs { get; init; }

    public int PlaybackTapSampleRate { get; init; }

    public int MicSampleRate { get; init; }

    public bool SampleRateMatches { get; init; }

    public int PlaybackWritePosition { get; init; }

    public int NumberOfDelayWindowsChecked { get; init; }

    public int NumberOfDelayWindowsAvailable { get; init; }

    public int NumberOfDelayWindowsSkippedLowEnergy { get; init; }

    public double MaxReferenceEnergySeen { get; init; }

    public string? CorrelationUnavailableReason { get; init; }

    public string? PlaybackReferenceSource { get; init; }

    public bool PlaybackReferenceIsConsumptionAligned { get; init; }

    public long PlaybackConsumedSamplesTotal { get; init; }

    public double ReferenceBufferedMs { get; init; }

    public double? ReferenceNewestAgeMs { get; init; }

    public double? ReferenceOldestAgeMs { get; init; }

    public int OutputReadSamples { get; init; }

    public double OutputReadDurationMs { get; init; }

    public DateTimeOffset? LastOutputReadAtUtc { get; init; }
}

public sealed record PlaybackReferenceDebugSnapshot
{
    public required bool IsPlaybackActive { get; init; }

    public required int SampleRate { get; init; }

    public required int BufferedSamples { get; init; }

    public required int CapacitySamples { get; init; }

    public required double BufferedMilliseconds { get; init; }

    public required double CurrentPlaybackEnergy { get; init; }

    public required double RecentPlaybackEnergy { get; init; }

    public required int WritePosition { get; init; }

    public required DateTimeOffset? PlaybackStartedAt { get; init; }

    public string? PlaybackReferenceSource { get; init; }

    public bool PlaybackReferenceIsConsumptionAligned { get; init; }

    public long PlaybackConsumedSamplesTotal { get; init; }

    public double ReferenceBufferedMilliseconds { get; init; }

    public double? ReferenceNewestAgeMilliseconds { get; init; }

    public double? ReferenceOldestAgeMilliseconds { get; init; }

    public int LastOutputReadSamples { get; init; }

    public double LastOutputReadDurationMilliseconds { get; init; }

    public DateTimeOffset? LastOutputReadAtUtc { get; init; }
}

public sealed record SelfSpeechGateDiagnosticEntry
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required string InputReason { get; init; }

    public required string Decision { get; init; }

    public required string Reason { get; init; }

    public required double MicEnergy { get; init; }

    public required double PlaybackEnergy { get; init; }

    public double? CurrentPlaybackEnergy { get; init; }

    public double? RecentPlaybackEnergy { get; init; }

    public required double EstimatedEchoEnergy { get; init; }

    public required double EchoLeakageMultiplier { get; init; }

    public required double EchoMargin { get; init; }

    public required double UserSpeechRatio { get; init; }

    public required double UserSpeechMargin { get; init; }

    public required double UserSpeechScore { get; init; }

    public long? PlaybackAgeMs { get; init; }

    public required bool AssistantPlaybackActive { get; init; }

    public required bool VadSaysSpeech { get; init; }

    public double? VadConfidence { get; init; }

    public bool? AecVerified { get; init; }

    public required int SustainedUncertainFrames { get; init; }

    public required int RequiredSustainedUserSpeechFrames { get; init; }

    public required string ConfigPolicyMode { get; init; }

    public string? CorrelationId { get; init; }

    public double? CorrelationScore { get; init; }

    public double? BestDelayMs { get; init; }

    public string? CorrelationDecision { get; init; }

    public bool? CorrelationAvailable { get; init; }

    public double? CorrelationMinScore { get; init; }

    public int? CorrelationMinDelayMs { get; init; }

    public int? CorrelationMaxDelayMs { get; init; }

    public int? CorrelationDelayStepMs { get; init; }

    public string? CorrelationReason { get; init; }

    public bool? ReferenceWindowAvailable { get; init; }

    public double? ReferenceWindowEnergy { get; init; }

    public int? ReferenceWindowSampleCount { get; init; }

    public int? RequestedMicSampleCount { get; init; }

    public int? RequestedDelayMinMs { get; init; }

    public int? RequestedDelayMaxMs { get; init; }

    public int? RequestedDelayStepMs { get; init; }

    public int? PlaybackRingBufferedSamples { get; init; }

    public int? PlaybackRingCapacitySamples { get; init; }

    public double? PlaybackRingBufferedMs { get; init; }

    public int? PlaybackTapSampleRate { get; init; }

    public int? MicSampleRate { get; init; }

    public bool? SampleRateMatches { get; init; }

    public int? PlaybackWritePosition { get; init; }

    public int? NumberOfDelayWindowsChecked { get; init; }

    public int? NumberOfDelayWindowsAvailable { get; init; }

    public int? NumberOfDelayWindowsSkippedLowEnergy { get; init; }

    public double? MaxReferenceEnergySeen { get; init; }

    public string? CorrelationUnavailableReason { get; init; }

    public string? PlaybackReferenceSource { get; init; }

    public bool? PlaybackReferenceIsConsumptionAligned { get; init; }

    public long? PlaybackConsumedSamplesTotal { get; init; }

    public double? ReferenceBufferedMs { get; init; }

    public double? ReferenceNewestAgeMs { get; init; }

    public double? ReferenceOldestAgeMs { get; init; }

    public int? OutputReadSamples { get; init; }

    public double? OutputReadDurationMs { get; init; }

    public DateTimeOffset? LastOutputReadAtUtc { get; init; }
}

public sealed record CorrectionRegenerationRequested
{
    public required string OriginalCorrelationId { get; init; }

    public required string CorrectionText { get; init; }

    public required BargeInSpeechContext SpeechContext { get; init; }
}

public sealed record BackendVoiceRequestCaptured
{
    public required string CorrelationId { get; init; }

    public required string Text { get; init; }

    public required string InteractionSource { get; init; }

    public required UserUtterance Utterance { get; init; }

    public required BargeInSpeechContext SpeechContext { get; init; }
}

public enum UtteranceRouteKind
{
    PauseAndClarify,
    CancelActiveTurn,
    ReplaceActiveTurn,
    AddToActiveTurn,
    QueueAfterActiveTurn,
    StatusQuestion,
    BackgroundOrNoOp,
    Unknown
}

public sealed record UserUtterance
{
    public required string Text { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required string? ActiveTurnId { get; init; }

    public required string? CorrelationId { get; init; }

    public required LiveAssistantTurnState StateWhenCaptured { get; init; }

    public required bool AssistantWasSpeaking { get; init; }

    public required string Source { get; init; }

    public double? Confidence { get; init; }
}

public sealed record UtteranceRouteDecision
{
    public required UtteranceRouteKind Kind { get; init; }

    public required double Confidence { get; init; }

    public required string Reason { get; init; }

    public string? ReplacementText { get; init; }

    public required string Action { get; init; }
}

public sealed record LiveUserUtteranceRouted
{
    public required UserUtterance Utterance { get; init; }

    public required UtteranceRouteDecision Decision { get; init; }
}

public sealed class SpeakerDuckingChangedEventArgs : EventArgs
{
    public required bool IsDucked { get; init; }

    public required float VolumeMultiplier { get; init; }

    public required string Reason { get; init; }

    public required TimeSpan FadeDuration { get; init; }

    public required DateTimeOffset ChangedAtUtc { get; init; }
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

public sealed record InterruptionCaptureDiagnostic
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required string CaptureKind { get; init; }

    public required string AssistantTurnId { get; init; }

    public required string? CorrelationId { get; init; }

    public required string SpeechType { get; init; }

    public required bool AssistantWasSpeaking { get; init; }

    public required bool DuckingWasActive { get; init; }

    public required int FrameCount { get; init; }

    public required int AudioMs { get; init; }

    public required int PreRollMs { get; init; }

    public required int RequestedPreRollMs { get; init; }

    public required int ActualPreRollMsAvailable { get; init; }

    public required int ActualPreRollMsIncluded { get; init; }

    public required int PreRollFramesIncluded { get; init; }

    public required int? OldestBufferedFrameAgeMs { get; init; }

    public required string BufferResetReason { get; init; }

    public required string? BufferOwnerAssistantTurnId { get; init; }

    public required string? CurrentAssistantTurnId { get; init; }

    public required int PostPaddingMs { get; init; }

    public required int MaxCaptureMs { get; init; }

    public required string CaptureEndReason { get; init; }

    public required int? FirstSpeechFrameRelativeMs { get; init; }

    public required int? LastSpeechFrameRelativeMs { get; init; }

    public required int CapturedSpeechMs { get; init; }

    public required int RawSpeechFrames { get; init; }

    public required int AecSpeechFrames { get; init; }

    public required int VadSpeechFrames { get; init; }

    public required int CaptureSpeechFrames { get; init; }

    public required int FalseSilenceFramesWhileVadSpeech { get; init; }

    public required DateTimeOffset CaptureStartUtc { get; init; }

    public required DateTimeOffset CaptureEndUtc { get; init; }

    public required int CaptureWallClockMs { get; init; }

    public required int SttInputAudioMs { get; init; }

    public required int ContinuousRecorderBufferMs { get; init; }

    public required long AnalysisFramesDropped { get; init; }

    public required long ContinuousFramesDropped { get; init; }

    public required double MaxProcessingLagMs { get; init; }

    public required double AverageProcessingLagMs { get; init; }

    public required int FrameGapCount { get; init; }

    public required int MaxCaptureFrameGapMs { get; init; }

    public required bool BuiltFromContinuousRecorder { get; init; }

    public required int SampleRate { get; init; }

    public required int SampleCount { get; init; }

    public required string SttTranscript { get; init; }

    public required string NormalizedTranscript { get; init; }

    public required string ClassificationType { get; init; }

    public required double ClassificationConfidence { get; init; }

    public required string ClassificationReason { get; init; }

    public required string DecisionAction { get; init; }

    public required bool DecisionAccepted { get; init; }

    public required string DecisionReason { get; init; }

    public required double VadConfidence { get; init; }

    public required bool WasWakeWordPresent { get; init; }

    public required bool IsAecDegraded { get; init; }

    public required string AecMode { get; init; }

    public string? CaptureStartReason { get; init; }

    public int? CandidateBurstMsAtPromotion { get; init; }

    public int? BurstTotalFrames { get; init; }

    public int? BurstVadSpeechFrames { get; init; }

    public int? BurstComfortDuckingFrames { get; init; }

    public int? BurstAllowFrames { get; init; }

    public int? BurstUncertainFrames { get; init; }

    public int? BurstSuppressAsSelfEchoFrames { get; init; }

    public int? BurstStrongSelfEchoFrames { get; init; }

    public double? BurstStrongSelfEchoRatio { get; init; }

    public string? BurstPromotionReason { get; init; }

    public required string? WavPath { get; init; }

    public required string? JsonPath { get; init; }

    public required string? FramesJsonlPath { get; init; }
}

public sealed record InterruptionCaptureFrameDiagnostic
{
    public required int FrameIndex { get; init; }

    public required long SequenceNumber { get; init; }

    public required DateTimeOffset CapturedAtUtc { get; init; }

    public required DateTimeOffset ProcessedAtUtc { get; init; }

    public required DateTimeOffset TimestampUtc { get; init; }

    public required int RelativeMs { get; init; }

    public required int CaptureRelativeMs { get; init; }

    public required int ProcessingLagMs { get; init; }

    public required int DurationMs { get; init; }

    public required int QueueDepth { get; init; }

    public required double RawEnergy { get; init; }

    public required double AecEnergy { get; init; }

    public required double VadConfidence { get; init; }

    public required bool VadSaysSpeech { get; init; }

    public required bool ComfortDuckingActive { get; init; }

    public required bool ComfortDuckingWouldAllow { get; init; }

    public required string SelfSpeechDecision { get; init; }

    public required string SelfSpeechReason { get; init; }

    public required bool CaptureIsSpeechFrame { get; init; }

    public required int LastSpeechAgeMs { get; init; }

    public required bool AppendedToCapture { get; init; }

    public required bool AppendedToContinuousRecorder { get; init; }

    public required bool ProcessedByAnalyzer { get; init; }

    public required int EndpointSilenceMs { get; init; }

    public string AcousticCaptureMode { get; init; } = "";

    public bool IdleRawMicPrimary { get; init; }

    public bool RawSpeechActive { get; init; }

    public bool AecSpeechActive { get; init; }

    public bool AecOnlyEnergyIgnored { get; init; }

    public double RawNoiseFloor { get; init; }

    public double AecNoiseFloor { get; init; }

    public bool AssistantPlaybackActive { get; init; }

    public double PlaybackReferenceRms { get; init; }

    public double? PlaybackReferenceAgeMs { get; init; }

    public int RequiredEndpointSilenceMs { get; init; }
}

public sealed record BargeInDecision
{
    public required bool Accepted { get; init; }

    public required BargeInAction Action { get; init; }

    public required InterruptionClassificationResult Classification { get; init; }

    public required string Reason { get; init; }
}
