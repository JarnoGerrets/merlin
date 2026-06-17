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

    public int VadMinSpeechMs { get; set; } = 250;

    public int VadTriggerSpeechMs { get; set; } = 350;

    public int VadEndSilenceMs { get; set; } = 500;

    public double VadEnergyThreshold { get; set; } = 0.015;

    public bool VadUseAdaptiveNoiseFloor { get; set; } = true;

    public int TriggerPreRollMs { get; set; } = 300;

    public int TriggerCaptureMs { get; set; } = 1500;

    public int TriggerPostSpeechWaitMs { get; set; } = 1200;

    public int TriggerMaxCaptureMs { get; set; } = 2500;

    public int DuckingVolumePercent { get; set; } = 20;

    public int DuckingFadeMs { get; set; } = 80;

    public int DuckingRestoreMs { get; set; } = 150;

    public string GatedSttModel { get; set; } = "medium.en";

    public string GatedSttDevice { get; set; } = "cuda";

    public int GatedSttBeamSize { get; set; } = 3;

    public double GatedSttTemperature { get; set; } = 0;

    public int GatedSttMaxAudioMs { get; set; } = 2500;

    public bool RequireWakeWordForFirstVersion { get; set; } = true;

    public bool AllowNaturalSoftBargeInWhenAecVerified { get; set; } = false;

    public bool PauseInsteadOfCancelOnSpeech { get; set; } = true;

    public bool BackchannelResumeEnabled { get; set; } = true;

    public bool ClarificationResumeEnabled { get; set; } = false;

    public string[] WakeWords { get; set; } = ["merlin"];

    public string[] HardStopPhrases { get; set; } = ["stop", "cancel", "shut up", "quiet", "enough", "never mind"];

    public string[] PausePhrases { get; set; } = ["wait", "pause", "hold on", "one second"];

    public string[] CorrectionPhrases { get; set; } = ["no", "actually", "no i mean", "i mean", "not", "not that", "that's wrong", "you misunderstood", "correct that"];

    public string[] ClarificationQuestionPrefixes { get; set; } = ["what", "why", "how", "which", "where"];

    public double MinClassifierConfidence { get; set; } = 0.75;

    public double MinHardStopConfidence { get; set; } = 0.65;

    public int MaxBargeInsPerAssistantTurn { get; set; } = 3;

    public int InterruptionCaptureMaxMs { get; set; } = 2500;

    public bool LogAudioDiagnostics { get; set; } = true;

    public bool SaveDebugAudio { get; set; } = false;

    public string DebugAudioPath { get; set; } = "%APPDATA%/Merlin/debug/barge-in";
}
