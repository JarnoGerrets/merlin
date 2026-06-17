using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Stores;

public interface IMemoryStore
{
    Task SaveMemoryAsync(MemoryRecord memory, CancellationToken cancellationToken = default);
    Task<MemoryRecord?> GetMemoryAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemorySearchResult>> SearchMemoriesAsync(MemorySearchRequest request, CancellationToken cancellationToken = default);
    Task UpdateLastAccessedAsync(IReadOnlyCollection<string> memoryIds, DateTimeOffset accessedAt, CancellationToken cancellationToken = default);
    Task DeleteMemoryAsync(string id, CancellationToken cancellationToken = default);
}
