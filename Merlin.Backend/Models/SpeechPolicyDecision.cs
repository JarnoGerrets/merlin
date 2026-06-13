namespace Merlin.Backend.Models;

public sealed class SpeechPolicyDecision
{
    public bool ShouldSpeak { get; init; }

    public bool ShouldQueue { get; init; } = true;

    public bool ShouldInterrupt { get; init; }

    public string? SpeechTextOverride { get; init; }

    public bool UsedLegacySpeakResponseFallback { get; init; }
}
