using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.IntentRouting;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ScopeAwareRoutingTests
{
    [Theory]
    [InlineData("please look up folder x", TargetScopes.LocalFiles, "file_access", "missing_capability")]
    [InlineData("can you find out where this file exists", TargetScopes.LocalFiles, "file_access", "missing_capability")]
    [InlineData("look up my meeting tomorrow", TargetScopes.Calendar, "calendar", "missing_capability")]
    [InlineData("look up the email from school", TargetScopes.Email, "email", "missing_capability")]
    [InlineData("find out what we discussed yesterday", TargetScopes.Memory, "memory_lookup", "missing_capability")]
    [InlineData("look up current DeepInfra pricing", TargetScopes.Web, "web_research", "missing_capability")]
    [InlineData("search the web for chatterbox turbo latency", TargetScopes.Web, "web_search", "web_search")]
    [InlineData("find official Godot docs for transparent windows", TargetScopes.Web, "web_research", "missing_capability")]
    [InlineData("find out whether faster-whisper beam_size affects VRAM", TargetScopes.Web, "web_research", "missing_capability")]
    [InlineData("check if our Chatterbox setup is wrong compared to official docs", TargetScopes.ProjectRepo, "codex_research", "missing_capability")]
    [InlineData("fix our Chatterbox setup based on the docs", TargetScopes.ProjectRepo, "codex_implementation", "missing_capability")]
    public void Route_WhenAmbiguousLookupPhraseIsUsed_SelectsScopeAndCapability(
        string message,
        string expectedScope,
        string expectedCapability,
        string expectedIntent)
    {
        var result = CreateRouter().ToIntentParseResult(message);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal(expectedCapability, result.CapabilityId);
        Assert.NotNull(result.Route);
        Assert.Equal(expectedScope, result.Route.TargetScope);
        Assert.Equal(expectedCapability, result.Route.RecommendedCapability);
        Assert.Contains(result.Route.CandidateScores, score => score.TargetScope == expectedScope);
    }

    [Fact]
    public void Route_WhenFolderLookupIsUsed_DoesNotRouteToWebSearch()
    {
        var result = CreateRouter().ToIntentParseResult("please look up folder x");

        Assert.Equal("file_access", result.CapabilityId);
        Assert.NotEqual("web_search", result.Route?.RecommendedCapability);
        Assert.Equal(CapabilitySafetyLevel.PrivateRead, result.Route?.SafetyLevel);
    }

    [Fact]
    public void Route_WhenSearchTheWebIsUsed_BuildsExecutableWebSearchRoute()
    {
        var result = CreateRouter().ToIntentParseResult("search the web for chatterbox turbo latency");

        Assert.Equal("web_search", result.Intent);
        Assert.Equal("web_search chatterbox turbo latency", result.NormalizedCommand);
        Assert.Equal(CapabilityAvailability.Implemented, result.Route?.Availability);
        Assert.Equal(CapabilitySafetyLevel.ExternalRequest, result.Route?.SafetyLevel);
        Assert.True(result.Route?.ShouldExecuteTool);
        Assert.Equal("chatterbox turbo latency", result.Route?.Arguments["query"]);
    }

    [Theory]
    [InlineData("open Chrome", "open_application", "open chrome")]
    [InlineData("open github.com", "open_url", "open github.com")]
    [InlineData("what time is it", "system_resource_query", "system resource current_time")]
    public async Task HybridParser_WhenExistingCommandIsUsed_PreservesExistingRuleRouting(
        string message,
        string expectedIntent,
        string expectedCommand)
    {
        var parser = CreateHybridParser();

        var result = await parser.ParseAsync(message);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal(expectedCommand, result.NormalizedCommand);
        Assert.Equal(nameof(RuleBasedIntentParser), result.ParserUsed);
    }

    [Theory]
    [InlineData("what is time complexity")]
    [InlineData("what is memory in C#")]
    public async Task HybridParser_WhenGeneralExplanationUsesAmbiguousTerms_DoesNotRouteToSystemOrWeb(
        string message)
    {
        var parser = CreateHybridParser(includeHierarchicalRouter: true);

        var result = await parser.ParseAsync(message);

        Assert.NotEqual("system_resource_query", result.Intent);
        Assert.NotEqual("web_search", result.Intent);
        Assert.NotEqual("missing_capability", result.Intent);
    }

    [Fact]
    public async Task HybridParser_WhenCurrentMemoryUsageIsAsked_PreservesExistingSystemResourceRecognition()
    {
        var parser = CreateHybridParser(includeHierarchicalRouter: true);

        var result = await parser.ParseAsync("what is current memory usage");

        Assert.Equal("missing_capability", result.Intent);
        Assert.Equal("system.get_memory", result.CapabilityId);
        Assert.Equal(nameof(MerlinIntentRouter), result.ParserUsed);
    }

    [Fact]
    public async Task CommandRouter_WhenSearchTheWebIsAsked_RoutesAndExecutesWebSearchTool()
    {
        var webSearchOptions = Options.Create(new WebSearchOptions
        {
            Enabled = true,
            Provider = "Fake",
            MaxResults = 8,
            PreferOfficialSourcesForTechnicalQueries = true
        });
        var webSearchTool = new WebSearchTool(new WebSearchService(
            new FakeWebSearchProvider(),
            webSearchOptions,
            NullLogger<WebSearchService>.Instance));
        var commandRouter = new CommandRouter(
            CreateHybridParser(),
            new ToolRegistry([webSearchTool]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var response = await commandRouter.RouteAsync("search the web for chatterbox turbo latency");

        Assert.True(response.Success);
        Assert.Equal("Web Search", response.ToolName);
        Assert.Equal("web_search", response.Intent);
        Assert.Equal("web_search", response.CapabilityId);
        Assert.Contains("Chatterbox", response.Message);
    }

    private static ScopeAwareCapabilityRouter CreateRouter()
    {
        return new ScopeAwareCapabilityRouter(
            new TargetScopeDetector(),
            new CapabilitySafetyClassifier(),
            TestCapabilityOptions.Create());
    }

    private static HybridIntentParser CreateHybridParser(bool includeHierarchicalRouter = false)
    {
        var localAIOptions = Options.Create(new LocalAIOptions
        {
            Enabled = false,
            MinimumConfidence = 0.70
        });
        var healthService = new LocalAIHealthService(
            new FakeLocalAIClient(),
            localAIOptions,
            NullLogger<LocalAIHealthService>.Instance);

        return new HybridIntentParser(
            new RuleBasedIntentParser(CreateApplicationOptions()),
            new FakeLocalAIIntentParser(),
            new CapabilityClassifier(new ToolRegistry([]), TestCapabilityOptions.Create()),
            localAIOptions,
            new RuntimeStateService(),
            healthService,
            NullLogger<HybridIntentParser>.Instance,
            includeHierarchicalRouter ? MerlinIntentRouterTests.CreateRouter() : null,
            null,
            null,
            new SpeechCommandNormalizer(),
            CreateRouter());
    }

    private static IOptions<ApplicationLaunchOptions> CreateApplicationOptions()
    {
        return Options.Create(new ApplicationLaunchOptions
        {
            Applications = new Dictionary<string, ApplicationLaunchTarget>(StringComparer.OrdinalIgnoreCase)
            {
                ["chrome"] = new ApplicationLaunchTarget
                {
                    DisplayName = "Chrome",
                    ExecutableOrUrl = "chrome.exe",
                    Aliases = ["chrome"]
                }
            }
        });
    }

    private sealed class FakeLocalAIIntentParser : LocalAIIntentParser
    {
        public FakeLocalAIIntentParser()
            : base(
                new FakeLocalAIClient(),
                Options.Create(new LocalAIOptions { Enabled = false }),
                TestCapabilityOptions.Create(),
                new ToolRegistry([]),
                new FakeAssistantPolicyProvider(),
                NullLogger<LocalAIIntentParser>.Instance,
                new LocalAIHealthService(
                    new FakeLocalAIClient(),
                    Options.Create(new LocalAIOptions { Enabled = false }),
                    NullLogger<LocalAIHealthService>.Instance))
        {
        }
    }

    private sealed class FakeLocalAIClient : ILocalAIClient
    {
        public Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeAssistantPolicyProvider : IAssistantPolicyProvider
    {
        public string GetPolicyText()
        {
            return "TEST POLICY";
        }
    }
}
