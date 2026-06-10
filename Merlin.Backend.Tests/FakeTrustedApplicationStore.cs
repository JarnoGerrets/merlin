using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class FakeTrustedApplicationStore : ITrustedApplicationStore
{
    private readonly List<TrustedApplicationMapping> _mappings = [];

    public IReadOnlyCollection<TrustedApplicationMapping> GetAll()
    {
        return _mappings.ToArray();
    }

    public TrustedApplicationMapping? FindByAlias(string alias)
    {
        return _mappings.FirstOrDefault(mapping =>
            string.Equals(mapping.Alias, alias, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveMapping(string alias, ApplicationCandidate candidate)
    {
        _mappings.RemoveAll(mapping =>
            string.Equals(mapping.Alias, alias, StringComparison.OrdinalIgnoreCase));

        _mappings.Add(new TrustedApplicationMapping
        {
            Alias = alias,
            DisplayName = candidate.DisplayName,
            ExecutablePath = candidate.ExecutablePath,
            Source = candidate.Source,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUsedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
