using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IWebSearchProvider
{
    Task<WebSearchResponse> SearchAsync(
        WebSearchRequest request,
        CancellationToken cancellationToken);
}
