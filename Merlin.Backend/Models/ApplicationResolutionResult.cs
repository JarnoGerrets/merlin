namespace Merlin.Backend.Models;

public sealed class ApplicationResolutionResult
{
    public bool Found { get; init; }

    public bool RequiresConfirmation { get; init; }

    public bool IsAmbiguous { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyCollection<ApplicationCandidate> Candidates { get; init; } = [];
}
