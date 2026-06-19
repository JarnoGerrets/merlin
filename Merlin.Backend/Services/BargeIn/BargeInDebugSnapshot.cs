namespace Merlin.Backend.Services.BargeIn;

public sealed record BargeInDebugSnapshot
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public bool? AssistantWasSpeaking { get; init; }

    public string? CaptureSource { get; init; }

    public string? BargeInState { get; init; }

    public double? MicRms { get; init; }

    public double? MicPeak { get; init; }

    public double? PlaybackReferenceRms { get; init; }

    public double? PlaybackEnergy { get; init; }

    public double? EstimatedEchoRms { get; init; }

    public double? MicToExpectedEchoRatio { get; init; }

    public double? UserDominanceScore { get; init; }

    public double? VadConfidence { get; init; }

    public bool? VadIsSpeech { get; init; }

    public double? CorrelationScore { get; init; }

    public double? BestCorrelationDelayMs { get; init; }

    public string? SelfSpeechGateDecision { get; init; }

    public string? SelfSpeechGateReason { get; init; }

    public string? CapturedWindowSelfPlaybackDecision { get; init; }

    public double? CapturedWindowBestCorrelation { get; init; }

    public string? SttAudioSource { get; init; }

    public bool? SttAudioIsAecProcessed { get; init; }

    public string? FinalBargeInDecision { get; init; }

    public double? MicRmsPercent { get; init; }

    public double? MicPeakPercent { get; init; }

    public double? PlaybackReferencePercent { get; init; }

    public double? ExpectedEchoPercent { get; init; }

    public double? VadPercent { get; init; }

    public double? CorrelationPercent { get; init; }

    public double? UserDominancePercent { get; init; }
}
