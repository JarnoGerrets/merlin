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
    public async Task RouteAsync_WhenMessageIsEmpty_ReturnsUnknownCommand()
    {
        var router = CreateRouter();

        var response = await router.RouteAsync(" ");

        Assert.False(response.Success);
        Assert.Equal("Unknown command.", response.Message);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
    }

    [Fact]
    public async Task RouteAsync_WhenDiscoveryCommandIsUsed_ReturnsAvailableTools()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestApplicationLaunchOptions.Create());
        services.AddSingleton(Options.Create(new Merlin.Backend.Configuration.LocalAIOptions()));
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<ILogger<StatusTool>>(NullLogger<StatusTool>.Instance);
        services.AddSingleton<ILocalAIHealthService>(new FakeLocalAIHealthService());
        services.AddSingleton<ILocalAIChatService, FakeLocalAIChatService>();
        services.AddSingleton<IConversationSummaryStore, FakeConversationSummaryStore>();
        services.AddSingleton<IConversationSessionService, ConversationSessionService>();
        services.AddSingleton<ILongTermMemoryStore, FakeLongTermMemoryStore>();
        services.AddSingleton<IMemoryExtractionService, FakeMemoryExtractionService>();
        services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
        services.AddSingleton<IProcessLauncher, FakeProcessLauncher>();
        services.AddSingleton<ITrustedApplicationStore, FakeTrustedApplicationStore>();
        services.AddSingleton<ITrustedCommandStore, FakeTrustedCommandStore>();
        services.AddSingleton<IApplicationResolver, ApplicationResolver>();
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        services.AddSingleton<ITool, OpenApplicationTool>();
        services.AddSingleton<ITool, OpenUrlTool>();
        services.AddSingleton<ITool, ToolDiscoveryTool>();
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
                NormalizedCommand = "can you check my folders",
                Confidence = 0.95,
                OriginalMessage = "can you check my folders?",
                ParserUsed = nameof(CapabilityClassifier)
            }),
            new ToolRegistry([new ThrowingTool()]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new ResponsePolisher());

        var response = await router.RouteAsync("can you check my folders?");

        Assert.False(response.Success);
        Assert.Equal("UNSUPPORTED_ACTION", response.ErrorCode);
        Assert.Equal("unsupported_action", response.Intent);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Contains("don't currently have a tool", response.Message);
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
        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
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
}
