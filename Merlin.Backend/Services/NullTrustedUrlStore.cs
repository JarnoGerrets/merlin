using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

internal sealed class NullTrustedUrlStore : ITrustedUrlStore
{
    public static NullTrustedUrlStore Instance { get; } = new();

    private NullTrustedUrlStore()
    {
    }

    public IReadOnlyCollection<TrustedUrlMapping> GetAll()
    {
        return [];
    }

    public TrustedUrlMapping? FindByAlias(string alias)
    {
        return null;
    }

    public void SaveMapping(string alias, string url, string displayName)
    {
    }

    public TrustedUrlMapping? UpdateMapping(string alias, string url, string displayName)
    {
        return null;
    }

    public bool DeleteMapping(string alias)
    {
        return false;
    }
}
