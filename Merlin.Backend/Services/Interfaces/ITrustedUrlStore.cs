using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ITrustedUrlStore
{
    IReadOnlyCollection<TrustedUrlMapping> GetAll();

    TrustedUrlMapping? FindByAlias(string alias);

    void SaveMapping(string alias, string url, string displayName);

    TrustedUrlMapping? UpdateMapping(string alias, string url, string displayName);

    bool DeleteMapping(string alias);
}
