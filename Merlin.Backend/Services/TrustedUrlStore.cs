using System.Text.Json;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class TrustedUrlStore : ITrustedUrlStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<TrustedUrlStore> _logger;
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private List<TrustedUrlMapping> _urls = [];

    public TrustedUrlStore(ILogger<TrustedUrlStore> logger)
        : this(GetDefaultStorePath(), logger)
    {
    }

    internal TrustedUrlStore(string storePath, ILogger<TrustedUrlStore> logger)
    {
        _storePath = storePath;
        _logger = logger;
        Load();
    }

    public IReadOnlyCollection<TrustedUrlMapping> GetAll()
    {
        lock (_syncRoot)
        {
            return _urls.ToArray();
        }
    }

    public TrustedUrlMapping? FindByAlias(string alias)
    {
        lock (_syncRoot)
        {
            var normalizedAlias = NormalizeAlias(alias);
            var mapping = _urls.FirstOrDefault(url =>
                string.Equals(NormalizeAlias(url.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                return null;
            }

            var updated = new TrustedUrlMapping
            {
                Alias = mapping.Alias,
                Url = mapping.Url,
                DisplayName = mapping.DisplayName,
                CreatedAtUtc = mapping.CreatedAtUtc,
                LastUsedAtUtc = DateTimeOffset.UtcNow,
                UseCount = mapping.UseCount + 1
            };
            ReplaceMapping(updated);
            Save();
            return updated;
        }
    }

    public void SaveMapping(string alias, string url, string displayName)
    {
        lock (_syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var normalizedAlias = NormalizeAlias(alias);
            var existing = _urls.FirstOrDefault(item =>
                string.Equals(NormalizeAlias(item.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

            var mapping = new TrustedUrlMapping
            {
                Alias = normalizedAlias,
                Url = url,
                DisplayName = displayName,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                LastUsedAtUtc = now,
                UseCount = Math.Max(1, existing?.UseCount ?? 1)
            };

            ReplaceMapping(mapping);
            Save();
        }
    }

    public TrustedUrlMapping? UpdateMapping(string alias, string url, string displayName)
    {
        lock (_syncRoot)
        {
            var normalizedAlias = NormalizeAlias(alias);
            var existing = _urls.FirstOrDefault(item =>
                string.Equals(NormalizeAlias(item.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                return null;
            }

            var mapping = new TrustedUrlMapping
            {
                Alias = existing.Alias,
                Url = url,
                DisplayName = displayName,
                CreatedAtUtc = existing.CreatedAtUtc,
                LastUsedAtUtc = DateTimeOffset.UtcNow,
                UseCount = existing.UseCount
            };

            ReplaceMapping(mapping);
            Save();
            return mapping;
        }
    }

    public bool DeleteMapping(string alias)
    {
        lock (_syncRoot)
        {
            var normalizedAlias = NormalizeAlias(alias);
            var removed = _urls.RemoveAll(item =>
                string.Equals(NormalizeAlias(item.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    internal static string NormalizeAlias(string alias)
    {
        var normalized = alias.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var suffix in new[]
        {
            " to the browser",
            " in the browser",
            " from the browser",
            " as a website",
            " website",
            " browser mapping",
            " mapping"
        })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^suffix.Length].Trim();
                break;
            }
        }

        return string.Join(
            ' ',
            normalized
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private void Load()
    {
        lock (_syncRoot)
        {
            try
            {
                var directory = Path.GetDirectoryName(_storePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(_storePath))
                {
                    _urls = [];
                    Save();
                    return;
                }

                var json = File.ReadAllText(_storePath);
                var document = JsonSerializer.Deserialize<TrustedUrlDocument>(json, JsonSerializerOptions);
                _urls = document?.Urls?.ToList() ?? [];
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to load trusted URL store. Starting with an empty store.");
                _urls = [];
            }
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new TrustedUrlDocument
        {
            Urls = _urls
                .OrderByDescending(item => item.UseCount)
                .ThenBy(item => item.Alias, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(document, JsonSerializerOptions);
        var tempPath = $"{_storePath}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, overwrite: true);
    }

    private void ReplaceMapping(TrustedUrlMapping mapping)
    {
        _urls.RemoveAll(item =>
            string.Equals(NormalizeAlias(item.Alias), NormalizeAlias(mapping.Alias), StringComparison.OrdinalIgnoreCase));
        _urls.Add(mapping);
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Merlin", "trusted-browser-mappings.json");
    }

    private sealed class TrustedUrlDocument
    {
        public IReadOnlyCollection<TrustedUrlMapping> Urls { get; init; } = [];
    }
}
