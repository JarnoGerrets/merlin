namespace Merlin.Backend.Models;

public sealed class VoiceSynthesisStreamMetadata
{
    public int SampleRate { get; init; }

    public int Channels { get; init; }

    public string Format { get; init; } = "s16le";
}
