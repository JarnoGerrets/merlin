using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tests;

internal sealed class FakeLongTermMemoryStore : ILongTermMemoryStore
{
    private readonly List<MemoryRecord> _memories = [];

    public bool IsHealthy { get; set; } = true;

    public IReadOnlyCollection<MemoryRecord> GetAll()
    {
        return _memories.ToArray();
    }

    public IReadOnlyList<MemoryRecord> GetByCategory(string category)
    {
        return _memories
            .Where(memory => string.Equals(memory.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public IReadOnlyList<MemoryRecord> Search(string query)
    {
        var terms = query
            .ToLowerInvariant()
            .Replace("?", string.Empty)
            .Replace(".", string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length >= 3)
            .ToArray();

        return _memories
            .Where(memory => terms.Any(term =>
                memory.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                || memory.Key.Contains(term, StringComparison.OrdinalIgnoreCase)
                || memory.Value.Contains(term, StringComparison.OrdinalIgnoreCase)
                || memory.Source.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    public IReadOnlyList<MemoryRecord> GetMostRelevant(string query, int count)
    {
        return Search(query).Take(count).ToArray();
    }

    public MemoryRecord SaveMemory(MemoryRecord memory)
    {
        var saved = Normalize(memory, existing: null);
        _memories.RemoveAll(item => item.MemoryId == saved.MemoryId);
        _memories.Add(saved);
        return saved;
    }

    public MemoryRecord UpdateMemory(MemoryRecord memory)
    {
        var existing = _memories.FirstOrDefault(item => item.MemoryId == memory.MemoryId);
        var saved = Normalize(memory, existing);
        _memories.RemoveAll(item => item.MemoryId == saved.MemoryId);
        _memories.Add(saved);
        return saved;
    }

    public bool DeleteMemory(string memoryId)
    {
        return _memories.RemoveAll(memory => memory.MemoryId == memoryId) > 0;
    }

    public MemoryRecord MergeMemory(MemoryRecord memory)
    {
        var existing = _memories.FirstOrDefault(item =>
            string.Equals(item.Category, memory.Category, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Key, memory.Key, StringComparison.OrdinalIgnoreCase));
        return UpdateMemory(new MemoryRecord
        {
            MemoryId = existing?.MemoryId ?? memory.MemoryId,
            Category = memory.Category,
            Key = memory.Key,
            Value = memory.Value,
            Source = memory.Source,
            Confidence = memory.Confidence
        });
    }

    private static MemoryRecord Normalize(MemoryRecord memory, MemoryRecord? existing)
    {
        var now = DateTimeOffset.UtcNow;
        return new MemoryRecord
        {
            MemoryId = existing?.MemoryId ?? (string.IsNullOrWhiteSpace(memory.MemoryId) ? Guid.NewGuid().ToString("N") : memory.MemoryId),
            Category = memory.Category,
            Key = memory.Key,
            Value = memory.Value,
            Source = memory.Source,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now,
            Confidence = memory.Confidence
        };
    }
}
