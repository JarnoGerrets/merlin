using System.Text.Json;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class TrustedApplicationStore : ITrustedApplicationStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<TrustedApplicationStore> _logger;
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private List<TrustedApplicationMapping> _applications = [];

    public TrustedApplicationStore(ILogger<TrustedApplicationStore> logger)
        : this(GetDefaultStorePath(), logger)
    {
    }

    internal TrustedApplicationStore(string storePath, ILogger<TrustedApplicationStore> logger)
    {
        _storePath = storePath;
        _logger = logger;
        Load();
    }

    public IReadOnlyCollection<TrustedApplicationMapping> GetAll()
    {
        lock (_syncRoot)
        {
            return _applications.ToArray();
        }
    }

    public TrustedApplicationMapping? FindByAlias(string alias)
    {
        lock (_syncRoot)
        {
            var normalizedAlias = NormalizeAlias(alias);
            var mapping = _applications.FirstOrDefault(application =>
                string.Equals(NormalizeAlias(application.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                return null;
            }

            var updated = new TrustedApplicationMapping
            {
                Alias = mapping.Alias,
                DisplayName = mapping.DisplayName,
                ExecutablePath = mapping.ExecutablePath,
                Source = mapping.Source,
                CreatedAtUtc = mapping.CreatedAtUtc,
                LastUsedAtUtc = DateTimeOffset.UtcNow
            };
            ReplaceMapping(updated);
            Save();
            return updated;
        }
    }

    public void SaveMapping(string alias, ApplicationCandidate candidate)
    {
        lock (_syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var existing = _applications.FirstOrDefault(application =>
                string.Equals(NormalizeAlias(application.Alias), NormalizeAlias(alias), StringComparison.OrdinalIgnoreCase));

            var mapping = new TrustedApplicationMapping
            {
                Alias = NormalizeAlias(alias),
                DisplayName = candidate.DisplayName,
                ExecutablePath = candidate.ExecutablePath,
                Source = candidate.Source,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                LastUsedAtUtc = now
            };

            ReplaceMapping(mapping);
            Save();
        }
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
                    _applications = [];
                    Save();
                    return;
                }

                var json = File.ReadAllText(_storePath);
                var document = JsonSerializer.Deserialize<TrustedApplicationDocument>(json, JsonSerializerOptions);
                _applications = document?.Applications?.ToList() ?? [];
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to load trusted application store. Starting with an empty store.");
                _applications = [];
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

        var document = new TrustedApplicationDocument
        {
            Applications = _applications.OrderBy(application => application.Alias).ToArray()
        };
        var json = JsonSerializer.Serialize(document, JsonSerializerOptions);
        var tempPath = $"{_storePath}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, overwrite: true);
    }

    private void ReplaceMapping(TrustedApplicationMapping mapping)
    {
        _applications.RemoveAll(application =>
            string.Equals(NormalizeAlias(application.Alias), NormalizeAlias(mapping.Alias), StringComparison.OrdinalIgnoreCase));
        _applications.Add(mapping);
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Merlin", "trusted-applications.json");
    }

    private static string NormalizeAlias(string alias)
    {
        return string.Join(
            ' ',
            alias.Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed class TrustedApplicationDocument
    {
        public IReadOnlyCollection<TrustedApplicationMapping> Applications { get; init; } = [];
    }
}
