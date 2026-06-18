using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class WebSearchService
{
    private readonly ILogger<WebSearchService> _logger;
    private readonly WebSearchOptions _options;
    private readonly IWebSearchProvider _provider;

    public WebSearchService(
        IWebSearchProvider provider,
        IOptions<WebSearchOptions> options,
        ILogger<WebSearchService> logger)
    {
        _provider = provider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<WebSearchResponse> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Failure(query, "Please tell me what to search for.");
        }

        if (!_options.Enabled)
        {
            return Failure(query, "Web search is configured but disabled. Enable WebSearch:Enabled or MERLIN_WEBSEARCH_ENABLED to use it.");
        }

        if (!string.Equals(_options.Provider, "Fake", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Failure(query, $"The {_options.Provider} web search provider needs an API key before I can search.");
        }

        if (!string.Equals(_options.Provider, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(query, $"The {_options.Provider} web search provider is not implemented yet. The Fake provider is available for testing.");
        }

        try
        {
            _logger.LogInformation(
                "Web search requested. Provider={Provider}, QueryLength={QueryLength}, MaxResults={MaxResults}",
                _options.Provider,
                query.Length,
                _options.MaxResults);

            var request = new WebSearchRequest(
                query.Trim(),
                Math.Clamp(_options.MaxResults, 1, 20),
                null,
                null,
                _options.PreferOfficialSourcesForTechnicalQueries,
                SearchFreshness.Any);

            return await _provider.SearchAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return Failure(query, "The web search provider timed out before returning results.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Web search provider failed.");
            return Failure(query, "The web search provider failed before returning results.");
        }
    }

    private WebSearchResponse Failure(string query, string message)
    {
        return new WebSearchResponse(
            query,
            [],
            string.IsNullOrWhiteSpace(_options.Provider) ? "Unknown" : _options.Provider,
            false,
            message);
    }
}
