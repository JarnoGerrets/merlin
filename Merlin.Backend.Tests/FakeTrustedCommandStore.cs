using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class FakeTrustedCommandStore : ITrustedCommandStore
{
    private readonly List<TrustedCommandMapping> _mappings = [];

    public IReadOnlyCollection<TrustedCommandMapping> GetAll()
    {
        return _mappings.ToArray();
    }

    public TrustedCommandMapping? FindByCommand(string command)
    {
        var normalizedCommand = TrustedCommandStore.NormalizeCommand(command);
        var mapping = _mappings.FirstOrDefault(item =>
            string.Equals(item.NormalizedOriginalCommand, normalizedCommand, StringComparison.OrdinalIgnoreCase));

        if (mapping is null)
        {
            return null;
        }

        var updated = new TrustedCommandMapping
        {
            OriginalCommand = mapping.OriginalCommand,
            NormalizedOriginalCommand = mapping.NormalizedOriginalCommand,
            Intent = mapping.Intent,
            NormalizedCommand = mapping.NormalizedCommand,
            ToolName = mapping.ToolName,
            Target = mapping.Target,
            DisplayName = mapping.DisplayName,
            CreatedAtUtc = mapping.CreatedAtUtc,
            LastUsedAtUtc = DateTimeOffset.UtcNow,
            UseCount = mapping.UseCount + 1
        };
        _mappings.Remove(mapping);
        _mappings.Add(updated);
        return updated;
    }

    public void SaveMapping(TrustedCommandMapping mapping)
    {
        var normalizedCommand = TrustedCommandStore.NormalizeCommand(mapping.OriginalCommand);
        _mappings.RemoveAll(item =>
            string.Equals(item.NormalizedOriginalCommand, normalizedCommand, StringComparison.OrdinalIgnoreCase));
        _mappings.Add(new TrustedCommandMapping
        {
            OriginalCommand = mapping.OriginalCommand,
            NormalizedOriginalCommand = normalizedCommand,
            Intent = mapping.Intent,
            NormalizedCommand = mapping.NormalizedCommand,
            ToolName = mapping.ToolName,
            Target = mapping.Target,
            DisplayName = mapping.DisplayName,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUsedAtUtc = DateTimeOffset.UtcNow,
            UseCount = Math.Max(1, mapping.UseCount)
        });
    }
}
