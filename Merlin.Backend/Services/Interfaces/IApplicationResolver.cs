using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IApplicationResolver
{
    string LastResolutionStatus { get; }

    Task<ApplicationResolutionResult> ResolveAsync(
        string applicationName,
        CancellationToken cancellationToken = default);
}
