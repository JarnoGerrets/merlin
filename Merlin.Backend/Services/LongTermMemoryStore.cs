using System.Text.Json;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class LongTermMemoryStore : ILongTermMemoryStore
{
    private static readonly HashSet<string> SupportedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "preference",
        "project",
        "operational",
        "fact",
        "task"
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<LongTermMemoryStore> _logger;
    private readonly object _syncRoot = new();
    private readonly string _storePath;
    private List<MemoryRecord> _memories = [];

    public LongTermMemoryStore(ILogger<LongTermMemoryStore> logger)
        : this(GetDefaultStorePath(), logger)
    {
    }

    internal LongTermMemoryStore(string storePath, ILogger<LongTermMemoryStore> logger)
    {
        _storePath = storePath;
        _logger = logger;
        Load();
    }

    public bool IsHealthy { get; private set; } = true;

    public IReadOnlyCollection<MemoryRecord> GetAll()
    {
        lock (_syncRoot)
        {
            return _memories.Select(CloneMemory).ToArray();
        }
    }

    public IReadOnlyList<MemoryRecord> GetByCategory(string category)
    {
        lock (_syncRoot)
        {
            var normalizedCategory = NormalizeCategory(category);
            return _memories
                .Where(memory => string.Equals(memory.Category, normalizedCategory, StringComparison.OrdinalIgnoreCase))
                .OrderBy(memory => memory.Key, StringComparer.OrdinalIgnoreCase)
                .Select(CloneMemory)
                .ToArray();
        }
    }

    public IReadOnlyList<MemoryRecord> Search(string query)
    {
        lock (_syncRoot)
        {
            var queryTerms = GetSearchTerms(query);
            if (queryTerms.Length == 0)
            {
                return [];
            }

            return _memories
                .Where(memory => Matches(memory, queryTerms))
                .OrderByDescending(memory => memory.UpdatedAtUtc)
                .ThenBy(memory => memory.Key, StringComparer.OrdinalIgnoreCase)
                .Select(CloneMemory)
                .ToArray();
        }
    }

    public IReadOnlyList<MemoryRecord> GetMostRelevant(string query, int count)
    {
        return Search(query).Take(Math.Max(0, count)).ToArray();
    }

    public MemoryRecord SaveMemory(MemoryRecord memory)
    {
        lock (_syncRoot)
        {
            var normalized = NormalizeMemory(memory, existing: null);
            ReplaceMemory(normalized);
            Save();
            return CloneMemory(normalized);
        }
    }

    public MemoryRecord UpdateMemory(MemoryRecord memory)
    {
        lock (_syncRoot)
        {
            var existing = _memories.FirstOrDefault(item =>
                string.Equals(item.MemoryId, memory.MemoryId, StringComparison.OrdinalIgnoreCase));
            var normalized = NormalizeMemory(memory, existing);
            ReplaceMemory(normalized);
            Save();
            return CloneMemory(normalized);
        }
    }

    public bool DeleteMemory(string memoryId)
    {
        lock (_syncRoot)
        {
            var removed = _memories.RemoveAll(memory =>
                string.Equals(memory.MemoryId, memoryId, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                Save();
            }

            return removed;
        }
    }

    public MemoryRecord MergeMemory(MemoryRecord memory)
    {
        lock (_syncRoot)
        {
            var category = NormalizeCategory(memory.Category);
            var key = NormalizeKey(memory.Key);
            var existing = _memories.FirstOrDefault(item =>
                string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));

            var normalized = NormalizeMemory(memory, existing);
            ReplaceMemory(normalized);
            Save();
            return CloneMemory(normalized);
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
                    _memories = [];
                    Save();
                    IsHealthy = true;
                    return;
                }

                var json = File.ReadAllText(_storePath);
                var document = JsonSerializer.Deserialize<LongTermMemoryDocument>(json, JsonSerializerOptions);
                _memories = document?.Memories?.Select(CloneMemory).ToList() ?? [];
                IsHealthy = true;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to load long-term memory store. Starting with an empty store.");
                _memories = [];
                IsHealthy = false;
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

        var document = new LongTermMemoryDocument
        {
            Memories = _memories
                .OrderBy(memory => memory.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(memory => memory.Key, StringComparer.OrdinalIgnoreCase)
                .Select(CloneMemory)
                .ToArray()
        };
        var json = JsonSerializer.Serialize(document, JsonSerializerOptions);
        var tempPath = $"{_storePath}.tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _storePath, overwrite: true);
        IsHealthy = true;
    }

    private MemoryRecord NormalizeMemory(MemoryRecord memory, MemoryRecord? existing)
    {
        var now = DateTimeOffset.UtcNow;
        return new MemoryRecord
        {
            MemoryId = existing?.MemoryId
                ?? (string.IsNullOrWhiteSpace(memory.MemoryId) ? Guid.NewGuid().ToString("N") : memory.MemoryId),
            Category = NormalizeCategory(memory.Category),
            Key = NormalizeKey(memory.Key),
            Value = memory.Value.Trim(),
            Source = memory.Source.Trim(),
            CreatedAtUtc = existing?.CreatedAtUtc ?? (memory.CreatedAtUtc == default ? now : memory.CreatedAtUtc),
            UpdatedAtUtc = now,
            Confidence = Math.Clamp(memory.Confidence, 0, 1)
        };
    }

    private static string NormalizeCategory(string category)
    {
        var normalized = Normalize(category).Replace(' ', '-');
        return SupportedCategories.Contains(normalized)
            ? normalized
            : "fact";
    }

    private static string NormalizeKey(string key)
    {
        return Normalize(key).Replace(' ', '_');
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool Matches(MemoryRecord memory, IReadOnlyCollection<string> queryTerms)
    {
        var searchableText = Normalize($"{memory.Category} {memory.Key} {memory.Value} {memory.Source}");
        return queryTerms.Any(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] GetSearchTerms(string query)
    {
        return Normalize(query)
            .Replace("?", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ReplaceMemory(MemoryRecord memory)
    {
        _memories.RemoveAll(item => string.Equals(item.MemoryId, memory.MemoryId, StringComparison.OrdinalIgnoreCase));
        _memories.Add(memory);
    }

    private static MemoryRecord CloneMemory(MemoryRecord memory)
    {
        return new MemoryRecord
        {
            MemoryId = memory.MemoryId,
            Category = memory.Category,
            Key = memory.Key,
            Value = memory.Value,
            Source = memory.Source,
            CreatedAtUtc = memory.CreatedAtUtc,
            UpdatedAtUtc = memory.UpdatedAtUtc,
            Confidence = memory.Confidence
        };
    }

    private static string GetDefaultStorePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Merlin", "long-term-memory.json");
    }

    private sealed class LongTermMemoryDocument
    {
        public IReadOnlyCollection<MemoryRecord> Memories { get; init; } = [];
    }
}
