namespace Merlin.Backend.Configuration;

public sealed class VoiceInputOptions
{
    public string Owner { get; set; } = "backend";

    public bool BackendVoiceInputEnabled { get; set; } = true;

    public bool FrontendVoiceInputEnabled { get; set; } = false;

    public string BackendIdleVoiceInteractionSource { get; set; } = "backend_idle_voice";

    public bool IsBackendOwnedMode =>
        string.Equals(Owner, "backend", StringComparison.OrdinalIgnoreCase)
        && BackendVoiceInputEnabled
        && !FrontendVoiceInputEnabled;
}
