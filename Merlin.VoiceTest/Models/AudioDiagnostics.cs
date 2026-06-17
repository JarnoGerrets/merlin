namespace Merlin.VoiceTest.Models;

public sealed class AudioDiagnostics
{
    public double DurationMs { get; set; }
    public int SampleRate { get; set; }
    public int ChannelCount { get; set; }
    public double RmsLevel { get; set; }
    public double PeakLevel { get; set; }
    public bool ClippingDetected { get; set; }
    public double? SilenceBeforeSpeechMs { get; set; }
    public double? SilenceAfterSpeechMs { get; set; }
    public double? SpeechDurationMs { get; set; }
    public bool SignalTooQuiet { get; set; }
    public bool SignalTooLoud { get; set; }
    public bool PossibleClipping { get; set; }
    public string AudioFilePath { get; set; } = string.Empty;
    public long WavFileSize { get; set; }
    public double TranscriptionLatencyMs { get; set; }
    public double TotalAttemptMs { get; set; }
    public double? VadSpeechStartMs { get; set; }
    public double? VadSpeechEndMs { get; set; }
    public bool VadTriggered { get; set; }
    public string VadReason { get; set; } = string.Empty;
    public int? EndSilenceMs { get; set; }
}
