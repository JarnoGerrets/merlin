namespace Merlin.Backend.Configuration;

public sealed class PiperOptions
{
    public string ExecutablePath { get; set; } = Path.Combine(
        "..",
        ".tmp",
        "piper",
        "piper",
        "piper.exe");

    public string ModelPath { get; set; } = Path.Combine(
        "..",
        ".tmp",
        "piper",
        "voices",
        "en_GB-northern_english_male-medium.onnx");

    public string ConfigPath { get; set; } = Path.Combine(
        "..",
        ".tmp",
        "piper",
        "voices",
        "en_GB-northern_english_male-medium.onnx.json");

    public int SampleRate { get; set; } = 22050;

    public int Channels { get; set; } = 1;

    public string Format { get; set; } = "s16le";

    public double SentenceSilenceSeconds { get; set; } = 0.05;

    public int EndSilenceMs { get; set; } = 180;
}
