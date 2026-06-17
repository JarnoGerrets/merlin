using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.IntentRouting;
using Merlin.Backend.Tools;
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
    public async Task ParseAsync_WhenRuleBasedSeesTimeQuestion_ReturnsSystemResourceQuery()
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

        Assert.Equal("system_resource_query", result.Intent);
        Assert.Equal("system resource current_time", result.NormalizedCommand);
        Assert.Equal("system_time", result.CapabilityId);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenLocalAIUnavailableAndTimezoneQuestionIsFlexible_UsesHierarchicalRouter()
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "open_url",
            NormalizedCommand = "open google.com",
            Confidence = 0.95,
            OriginalMessage = "what timezone are we in?"
        });

        var parser = CreateParser(localParser, enabled: true, localAIAvailable: false, includeHierarchicalRouter: true);

        var result = await parser.ParseAsync("what timezone are we in?");

        Assert.Equal("system_resource_query", result.Intent);
        Assert.Equal("system resource timezone", result.NormalizedCommand);
        Assert.Equal("system_timezone", result.CapabilityId);
        Assert.Equal(nameof(MerlinIntentRouter), result.ParserUsed);
        Assert.Equal(0, localParser.CallCount);
    }

    [Theory]
    [InlineData("Can you open paint?", "open paint", "open_application", "application_launch", nameof(RuleBasedIntentParser))]
    [InlineData("Can you open the terminal for me?", "open terminal", "open_application", "application_launch", nameof(RuleBasedIntentParser))]
    [InlineData("Can you pull up facebook for me?", "open facebook", "open_application", "application_launch", nameof(RuleBasedIntentParser))]
    [InlineData("Please open facebook.com", "open facebook.com", "open_url", "url_opening", nameof(RuleBasedIntentParser))]
    [InlineData("Please open facebook.com for me", "open facebook.com", "open_url", "url_opening", nameof(MerlinIntentRouter))]
    public async Task ParseAsync_WhenLocalAIUnavailableAndOpenRequestIsPolite_UsesHierarchicalRouter(
        string message,
        string expectedCommand,
        string expectedIntent,
        string expectedCapabilityId,
        string expectedParser)
    {
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = $"chat {message}",
            Confidence = 0.95,
            OriginalMessage = message
        });

        var parser = CreateParser(localParser, enabled: true, localAIAvailable: false, includeHierarchicalRouter: true);

        var result = await parser.ParseAsync(message);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal(expectedCommand, result.NormalizedCommand);
        Assert.Equal(expectedCapabilityId, result.CapabilityId);
        Assert.Equal(expectedParser, result.ParserUsed);
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
            OriginalMessage = "can you pull up the newsfeed?",
            CapabilityId = "news",
            CapabilityName = "News"
        });

        var parser = CreateParser(localParser, enabled: true);

        var result = await parser.ParseAsync("can you pull up the newsfeed?");

        Assert.Equal("missing_capability", result.Intent);
        Assert.Equal("news", result.CapabilityId);
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

    [Fact]
    public async Task ParseAsync_WhenPendingCandidateNameMatches_ReturnsConfirmation()
    {
        var confirmationService = new ConfirmationService();
        confirmationService.Create(
            "open_application",
            string.Empty,
            "visual",
            "visual",
            "open visual",
            "open_application",
            "open visual",
            "Open Application",
            [
                new ApplicationCandidate
                {
                    DisplayName = "Visual Studio",
                    ExecutablePath = "visual-studio.lnk",
                    Source = "StartMenu",
                    Confidence = 0.85
                },
                new ApplicationCandidate
                {
                    DisplayName = "Visual Studio Installer",
                    ExecutablePath = "visual-studio-installer.lnk",
                    Source = "StartMenu",
                    Confidence = 0.85
                }
            ]);
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = "chat Visual Studio Installer",
            Confidence = 0.95,
            OriginalMessage = "Visual Studio Installer"
        });

        var parser = CreateParser(
            localParser,
            enabled: true,
            localAIAvailable: false,
            includeHierarchicalRouter: true,
            confirmationService: confirmationService);

        var result = await parser.ParseAsync("Visual Studio Installer");

        Assert.Equal("confirmation", result.Intent);
        Assert.Equal("Visual Studio Installer", result.NormalizedCommand);
        Assert.Equal(nameof(ConfirmationTool), result.ParserUsed);
        Assert.Equal(0, localParser.CallCount);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("I confirm.")]
    [InlineData("yes please")]
    [InlineData("yes please do")]
    [InlineData("yes please open it")]
    [InlineData("please do so")]
    [InlineData("do so")]
    [InlineData("yes do so")]
    [InlineData("sure do so")]
    [InlineData("okay please do so")]
    [InlineData("go ahead please")]
    [InlineData("open that as a website")]
    [InlineData("yes open that in the browser")]
    [InlineData("that works")]
    [InlineData("works for me")]
    [InlineData("sure go ahead")]
    [InlineData("okay do it")]
    [InlineData("go ahead")]
    [InlineData("open it")]
    public async Task ParseAsync_WhenPendingConfirmationAndConfirmationPhraseIsSpoken_ReturnsConfirmation(string confirmationPhrase)
    {
        var confirmationService = new ConfirmationService();
        confirmationService.Create(
            "open_application",
            "powerpnt.exe",
            "PowerPoint",
            "powerpoint",
            "open powerpoint",
            "open_application",
            "open powerpoint",
            "Open Application");
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = $"chat {confirmationPhrase}",
            Confidence = 0.95,
            OriginalMessage = confirmationPhrase
        });

        var parser = CreateParser(
            localParser,
            enabled: true,
            localAIAvailable: false,
            includeHierarchicalRouter: true,
            confirmationService: confirmationService);

        var result = await parser.ParseAsync(confirmationPhrase);

        Assert.Equal("confirmation", result.Intent);
        Assert.Equal(confirmationPhrase.Trim(), result.NormalizedCommand);
        Assert.Equal(nameof(ConfirmationTool), result.ParserUsed);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenPendingConfirmationAndUnrelatedMessageArrives_ClearsPendingAndRoutesMessage()
    {
        var confirmationService = new ConfirmationService();
        confirmationService.Create(
            "open_application",
            "powerpnt.exe",
            "PowerPoint",
            "powerpoint",
            "open powerpoint",
            "open_application",
            "open powerpoint",
            "Open Application");
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = "chat what time is it",
            Confidence = 0.95,
            OriginalMessage = "what time is it"
        });

        var parser = CreateParser(
            localParser,
            enabled: true,
            localAIAvailable: false,
            includeHierarchicalRouter: true,
            confirmationService: confirmationService);

        var result = await parser.ParseAsync("what time is it");

        Assert.Equal("system_resource_query", result.Intent);
        Assert.Equal("system resource current_time", result.NormalizedCommand);
        Assert.Equal(0, confirmationService.PendingCount);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenPendingBrowserMappingEditAndValidDomainArrives_ReturnsEditBrowserMapping()
    {
        var pendingInteractions = new PendingInteractionService();
        pendingInteractions.Create(
            PendingInteractionTypes.BrowserMappingEdit,
            "What should I change terminal to?",
            new Dictionary<string, string> { ["alias"] = "terminal" },
            "edit browser mapping terminal");
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = "chat terminal.nl",
            Confidence = 0.95,
            OriginalMessage = "terminal.nl"
        });

        var parser = CreateParser(
            localParser,
            enabled: true,
            localAIAvailable: false,
            includeHierarchicalRouter: true,
            pendingInteractionService: pendingInteractions);

        var result = await parser.ParseAsync("terminal dot nl");

        Assert.Equal("edit_browser_mapping", result.Intent);
        Assert.Equal("edit browser mapping terminal to terminal.nl", result.NormalizedCommand);
        Assert.Equal(0, pendingInteractions.PendingCount);
        Assert.Equal(0, localParser.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenPendingBrowserMappingEditAndUnrelatedMessageArrives_ClearsPendingAndRoutesMessage()
    {
        var pendingInteractions = new PendingInteractionService();
        pendingInteractions.Create(
            PendingInteractionTypes.BrowserMappingEdit,
            "What should I change terminal to?",
            new Dictionary<string, string> { ["alias"] = "terminal" },
            "edit browser mapping terminal");
        var localParser = new FakeIntentParser(new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = "chat what time is it",
            Confidence = 0.95,
            OriginalMessage = "what time is it"
        });

        var parser = CreateParser(
            localParser,
            enabled: true,
            localAIAvailable: false,
            includeHierarchicalRouter: true,
            pendingInteractionService: pendingInteractions);

        var result = await parser.ParseAsync("what time is it");

        Assert.Equal("system_resource_query", result.Intent);
        Assert.Equal("system resource current_time", result.NormalizedCommand);
        Assert.Equal(0, pendingInteractions.PendingCount);
        Assert.Equal(0, localParser.CallCount);
    }

    private static HybridIntentParser CreateParser(
        LocalAIIntentParser localParser,
        bool enabled,
        bool localAIAvailable = true,
        bool includeHierarchicalRouter = false,
        IConfirmationService? confirmationService = null,
        IPendingInteractionService? pendingInteractionService = null)
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
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            localParser,
            new CapabilityClassifier(new ToolRegistry([]), TestCapabilityOptions.Create()),
            localAIOptions,
            new RuntimeStateService(),
            healthService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<HybridIntentParser>.Instance,
            includeHierarchicalRouter ? MerlinIntentRouterTests.CreateRouter() : null,
            confirmationService,
            pendingInteractionService,
            new SpeechCommandNormalizer());
    }

    private sealed class FakeIntentParser : LocalAIIntentParser
    {
        private readonly IntentParseResult _result;

        public FakeIntentParser(IntentParseResult result)
            : base(
                new FakeLocalAIClient(),
                Options.Create(new LocalAIOptions { Enabled = false }),
                TestCapabilityOptions.Create(),
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
