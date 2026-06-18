using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class WebSearchToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCommandContainsQuery_ReturnsDeterministicFakeResults()
    {
        var tool = CreateTool(enabled: true);

        var result = await tool.ExecuteAsync("web_search chatterbox turbo latency");

        Assert.True(result.Success);
        Assert.Equal("web_search", result.Intent);
        Assert.Equal("web_search", result.CapabilityId);
        Assert.Equal("tool", result.ResponseType);
        Assert.Contains("Chatterbox", result.Message);
        Assert.Contains("https://", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStructuredRouteContainsQuery_UsesRouteArgument()
    {
        var tool = CreateTool(enabled: true);

        var result = await tool.ExecuteAsync(new ToolExecutionContext
        {
            NormalizedCommand = "web_search",
            Route = new CapabilityRouteResult(
                "web_search",
                "search",
                TargetScopes.Web,
                "web_search",
                0.9,
                true,
                false,
                CapabilitySafetyLevel.ExternalRequest,
                null,
                [],
                "web_search",
                new Dictionary<string, string> { ["query"] = "DeepInfra pricing" },
                true,
                "test",
                "Web Search",
                CapabilityAvailability.Implemented)
        });

        Assert.True(result.Success);
        Assert.Contains("DeepInfra pricing", result.Message);
        Assert.Contains("deepinfra.com/pricing", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryIsEmpty_ReturnsFriendlyError()
    {
        var tool = CreateTool(enabled: true);

        var result = await tool.ExecuteAsync("web_search");

        Assert.False(result.Success);
        Assert.Equal("WEB_SEARCH_EMPTY_QUERY", result.ErrorCode);
        Assert.Contains("what to search", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWebSearchIsDisabled_ReturnsSetupMessage()
    {
        var tool = CreateTool(enabled: false);

        var result = await tool.ExecuteAsync("web_search chatterbox turbo latency");

        Assert.False(result.Success);
        Assert.Equal("WEB_SEARCH_PROVIDER_ERROR", result.ErrorCode);
        Assert.Contains("disabled", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRealProviderHasNoApiKey_ReturnsSetupMessage()
    {
        var tool = CreateTool(enabled: true, providerName: "Brave", apiKey: "");

        var result = await tool.ExecuteAsync("web_search chatterbox turbo latency");

        Assert.False(result.Success);
        Assert.Contains("needs an API key", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderThrowsTimeout_ReturnsFriendlyError()
    {
        var tool = CreateTool(enabled: true, provider: new TimeoutProvider());

        var result = await tool.ExecuteAsync("web_search chatterbox turbo latency");

        Assert.False(result.Success);
        Assert.Contains("timed out", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProviderReturnsEmptyResults_ReturnsNoResultsMessage()
    {
        var tool = CreateTool(enabled: true, provider: new EmptyProvider());

        var result = await tool.ExecuteAsync("web_search obscure topic");

        Assert.False(result.Success);
        Assert.Equal("WEB_SEARCH_NO_RESULTS", result.ErrorCode);
        Assert.Contains("couldn't find reliable public results", result.Message);
    }

    [Fact]
    public async Task FakeProvider_WhenTechnicalOfficialDocsQueryIsUsed_PrefersOfficialDomains()
    {
        var provider = new FakeWebSearchProvider();

        var response = await provider.SearchAsync(
            new WebSearchRequest(
                "official Godot docs for transparent windows",
                3,
                null,
                null,
                true,
                SearchFreshness.Any),
            CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("docs.godotengine.org", response.Results[0].DisplayUrl);
    }

    [Fact]
    public async Task SearchAsync_WhenCancellationIsRequested_PropagatesCancellation()
    {
        var tool = CreateTool(enabled: true, provider: new CancellationObservingProvider());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            tool.ExecuteAsync("web_search chatterbox turbo latency", cancellation.Token));
    }

    private static WebSearchTool CreateTool(
        bool enabled,
        string providerName = "Fake",
        string apiKey = "",
        IWebSearchProvider? provider = null)
    {
        var options = Options.Create(new WebSearchOptions
        {
            Enabled = enabled,
            Provider = providerName,
            ApiKey = apiKey,
            MaxResults = 8,
            PreferOfficialSourcesForTechnicalQueries = true
        });
        var service = new WebSearchService(
            provider ?? new FakeWebSearchProvider(),
            options,
            NullLogger<WebSearchService>.Instance);
        return new WebSearchTool(service);
    }

    private sealed class TimeoutProvider : IWebSearchProvider
    {
        public Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
        {
            throw new TimeoutException();
        }
    }

    private sealed class EmptyProvider : IWebSearchProvider
    {
        public Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WebSearchResponse(request.Query, [], "Fake", true, null));
        }
    }

    private sealed class CancellationObservingProvider : IWebSearchProvider
    {
        public Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new WebSearchResponse(request.Query, [], "Fake", true, null));
        }
    }
}
