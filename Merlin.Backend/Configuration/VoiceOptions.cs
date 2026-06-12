namespace Merlin.Backend.Configuration;

public sealed class VoiceOptions
{
    public string PythonExecutable { get; set; } = "python";

    public string[] PythonArguments { get; set; } = [];

    public string WhisperModelSize { get; set; } = "base.en";

    public string WhisperDevice { get; set; } = "cpu";

    public string WhisperComputeType { get; set; } = "int8";

    public string WhisperLanguage { get; set; } = "en";

    public int WhisperBeamSize { get; set; } = 1;

    public int WhisperVadMinSilenceDurationMs { get; set; } = 250;

    public int ProcessTimeoutSeconds { get; set; } = 120;
}
