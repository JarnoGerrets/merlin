using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class HybridIntentParserTests
{
    [Fact]
    public async Task ParseAsync_WhenRuleConfidenceIsHigh_ReturnsRuleResultWithoutCallingLocalAI()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open example.com",
            Confidence = 0.95,
            OriginalMessage = "open notepad"
        });

        var parser = CreateParser(localParser, enabled: true);

        var result = await parser.ParseAsync("open notepad");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open notepad", result.NormalizedCommand);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenTrustedCommandExists_UsesTrustedParserBeforeRuleBasedParser()
    {
        var store = new FakeTrustedCommandStore();
        store.SaveMapping(new TrustedCommandMapping
        {
            OriginalCommand = "open notepad",
            Intent = "open_application",
            NormalizedCommand = "open paint",
            ToolName = "Open Application",
            Target = "mspaint.exe",
            DisplayName = "Paint",
            UseCount = 1
        });
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open example.com",
            Confidence = 0.95,
            OriginalMessage = "open notepad"
        });

        var parser = CreateParser(localParser, enabled: true, trustedCommandStore: store);

        var result = await parser.ParseAsync("open notepad");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open paint", result.NormalizedCommand);
        Assert.Equal(nameof(TrustedCommandIntentParser), result.ParserUsed);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenLocalAIIsDisabled_ReturnsFallbackResult()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open google.com",
            Confidence = 0.95,
            OriginalMessage = "bring up google"
        });

        var parser = CreateParser(localParser, enabled: false);

        var result = await parser.ParseAsync("bring up google");

        Assert.Equal("general_conversation", result.Intent);
        Assert.True(result.Confidence > 0);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenRuleConfidenceIsLowAndLocalAIEnabled_ReturnsLocalAIResult()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open google.com",
            Confidence = 0.85,
            OriginalMessage = "bring up google"
        });

        var parser = CreateParser(localParser, enabled: true);

        var result = await parser.ParseAsync("bring up google");

        Assert.Equal("open_url", result.Intent);
        Assert.Equal("open google.com", result.NormalizedCommand);
        Assert.Equal(1, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenTrustedCommandExists_UsesTrustedParserBeforeLocalAIParser()
    {
        var store = new FakeTrustedCommandStore();
        store.SaveMapping(new TrustedCommandMapping
        {
            OriginalCommand = "could you open paint for me",
            Intent = "open_application",
            NormalizedCommand = "open paint",
            ToolName = "Open Application",
            Target = "mspaint.exe",
            DisplayName = "Paint",
            UseCount = 1
        });
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open google.com",
            Confidence = 0.85,
            OriginalMessage = "could you open paint for me"
        });

        var parser = CreateParser(localParser, enabled: true, trustedCommandStore: store);

        var result = await parser.ParseAsync("could you open paint for me");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open paint", result.NormalizedCommand);
        Assert.Equal(nameof(TrustedCommandIntentParser), result.ParserUsed);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenLocalAIIsEnabledButUnavailable_ReturnsFallbackResult()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open google.com",
            Confidence = 0.85,
            OriginalMessage = "bring up google"
        });

        var parser = CreateParser(localParser, enabled: true, localAIAvailable: false);

        var result = await parser.ParseAsync("bring up google");

        Assert.Equal("general_conversation", result.Intent);
        Assert.True(result.Confidence > 0);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenFallbackSeesUnsupportedAction_ReturnsUnsupportedAction()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = null,
            NormalizedCommand = "delete my files",
            Confidence = 0,
            OriginalMessage = "delete my files"
        });

        var parser = CreateParser(localParser, enabled: false);

        var result = await parser.ParseAsync("delete my files");

        Assert.Equal("unsupported_action", result.Intent);
        Assert.Equal("delete my files", result.NormalizedCommand);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenFallbackSeesTimeQuestion_ReturnsGeneralConversation()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = null,
            NormalizedCommand = "what time is it",
            Confidence = 0,
            OriginalMessage = "what time is it?"
        });

        var parser = CreateParser(localParser, enabled: false);

        var result = await parser.ParseAsync("what time is it?");

        Assert.Equal("general_conversation", result.Intent);
        Assert.Equal("chat what time is it", result.NormalizedCommand);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenLocalAIReturnsMissingCapability_UsesLocalAIResult()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "missing_capability",
            NormalizedCommand = "can you pull up the newsfeed",
            Confidence = 0.9,
            OriginalMessage = "can you pull up the newsfeed?"
        });

        var parser = CreateParser(localParser, enabled: true);

        var result = await parser.ParseAsync("can you pull up the newsfeed?");

        Assert.Equal("missing_capability", result.Intent);
        Assert.Equal(nameof(LocalAIIntentParser), result.ParserUsed);
        Assert.Equal(1, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenLocalAIUnavailableAndNewsfeedRequested_FallsBackToMissingCapability()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = null,
            NormalizedCommand = "can you pull up the newsfeed",
            Confidence = 0,
            OriginalMessage = "can you pull up the newsfeed?"
        });

        var parser = CreateParser(localParser, enabled: true, localAIAvailable: false);

        var result = await parser.ParseAsync("can you pull up the newsfeed?");

        Assert.Equal("missing_capability", result.Intent);
        Assert.NotEqual("unknown", result.Intent);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenFolderCheckRequested_DoesNotRouteToDiagnostics()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = null,
            NormalizedCommand = "can you check my folders",
            Confidence = 0,
            OriginalMessage = "can you check my folders?"
        });

        var parser = CreateParser(localParser, enabled: false);

        var result = await parser.ParseAsync("can you check my folders?");

        Assert.Equal("missing_capability", result.Intent);
        Assert.NotEqual("diagnostics", result.Intent);
        Assert.Equal(0, localParser.CallCount);
    }

    private static HybridIntentParser CreateParser(
        LocalAIIntentParser localParser,
        bool enabled,
        bool localAIAvailable = true,
        ITrustedCommandStore? trustedCommandStore = null)
    {
        var localAIOptions = Options.Create(new LocalAIOptions
        {
            Enabled = enabled,
            MinimumConfidence = 0.70
        });
        var healthService = new LocalAIHealthService(
            new FakeLocalAIClient(),
            localAIOptions,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalAIHealthService>.Instance);

        if (localAIAvailable)
        {
            healthService.MarkAvailable(1);
        }
        else
        {
            healthService.MarkUnavailable("offline");
        }

        return new HybridIntentParser(
            new TrustedCommandIntentParser(trustedCommandStore ?? new FakeTrustedCommandStore()),
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            localParser,
            new CapabilityClassifier(new ToolRegistry([]), TestCapabilityOptions.Create()),
            localAIOptions,
            new RuntimeStateService(),
            healthService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridIntentParser>.Instance);
    }

    private sealed class FakeIntentParser : LocalAIIntentParser
    {
        private readonly IntentParseResult _result;

        public FakeIntentParser(IntentParseResult result)
            : base(
                new FakeLocalAIClient(),
                Options.Create(new LocalAIOptions { Enabled = false }),
                new Merlin.Backend.Services.ToolRegistry([]),
                new FakeAssistantPolicyProvider(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalAIIntentParser>.Instance,
                new LocalAIHealthService(
                    new FakeLocalAIClient(),
                    Options.Create(new LocalAIOptions { Enabled = false }),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalAIHealthService>.Instance))
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public override Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeLocalAIClient : ILocalAIClient
    {
        public Task<string?> GenerateAsync(
            string prompt,
            CancellationToken cancellationToken = default)
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
