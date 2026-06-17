namespace Merlin.Backend.Services;

public sealed class ChatterboxSynthesisResult
{
    public int SampleRate { get; init; }

    public int Channels { get; init; } = 1;

    public string Format { get; init; } = "s16le";

    public byte[] Audio { get; init; } = [];

    public double DurationSeconds { get; init; }

    public double GenerationMs { get; init; }

    public double ConditioningMs { get; init; }
}
