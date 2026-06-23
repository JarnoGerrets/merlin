namespace Merlin.Backend.Configuration;

public sealed class TrustedRegistryOptions
{
    public bool Enabled { get; set; } = true;

    public string? DatabasePath { get; set; }

    public bool ImportLegacyJsonOnStartup { get; set; } = true;

    public bool KeepLegacyJsonAsBackup { get; set; } = true;

    public bool WriteThroughLegacyJson { get; set; } = false;

    public bool EnableTrustedCommandParser { get; set; } = false;
}
