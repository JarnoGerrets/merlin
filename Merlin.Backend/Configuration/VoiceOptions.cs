namespace Merlin.Backend.Configuration;

public sealed class VoiceOptions
{
    public string PythonExecutable { get; set; } = "python";

    public string[] PythonArguments { get; set; } = [];

    public string WhisperModelSize { get; set; } = "small.en";

    public string WhisperDevice { get; set; } = "cpu";

    public string WhisperComputeType { get; set; } = "int8";

    public string WhisperLanguage { get; set; } = "en";

    public int WhisperBeamSize { get; set; } = 5;

    public int WhisperVadMinSilenceDurationMs { get; set; } = 450;

    public string KokoroVoice { get; set; } = "bm_george";

    public string KokoroLanguageCode { get; set; } = "b";

    public float KokoroSpeed { get; set; } = 1.0f;

    public int ProcessTimeoutSeconds { get; set; } = 120;

    public bool WarmupOnStartup { get; set; } = true;
}
