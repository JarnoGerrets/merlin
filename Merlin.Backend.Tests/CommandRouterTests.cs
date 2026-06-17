using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class CommandRouterTests
{
    [Fact]
    public async Task RouteAsync_WhenCommandMatchesTool_ReturnsToolResponse()
    {
        var tool = new FakeTool("open test app", new ToolResult
        {
            Success = true,
            Message = "Opening test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "open test app",
            CorrelationId = "test-123"
        });

        Assert.True(response.Success);
        Assert.Equal("Opening test app...", response.Message);
        Assert.Equal("test-123", response.CorrelationId);
        Assert.Equal("Fake Tool", response.ToolName);
        Assert.Equal("fake_intent", response.Intent);
        Assert.Equal(1, response.IntentConfidence);
        Assert.Equal("open test app", response.OriginalMessage);
        Assert.Equal("open test app", tool.ExecutedCommand);
    }

    [Fact]
    public async Task RouteAsync_WhenCommandUsesAlias_ReturnsToolResponse()
    {
        var tool = new FakeTool("start test app", new ToolResult
        {
            Success = true,
            Message = "Starting test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync("START TEST APP");

        Assert.True(response.Success);
        Assert.Equal("Starting test app...", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenCorrelationIdIsProvided_PreservesCorrelationId()
    {
        var tool = new FakeTool("open test app", new ToolResult
        {
            Success = true,
            Message = "Opening test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "open test app",
            CorrelationId = "provided-id"
        });

        Assert.Equal("provided-id", response.CorrelationId);
    }

    [Fact]
    public async Task RouteAsync_WhenCorrelationIdIsMissing_GeneratesCorrelationId()
    {
        var tool = new FakeTool("open test app", new ToolResult
        {
            Success = true,
            Message = "Opening test app...",
            ToolName = "Fake Tool",
            Intent = "fake_intent"
        });

        var router = CreateRouter(tool);

        var response = await router.RouteAsync("open test app");

        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
    }

    [Fact]
    public async Task RouteAsync_WhenVoiceCommandContainsSpokenDomain_NormalizesBeforeParsing()
    {
        var launcher = new FakeProcessLauncher();
        var router = new CommandRouter(
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            new ToolRegistry([new OpenUrlTool(launcher)]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            new SpeechCommandNormalizer());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "open terminal dot nl",
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("Open URL", response.ToolName);
        Assert.Equal("open_url", response.Intent);
        Assert.Equal("open terminal.nl", response.OriginalMessage);
        Assert.Equal("https://terminal.nl", launcher.LaunchedTarget);
    }

    [Fact]
    public async Task RouteAsync_WhenVoiceMappingEditContainsDottedDomain_PreservesDomain()
    {
        var store = new FakeTrustedUrlStore();
        store.SaveMapping("terminal", "https://terminal.com", "terminal.com");
        var router = new CommandRouter(
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            new ToolRegistry([new EditBrowserMappingTool(store)]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher(),
            new SpeechCommandNormalizer());

        var response = await router.RouteAsync(new AssistantRequest
        {
            Message = "Can you change terminal browser mapping to terminal.nl?",
            InteractionSource = "voice"
        });

        Assert.True(response.Success);
        Assert.Equal("Edit Browser Mapping", response.ToolName);
        Assert.Equal("edit_browser_mapping", response.Intent);
        Assert.Equal("can you change terminal browser mapping to terminal.nl?", response.OriginalMessage);
        Assert.Equal("https://terminal.nl", store.FindByAlias("terminal")?.Url);
    }

    [Fact]
    public async Task RouteAsync_WhenCommandIsUnknown_ReturnsUnknownCommand()
    {
        var router = CreateRouter();

        var response = await router.RouteAsync("do something unknown");

        Assert.False(response.Success);
        Assert.Equal("Unknown command.", response.Message);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
        Assert.Null(response.ToolName);
        Assert.Null(response.Intent);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
    }

    [Fact]
    public async Task RouteAsync_WhenMessageIsEmpty_ReturnsUnknownInput()
    {
        var router = CreateRouter();

        var response = await router.RouteAsync(" ");

        Assert.False(response.Success);
        Assert.Equal("I couldn't understand that request.", response.Message);
        Assert.Equal("UNKNOWN_INPUT", response.ErrorCode);
        Assert.Equal("unknown_input", response.Intent);
        Assert.Equal("error", response.ResponseType);
    }

    [Fact]
    public async Task RouteAsync_WhenDiscoveryCommandIsUsed_ReturnsAvailableTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestApplicationLaunchOptions.Create());
        services.AddSingleton(Options.Create(new Merlin.Backend.Configuration.LocalAIOptions()));
        services.AddSingleton(TestCapabilityOptions.Create());
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<ILogger<StatusTool>>(NullLogger<StatusTool>.Instance);
        services.AddSingleton<ILocalAIHealthService>(new FakeLocalAIHealthService());
        services.AddSingleton<ILocalAIChatService, FakeLocalAIChatService>();
        services.AddSingleton<IConversationSummaryStore, FakeConversationSummaryStore>();
        services.AddSingleton<IConversationSessionService, ConversationSessionService>();
        services.AddSingleton<ILongTermMemoryStore, FakeLongTermMemoryStore>();
        services.AddSingleton<IMemoryExtractionService, FakeMemoryExtractionService>();
        services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
        services.AddSingleton<ISystemResourceProvider, FakeSystemResourceProvider>();
        services.AddSingleton<IProcessLauncher, FakeProcessLauncher>();
        services.AddSingleton<ITrustedApplicationStore, FakeTrustedApplicationStore>();
        services.AddSingleton<ITrustedCommandStore, FakeTrustedCommandStore>();
        services.AddSingleton<IApplicationResolver, ApplicationResolver>();
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        services.AddSingleton<ITool, OpenApplicationTool>();
        services.AddSingleton<ITool, OpenUrlTool>();
        services.AddSingleton<ITool, ToolDiscoveryTool>();
        services.AddSingleton<ITool, SystemResourceTool>();
        services.AddSingleton<ITool, StatusTool>();
        services.AddSingleton<ITool, ConfirmationTool>();
        services.AddSingleton<ITool, GeneralConversationTool>();
        services.AddSingleton<ToolRegistry>();
        services.AddSingleton<IAIService, DummyAIService>();
        services.AddSingleton<IIntentParser, RuleBasedIntentParser>();
        services.AddSingleton<IResponsePolisher, NoOpResponsePolisher>();
        services.AddSingleton<ILogger<CommandRouter>>(NullLogger<CommandRouter>.Instance);
        services.AddSingleton<CommandRouter>();

        await using var serviceProvider = services.BuildServiceProvider();
        var router = serviceProvider.GetRequiredService<CommandRouter>();

        var response = await router.RouteAsync("list tools");

        Assert.True(response.Success);
        Assert.Equal("Tool Discovery", response.ToolName);
        Assert.Equal("tool_discovery", response.Intent);
        Assert.True(response.IntentConfidence > 0);
        Assert.Equal("list tools", response.OriginalMessage);
        Assert.NotNull(response.AvailableTools);
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Open Application");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Open URL");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Tool Discovery");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "System Resource");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Status");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "Confirmation");
        Assert.Contains(response.AvailableTools, tool => tool.Name == "General Conversation");
    }

    [Fact]
    public async Task RouteAsync_WhenUnsupportedActionIsDetected_ReturnsPolishedUnsupportedActionWithoutToolExecution()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "unsupported_action",
                NormalizedCommand = "delete all my files",
                Confidence = 0.95,
                OriginalMessage = "delete all my files",
                ParserUsed = nameof(CapabilityClassifier),
                CapabilityId = "destructive_file_action",
                CapabilityName = "Destructive File Action"
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("delete all my files");

        Assert.False(response.Success);
        Assert.Equal("UNSUPPORTED_ACTION", response.ErrorCode);
        Assert.Equal("unsupported_action", response.Intent);
        Assert.Equal("destructive_file_action", response.CapabilityId);
        Assert.Equal("safety", response.ResponseType);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Contains("delete files", response.Message);
        Assert.DoesNotContain("UNSUPPORTED_ACTION", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenMissingCapabilityIsDetected_DoesNotExecuteTool()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "missing_capability",
                NormalizedCommand = "can you pull up the newsfeed",
                Confidence = 0.92,
                OriginalMessage = "can you pull up the newsfeed?",
                ParserUsed = nameof(LocalAIIntentParser),
                CapabilityId = "news",
                CapabilityName = "News"
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("can you pull up the newsfeed?");

        Assert.False(response.Success);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
        Assert.Equal("news", response.CapabilityId);
        Assert.Equal("limitation", response.ResponseType);
        Assert.Contains("News capability", response.Message);
        Assert.Contains("NewsTool or WebSearch capability", response.Message);
        Assert.DoesNotContain("MISSING_CAPABILITY", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenFileAccessCapabilityIsMissing_ReturnsFriendlyLimitation()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "missing_capability",
                NormalizedCommand = "check my folders",
                Confidence = 0.92,
                OriginalMessage = "check my folders",
                ParserUsed = nameof(CapabilityClassifier),
                CapabilityId = "file_access",
                CapabilityName = "File Access"
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("check my folders");

        Assert.False(response.Success);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
        Assert.Equal("file_access", response.CapabilityId);
        Assert.Equal("File Access", response.CapabilityName);
        Assert.Equal("limitation", response.ResponseType);
        Assert.Contains("file access capability", response.Message);
        Assert.DoesNotContain("MISSING_CAPABILITY", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenUnknownInputIsDetected_DoesNotExecuteTool()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "unknown_input",
                NormalizedCommand = "asdfghjkl qwerty",
                Confidence = 0.9,
                OriginalMessage = "asdfghjkl qwerty",
                ParserUsed = nameof(CapabilityClassifier)
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher(TestCapabilityOptions.Create()));

        var response = await router.RouteAsync("asdfghjkl qwerty");

        Assert.False(response.Success);
        Assert.Equal("UNKNOWN_INPUT", response.ErrorCode);
        Assert.Equal("unknown_input", response.Intent);
        Assert.Equal("error", response.ResponseType);
        Assert.Equal("I couldn't understand that request.", response.Message);
    }

    [Fact]
    public async Task RouteAsync_WhenSystemResourceIntentIsDetected_ExecutesSystemResourceTool()
    {
        var router = new CommandRouter(
            new FixedIntentParser(new IntentParseResult
            {
                Intent = "system_resource_query",
                NormalizedCommand = "system resource current_date",
                Confidence = 0.98,
                OriginalMessage = "what is today's date?",
                ParserUsed = nameof(RuleBasedIntentParser),
                CapabilityId = "system_date",
                CapabilityName = "System Date"
            }),
            new ToolRegistry([new SystemResourceTool(new FakeSystemResourceProvider())]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());

        var response = await router.RouteAsync("what is today's date?");

        Assert.True(response.Success);
        Assert.Equal("System Resource", response.ToolName);
        Assert.Equal("system_resource_query", response.Intent);
        Assert.Equal("system_date", response.CapabilityId);
        Assert.Equal("assistant", response.ResponseType);
        Assert.Contains("10-06-2026", response.Message);
    }

    private static CommandRouter CreateRouter(params ITool[] tools)
    {
        return new CommandRouter(
            new PassthroughIntentParser(),
            new ToolRegistry(tools),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());
    }

    private sealed class PassthroughIntentParser : IIntentParser
    {
        public Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IntentParseResult
            {
                Intent = null,
                NormalizedCommand = message.Trim(),
                Confidence = 1,
                OriginalMessage = message
            });
        }
    }

    private sealed class FixedIntentParser : IIntentParser
    {
        private readonly IntentParseResult _result;

        public FixedIntentParser(IntentParseResult result)
        {
            _result = result;
        }

        public Task<IntentParseResult> ParseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingTool : ITool
    {
        public string Name => "Throwing Tool";

        public string Description => "Fails if executed.";

        public IReadOnlyCollection<string> Examples { get; } = [];

        public bool CanHandle(string command)
        {
            return true;
        }

        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("This tool should not execute.");
        }
    }

    private sealed class FakeTool : ITool
    {
        private readonly string _supportedCommand;
        private readonly ToolResult _result;

        public FakeTool(string supportedCommand, ToolResult result)
        {
            _supportedCommand = supportedCommand;
            _result = result;
        }

        public string Name => "Fake Tool";

        public string Description => "A test-only tool.";

        public IReadOnlyCollection<string> Examples { get; } = ["open test app", "start test app"];

        public string? ExecutedCommand { get; private set; }

        public bool CanHandle(string command)
        {
            return string.Equals(command, _supportedCommand, StringComparison.OrdinalIgnoreCase);
        }

        public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            ExecutedCommand = command;
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public string? LaunchedTarget { get; private set; }

        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            LaunchedTarget = target;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLocalAIChatService : ILocalAIChatService
    {
        public Task<LocalAIChatResult> GenerateResponseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalAIChatResult
            {
                Success = true,
                Message = "chat"
            });
        }
    }

    private sealed class FakeLocalAIHealthService : ILocalAIHealthService
    {
        public bool IsEnabled => false;

        public bool IsAvailable => false;

        public DateTimeOffset? LastWarmupUtc => null;

        public string? LastError => null;

        public long? LastLatencyMs => null;

        public Task WarmupAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void MarkDisabled()
        {
        }

        public void MarkAvailable(long latencyMs)
        {
        }

        public void MarkUnavailable(string error, long? latencyMs = null)
        {
        }
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Merlin.Backend.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeSystemResourceProvider : ISystemResourceProvider
    {
        public DateTimeOffset GetCurrentLocalTime()
        {
            return new DateTimeOffset(2026, 6, 10, 13, 45, 30, TimeSpan.FromHours(2));
        }

        public DateOnly GetCurrentLocalDate()
        {
            return new DateOnly(2026, 6, 10);
        }

        public TimeZoneInfo GetLocalTimeZone()
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Test/Zone",
                TimeSpan.FromHours(2),
                "Test Time",
                "Test Standard Time");
        }
    }
}
