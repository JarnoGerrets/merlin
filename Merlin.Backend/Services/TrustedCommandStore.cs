using System.Text.Json;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class TrustedCommandStore : ITrustedCommandStore
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<TrustedCommandStore> _logger;
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private List<TrustedCommandMapping> _commands = [];

    public TrustedCommandStore(ILogger<TrustedCommandStore> logger)
        : this(GetDefaultStorePath(), logger)
    {
    }

    internal TrustedCommandStore(string storePath, ILogger<TrustedCommandStore> logger)
    {
        _storePath = storePath;
        _logger = logger;
        Load();
    }

    public IReadOnlyCollection<TrustedCommandMapping> GetAll()
    {
        lock (_syncRoot)
        {
            return _commands.ToArray();
        }
    }

    public TrustedCommandMapping? FindByCommand(string command)
    {
        lock (_syncRoot)
        {
            var normalizedCommand = NormalizeCommand(command);
            var mapping = _commands.FirstOrDefault(item =>
                string.Equals(item.NormalizedOriginalCommand, normalizedCommand, StringComparison.OrdinalIgnoreCase));

            if (mapping is null)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
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
                LastUsedAtUtc = now,
                UseCount = mapping.UseCount + 1
            };

            ReplaceMapping(updated);
            Save();
            return updated;
        }
    }

    public void SaveMapping(TrustedCommandMapping mapping)
    {
        lock (_syncRoot)
        {
            var now = DateTimeOffset.UtcNow;
            var normalizedOriginalCommand = NormalizeCommand(mapping.OriginalCommand);
            var existing = _commands.FirstOrDefault(item =>
                string.Equals(item.NormalizedOriginalCommand, normalizedOriginalCommand, StringComparison.OrdinalIgnoreCase));

            var updated = new TrustedCommandMapping
            {
                OriginalCommand = mapping.OriginalCommand.Trim(),
                NormalizedOriginalCommand = normalizedOriginalCommand,
                Intent = mapping.Intent,
                NormalizedCommand = mapping.NormalizedCommand.Trim(),
                ToolName = mapping.ToolName,
                Target = mapping.Target,
                DisplayName = mapping.DisplayName,
                CreatedAtUtc = existing?.CreatedAtUtc ?? now,
                LastUsedAtUtc = now,
                UseCount = Math.Max(1, existing?.UseCount ?? mapping.UseCount)
            };

            ReplaceMapping(updated);
            Save();
        }
    }

    internal static string NormalizeCommand(string command)
    {
        var trimmed = command.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
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
                    _commands = [];
                    Save();
                    return;
                }

                var json = File.ReadAllText(_storePath);
                var document = JsonSerializer.Deserialize<TrustedCommandDocument>(json, JsonSerializerOptions);
                _commands = document?.Commands?.ToList() ?? [];
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to load trusted command store. Starting with an empty store.");
                _commands = [];
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

        var document = new TrustedCommandDocument
        {
            Commands = _commands
                .OrderByDescending(command => command.UseCount)
                .ThenBy(command => command.NormalizedOriginalCommand, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(document, JsonSerializerOptions);
        var tempPath = $"{_storePath}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, overwrite: true);
    }

    private void ReplaceMapping(TrustedCommandMapping mapping)
    {
        _commands.RemoveAll(command =>
            string.Equals(
                command.NormalizedOriginalCommand,
                mapping.NormalizedOriginalCommand,
                StringComparison.OrdinalIgnoreCase));
        _commands.Add(mapping);
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Merlin", "trusted-commands.json");
    }

    private sealed class TrustedCommandDocument
    {
        public IReadOnlyCollection<TrustedCommandMapping> Commands { get; init; } = [];
    }
}
