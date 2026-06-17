using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class FakeTrustedUrlStore : ITrustedUrlStore
{
    private readonly List<TrustedUrlMapping> _mappings = [];

    public IReadOnlyCollection<TrustedUrlMapping> GetAll()
    {
        return _mappings.ToArray();
    }

    public TrustedUrlMapping? FindByAlias(string alias)
    {
        var normalizedAlias = TrustedUrlStore.NormalizeAlias(alias);
        return _mappings.FirstOrDefault(item =>
            string.Equals(TrustedUrlStore.NormalizeAlias(item.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveMapping(string alias, string url, string displayName)
    {
        var normalizedAlias = TrustedUrlStore.NormalizeAlias(alias);
        _mappings.RemoveAll(item =>
            string.Equals(TrustedUrlStore.NormalizeAlias(item.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));
        _mappings.Add(new TrustedUrlMapping
        {
            Alias = normalizedAlias,
            Url = url,
            DisplayName = displayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUsedAtUtc = DateTimeOffset.UtcNow,
            UseCount = 1
        });
    }

    public TrustedUrlMapping? UpdateMapping(string alias, string url, string displayName)
    {
        var existing = FindByAlias(alias);
        if (existing is null)
        {
            return null;
        }

        _mappings.Remove(existing);
        var updated = new TrustedUrlMapping
        {
            Alias = existing.Alias,
            Url = url,
            DisplayName = displayName,
            CreatedAtUtc = existing.CreatedAtUtc,
            LastUsedAtUtc = DateTimeOffset.UtcNow,
            UseCount = existing.UseCount
        };
        _mappings.Add(updated);
        return updated;
    }

    public bool DeleteMapping(string alias)
    {
        var normalizedAlias = TrustedUrlStore.NormalizeAlias(alias);
        return _mappings.RemoveAll(item =>
            string.Equals(TrustedUrlStore.NormalizeAlias(item.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase)) > 0;
    }
}
