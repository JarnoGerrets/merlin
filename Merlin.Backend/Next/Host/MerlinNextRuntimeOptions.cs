namespace Merlin.Backend.Next.Host;

public sealed class MerlinNextRuntimeOptions
{
    public const string SectionName = "MerlinNext";

    public bool Enabled { get; set; }

    public MerlinNextRuntimeMode Mode { get; set; } = MerlinNextRuntimeMode.Legacy;

    public bool ShadowEnabled { get; set; }

    public List<string> HandledCapabilities { get; set; } = [];
}
