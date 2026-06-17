namespace Merlin.VoiceTest.Models;

public sealed class TranscriptionResult
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public double AudioDurationSeconds { get; set; }
    public double LatencyMs { get; set; }
    public string CommandLine { get; set; } = string.Empty;
    public string StandardErrorTail { get; set; } = string.Empty;
    public bool Succeeded { get; set; }
    public string Error { get; set; } = string.Empty;
}
