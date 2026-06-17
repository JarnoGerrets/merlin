using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Search;

public sealed class MemorySearchService : IMemorySearchService
{
    private readonly IMemoryStore _memoryStore;

    public MemorySearchService(IMemoryStore memoryStore)
    {
        _memoryStore = memoryStore;
    }

    public Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        MemorySearchRequest request,
        CancellationToken cancellationToken = default) =>
        _memoryStore.SearchMemoriesAsync(request, cancellationToken);
}
