namespace Merlin.Backend.Models;

public sealed class VoiceTranscriptionResponse
{
    public string Text { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;

    public double Duration { get; init; }
}
