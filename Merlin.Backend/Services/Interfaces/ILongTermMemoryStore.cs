using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface ILongTermMemoryStore
{
    bool IsHealthy { get; }

    IReadOnlyCollection<MemoryRecord> GetAll();

    IReadOnlyList<MemoryRecord> GetByCategory(string category);

    IReadOnlyList<MemoryRecord> Search(string query);

    IReadOnlyList<MemoryRecord> GetMostRelevant(string query, int count);

    MemoryRecord SaveMemory(MemoryRecord memory);

    MemoryRecord UpdateMemory(MemoryRecord memory);

    bool DeleteMemory(string memoryId);

    MemoryRecord MergeMemory(MemoryRecord memory);
}
