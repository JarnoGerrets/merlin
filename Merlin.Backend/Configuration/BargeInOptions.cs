namespace Merlin.Backend.Configuration;

public sealed class BargeInOptions
{
    public bool Enabled { get; set; } = false;

    public string Mode { get; set; } = "Option4C";

    public bool RequireVoiceMode { get; set; } = true;

    public bool EnableAec { get; set; } = true;

    public bool EnableVad { get; set; } = true;

    public bool EnableSpeakerDucking { get; set; } = true;

    public bool EnableGatedStt { get; set; } = true;

    public bool EnableTurnCancellation { get; set; } = true;

    public string AecProvider { get; set; } = "WebRtcApm";

    public string WindowsAecSetClientPropertiesMode { get; set; } = "NAudio";

    public bool AllowDegradedAecFallback { get; set; } = false;

    public bool RequireRealAecForBargeIn { get; set; } = true;

    public int AecFrameMs { get; set; } = 10;

    public int FrameMs { get; set; } = 10;

    public int AecSampleRate { get; set; } = 48000;

    public int AecChannels { get; set; } = 1;

    public int CaptureFrameMs { get; set; } = 10;

    public string CaptureDeviceRole { get; set; } = "Communications";

    public string RenderDeviceRole { get; set; } = "Multimedia";

    public int ContinuousMicBufferMs { get; set; } = 10000;

    public int AnalysisQueueCapacityFrames { get; set; } = 500;

    public int VadMinSpeechMs { get; set; } = 250;

    public int VadTriggerSpeechMs { get; set; } = 350;

    public int VadEndSilenceMs { get; set; } = 450;

    public double VadEnergyThreshold { get; set; } = 0.015;

    public bool VadUseAdaptiveNoiseFloor { get; set; } = true;

    public bool CaptureContinuationUseVad { get; set; } = true;

    public double CaptureContinuationRawEnergyThreshold { get; set; } = 0.025;

    public double CaptureContinuationAecEnergyThreshold { get; set; } = 0.010;

    public int TriggerPreRollMs { get; set; } = 450;

    public int TriggerCaptureMs { get; set; } = 1500;

    public int TriggerPostSpeechWaitMs { get; set; } = 1200;

    public int TriggerMaxCaptureMs { get; set; } = 10000;

    public int DuckingVolumePercent { get; set; } = 20;

    public int DuckingFadeMs { get; set; } = 80;

    public int DuckingRestoreMs { get; set; } = 150;

    public int DuckingSpeechHangoverMs { get; set; } = 350;

    public ComfortDuckingOptions ComfortDucking { get; set; } = new();

    public FastNearEndDuckingOptions FastNearEndDucking { get; set; } = new();

    public BurstCapturePromotionOptions BurstCapturePromotion { get; set; } = new();

    public bool EnablePlaybackLeakageDuckingGuard { get; set; } = true;

    public double PlaybackLeakageReferenceEnergyThreshold { get; set; } = 0.03;

    public double PlaybackLeakageMinVadConfidence { get; set; } = 0.75;

    public double PlaybackLeakageMinEchoReducedEnergyMultiplier { get; set; } = 2.5;

    public double PlaybackLeakageMinNearEndToReferenceRatio { get; set; } = 0.55;

    public SelfSpeechSuppressionOptions SelfSpeechSuppression { get; set; } = new();

    public bool EnableCapturedWindowSelfPlaybackCheck { get; set; } = true;

    public double CapturedWindowSelfPlaybackCorrelationThreshold { get; set; } = 0.82;

    public double CapturedWindowSelfPlaybackLikelyUserThreshold { get; set; } = 0.35;

    public double CapturedWindowSelfPlaybackMinReferenceEnergy { get; set; } = 0.008;

    public double CapturedWindowSelfPlaybackMinCaptureEnergy { get; set; } = 0.008;

    public int CapturedWindowSelfPlaybackMaxSlices { get; set; } = 4;

    public int CapturedWindowSelfPlaybackSliceMs { get; set; } = 250;

    public int CapturedWindowSelfPlaybackDelayMsMin { get; set; } = 0;

    public int CapturedWindowSelfPlaybackDelayMsMax { get; set; } = 250;

    public int CapturedWindowSelfPlaybackDelayStepMs { get; set; } = 10;

    public int CapturedWindowSelfPlaybackRecentPlaybackMs { get; set; } = 750;

    public double CapturedWindowSelfPlaybackStrongUserEnergyRatio { get; set; } = 2.25;

    public bool EnableFastHardStopCapture { get; set; } = true;

    public bool RequireSustainedUserSpeechScoreDuringPlayback { get; set; } = false;

    public bool PausePlaybackOnSustainedUserSpeechScore { get; set; } = false;

    public bool PausePlaybackOnRollingUserSpeechEvidence { get; set; } = false;

    public double SustainedUserSpeechScoreThreshold { get; set; } = 0.90;

    public int SustainedUserSpeechScoreDurationMs { get; set; } = 250;

    public int FloorYieldEvidenceWindowMs { get; set; } = 350;

    public double FloorYieldHighScoreThreshold { get; set; } = 0.90;

    public int FloorYieldRequiredHighScoreMs { get; set; } = 180;

    public double FloorYieldAverageScoreThreshold { get; set; } = 0.65;

    public bool FloorYieldRequireRecentHighFrame { get; set; } = true;

    public int FloorYieldRecentHighFrameWindowMs { get; set; } = 80;

    public bool RequireWakePrefixForStopDuringPlayback { get; set; } = false;

    public string StopWakePrefix { get; set; } = "merlin";

    public int FastHardStopMinSpeechMs { get; set; } = 120;

    public int FastHardStopCaptureWindowMs { get; set; } = 900;

    public int FastHardStopPostSpeechPaddingMs { get; set; } = 150;

    public double FastHardStopMinConfidence { get; set; } = 0.65;

    public string GatedSttModel { get; set; } = "medium.en";

    public string GatedSttDevice { get; set; } = "cuda";

    public int GatedSttBeamSize { get; set; } = 3;

    public double GatedSttTemperature { get; set; } = 0;

    public int GatedSttMaxAudioMs { get; set; } = 10000;

    public bool RequireWakeWordForFirstVersion { get; set; } = true;

    public bool AllowNaturalSoftBargeInWhenAecVerified { get; set; } = false;

    public bool PauseInsteadOfCancelOnSpeech { get; set; } = true;

    public bool BackchannelResumeEnabled { get; set; } = true;

    public bool ClarificationResumeEnabled { get; set; } = false;

    public string[] WakeWords { get; set; } = ["merlin"];

    public string[] HardStopPhrases { get; set; } =
    [
        "stop",
        "stop talking",
        "cancel",
        "cancel that",
        "abort",
        "abort that",
        "shut up",
        "quiet",
        "be quiet",
        "enough",
        "that's enough",
        "never mind",
        "nevermind",
        "wait",
        "hold on",
        "pause",
        "pause that",
        "no stop"
    ];

    public string[] PausePhrases { get; set; } = ["wait", "pause", "hold on", "one second"];

    public string[] CorrectionPhrases { get; set; } = ["no", "actually", "no i mean", "i mean", "not", "not that", "that's wrong", "you misunderstood", "correct that"];

    public string[] ClarificationQuestionPrefixes { get; set; } = ["what", "why", "how", "which", "where"];

    public double MinClassifierConfidence { get; set; } = 0.75;

    public double MinHardStopConfidence { get; set; } = 0.65;

    public int MaxBargeInsPerAssistantTurn { get; set; } = 3;

    public int InterruptionCaptureMaxMs { get; set; } = 10000;

    public bool LogAudioDiagnostics { get; set; } = true;

    public bool SaveDebugAudio { get; set; } = false;

    public string DebugAudioPath { get; set; } = "Logs/InterruptionCaptures";
}

public sealed class FastNearEndDuckingOptions
{
    public bool Enabled { get; set; } = true;

    public bool RequireAssistantPlayback { get; set; } = true;

    public int MinSpeechMs { get; set; } = 50;

    public double MinVadConfidence { get; set; } = 0.4;

    public double MinEnergyRatioOverNoise { get; set; } = 4.0;

    public double MinAbsoluteEnergy { get; set; } = 0.008;

    public int HangoverMs { get; set; } = 500;

    public bool UseSelfSpeechGate { get; set; } = true;

    public string InputReason { get; set; } = "fast_near_end_ducking";
}

public sealed class ComfortDuckingOptions
{
    public bool Enabled { get; set; } = true;

    public bool AllowUncertain { get; set; } = true;

    public bool SuppressOnlyStrongSelfEcho { get; set; } = true;

    public int MinSpeechMs { get; set; } = 50;

    public int HangoverMs { get; set; } = 400;

    public string InputReason { get; set; } = "comfort_ducking";
}

public sealed class BurstCapturePromotionOptions
{
    public bool Enabled { get; set; } = true;

    public int MinBurstMs { get; set; } = 350;

    public int MaxWindowMs { get; set; } = 600;

    public int MinCandidateFrames { get; set; } = 8;

    public double MinVadSpeechFrameRatio { get; set; } = 0.35;

    public bool AllowUncertainPromotion { get; set; } = true;

    public double StrongSelfEchoVetoRatio { get; set; } = 0.60;

    public int StrongSelfEchoVetoMinFrames { get; set; } = 5;

    public bool RequireAssistantPlayback { get; set; } = true;
}

public sealed class SelfSpeechSuppressionOptions
{
    public bool Enabled { get; set; } = true;

    public bool SuppressDuringPlayback { get; set; } = true;

    public int PlaybackOnsetGraceMs { get; set; } = 250;

    public double EchoLeakageMultiplier { get; set; } = 0.35;

    public double EchoMargin { get; set; } = 0.02;

    public double UserSpeechRatio { get; set; } = 1.8;

    public double UserSpeechMargin { get; set; } = 0.05;

    public int RequireSustainedUserSpeechFrames { get; set; } = 2;

    public bool AllowFastHardStopOverride { get; set; } = true;

    public bool LogDecisions { get; set; } = false;

    public bool DiagnosticsFileEnabled { get; set; } = true;

    public string DiagnosticsFilePath { get; set; } = "Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl";

    public int DiagnosticsSampleEveryNFrames { get; set; } = 1;

    public bool DiagnosticsIncludeSuppressed { get; set; } = true;

    public bool DiagnosticsIncludeAllowed { get; set; } = true;

    public bool DiagnosticsIncludeUncertain { get; set; } = true;

    public string PolicyMode { get; set; } = "StrictDuringPlayback";

    public bool AllowSustainedUncertainForDucking { get; set; } = false;

    public bool AllowSustainedUncertainForCapture { get; set; } = false;

    public bool AllowSustainedUncertainForFastHardStop { get; set; } = true;

    public int FastHardStopUncertainFrames { get; set; } = 6;

    public double FastHardStopUncertainExtraMargin { get; set; } = 0.04;

    public bool CorrelationDetectionEnabled { get; set; } = true;

    public double CorrelationMinScore { get; set; } = 0.65;

    public double CorrelationSelfEchoThreshold { get; set; } = 0.70;

    public double CorrelationLikelyUserThreshold { get; set; } = 0.35;

    public int CorrelationMinDelayMs { get; set; } = 0;

    public int CorrelationMaxDelayMs { get; set; } = 250;

    public int CorrelationStepMs { get; set; } = 10;

    public double CorrelationMinReferenceEnergy { get; set; } = 0.005;

    public double CorrelationMinMicEnergy { get; set; } = 0.005;
}
