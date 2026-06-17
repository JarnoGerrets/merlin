namespace Merlin.Backend.Configuration;

public sealed class TtsOptions
{
    public string Provider { get; set; } = "chatterbox";

    public string FallbackProvider { get; set; } = "piper";

    public string ChatterboxDevice { get; set; } = "cuda";

    public string ChatterboxReferenceVoicePath { get; set; } = Path.Combine("VoiceReference", "Reference.wav");

    public string ChatterboxCacheDir { get; set; } = Path.Combine("VoiceCache", "Chatterbox");

    public string ChatterboxModel { get; set; } = "turbo";

    public double ChatterboxExaggeration { get; set; } = 0.65;

    public double ChatterboxCfgWeight { get; set; } = 0.25;

    public double ChatterboxTemperature { get; set; } = 0.65;

    public double ChatterboxRepetitionPenalty { get; set; } = 1.15;

    public double ChatterboxTopP { get; set; } = 0.9;

    public double ChatterboxMinP { get; set; } = 0.05;

    public bool ChatterboxKeepWarm { get; set; } = true;

    public int ChatterboxMaxTextCharsPerChunk { get; set; } = 350;

    public bool ChatterboxEnableInteractiveChunking { get; set; } = true;

    public int ChatterboxFirstChunkTargetChars { get; set; } = 70;

    public int ChatterboxFirstChunkMaxChars { get; set; } = 120;

    public int ChatterboxNextChunkTargetChars { get; set; } = 95;

    public int ChatterboxNextChunkMaxChars { get; set; } = 140;

    public bool ChatterboxEnablePhraseCache { get; set; } = true;

    public int ChatterboxCircuitBreakerFailures { get; set; } = 3;

    public int ChatterboxCircuitBreakerCooldownSeconds { get; set; } = 60;

    public string ChatterboxPythonExecutable { get; set; } = "python";

    public string ChatterboxWorkerScriptPath { get; set; } = Path.Combine("VoiceScripts", "chatterbox_worker.py");
}
