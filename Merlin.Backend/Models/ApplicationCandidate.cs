namespace Merlin.Backend.Models;

public sealed class ApplicationCandidate
{
    public string DisplayName { get; init; } = string.Empty;

    public string ExecutablePath { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public double Confidence { get; init; }
}
