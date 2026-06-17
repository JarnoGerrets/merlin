namespace Merlin.VoiceTest.Models;

public sealed class VoiceTestOptions
{
    public string Phrases { get; set; } = "default";
    public int? MaxPhrases { get; set; }
    public string Mode { get; set; } = "fixed";
    public int RecordingSeconds { get; set; } = 5;
    public string Output { get; set; } = "Reports";
    public bool KeepAudio { get; set; } = true;
    public string? Device { get; set; }
    public string DeviceType { get; set; } = "cuda";
    public string Model { get; set; } = "medium.en";
    public string ComputeType { get; set; } = "int8_float16";
    public string Language { get; set; } = "en";
    public string Task { get; set; } = "transcribe";
    public int BeamSize { get; set; } = 5;
    public double Temperature { get; set; } = 0;
    public string PythonExecutable { get; set; } = "python";
    public string[] PythonArguments { get; set; } = [];
    public string WhisperScriptPath { get; set; } = "..\\Merlin.Backend\\VoiceScripts\\transcribe_faster_whisper.py";
    public string InitialPrompt { get; set; } =
        "Merlin voice commands may include Whisper, beam search, SQLite, DeepInfra, Chatterbox, Codex CLI, CUDA, AppData, medium.en, beam=5, speech to text, and local memory.";
    public int ProcessTimeoutSeconds { get; set; } = 120;
    public int InputSampleRate { get; set; } = 16000;
    public int TargetSampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public int PreRollMs { get; set; } = 300;
    public int MinSpeechMs { get; set; } = 250;
    public int EndSilenceMs { get; set; } = 700;
    public int MaxUtteranceMs { get; set; } = 9000;
    public double VadStartRmsThreshold { get; set; } = 0.015;
    public double VadEndRmsThreshold { get; set; } = 0.010;
}
