namespace Merlin.Backend.Configuration;

public sealed class VoiceOptions
{
    public string PythonExecutable { get; set; } = "python";

    public string[] PythonArguments { get; set; } = [];

    public string WhisperModelSize { get; set; } = "medium.en";

    public string WhisperDevice { get; set; } = "cuda";

    public string WhisperComputeType { get; set; } = "int8_float16";

    public string WhisperLanguage { get; set; } = "en";

    public int WhisperBeamSize { get; set; } = 5;

    public int WhisperVadMinSilenceDurationMs { get; set; } = 600;

    public string WhisperInitialPrompt { get; set; } =
        "Merlin voice commands may include app names, website domains, browser mappings, terminal.nl, facebook.com, github.com, vscode, Visual Studio Code, PowerShell, Chrome, dot com, dot nl, dot co dot uk.";

    public int ProcessTimeoutSeconds { get; set; } = 120;
}
