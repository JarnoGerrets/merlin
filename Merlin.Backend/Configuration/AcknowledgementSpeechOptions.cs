namespace Merlin.Backend.Configuration;

public sealed class AcknowledgementSpeechOptions
{
    public bool Enabled { get; set; } = true;

    public bool VoiceModeOnly { get; set; } = true;

    public int MinimumExpectedLatencyMs { get; set; } = 1500;

    public int FirstProgressAfterMs { get; set; } = 6000;

    public int SecondProgressAfterMs { get; set; } = 14000;

    public int LongWaitProgressAfterMs { get; set; } = 25000;

    public int MaxProgressUpdates { get; set; } = 3;

    public int PhraseCooldownSeconds { get; set; } = 600;

    public bool UseCachedAudioOnlyForAcknowledgements { get; set; } = true;
}
