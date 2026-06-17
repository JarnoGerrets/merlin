using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Search;

public interface IMemorySearchService
{
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        MemorySearchRequest request,
        CancellationToken cancellationToken = default);
}
