using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ITrustedApplicationStore
{
    IReadOnlyCollection<TrustedApplicationMapping> GetAll();

    TrustedApplicationMapping? FindByAlias(string alias);

    void SaveMapping(string alias, ApplicationCandidate candidate);
}
