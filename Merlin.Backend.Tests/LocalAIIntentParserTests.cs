using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LocalAIIntentParserTests
{
    [Fact]
    public async Task ParseAsync_WhenLocalAIIsDisabled_ReturnsUnknownWithoutCallingClient()
    {
        var client = new FakeLocalAIClient("""{"intent":"open_url","normalizedCommand":"open google.com","confidence":0.9}""");
        var parser = CreateParser(client, enabled: false);

        var result = await parser.ParseAsync("take me to google");

        Assert.Null(result.Intent);
        Assert.Equal(0, result.Confidence);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task ParseAsync_WhenModelReturnsValidSupportedCommand_ReturnsIntent()
    {
        var parser = CreateParser(new FakeLocalAIClient(
            """{"intent":"open_url","normalizedCommand":"open google.com","confidence":0.85}"""));

        var result = await parser.ParseAsync("please bring up google");

        Assert.Equal("open_url", result.Intent);
        Assert.Equal("open google.com", result.NormalizedCommand);
        Assert.Equal(0.85, result.Confidence);
        Assert.Equal("please bring up google", result.OriginalMessage);
    }

    [Fact]
    public async Task ParseAsync_WhenModelReturnsGeneralConversation_ReturnsChatCommand()
    {
        var parser = CreateParser(new FakeLocalAIClient(
            """{"intent":"general_conversation","normalizedCommand":"tell me a joke","confidence":0.9}"""));

        var result = await parser.ParseAsync("tell me a joke");

        Assert.Equal("general_conversation", result.Intent);
        Assert.Equal("chat tell me a joke", result.NormalizedCommand);
        Assert.Equal(0.9, result.Confidence);
        Assert.Equal("tell me a joke", result.OriginalMessage);
    }

    [Theory]
    [InlineData("""{"intent":"missing_capability","normalizedCommand":"can you pull up the newsfeed for me","confidence":0.9}""", "missing_capability", "can you pull up the newsfeed for me")]
    [InlineData("""{"intent":"unsupported_action","normalizedCommand":"delete all my files","confidence":0.95}""", "unsupported_action", "delete all my files")]
    [InlineData("""{"intent":"unknown_input","normalizedCommand":"asdfghjkl qwerty","confidence":0.9}""", "unknown_input", "asdfghjkl qwerty")]
    public async Task ParseAsync_WhenModelReturnsNonExecutableClassification_ReturnsIntent(
        string modelResponse,
        string expectedIntent,
        string expectedCommand)
    {
        var parser = CreateParser(new FakeLocalAIClient(modelResponse));

        var result = await parser.ParseAsync(expectedCommand);

        Assert.Equal(expectedIntent, result.Intent);
        Assert.Equal(expectedCommand, result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
    }

    [Theory]
    [InlineData("""{"intent":"tool_discovery","normalizedCommand":"list tools","confidence":0.9}""")]
    [InlineData("""
        ```json
        {
          "intent": "tool_discovery",
          "normalizedCommand": "list tools",
          "confidence": 0.9
        }
        ```
        """)]
    [InlineData("""
        ```
        {
          "intent": "tool_discovery",
          "normalizedCommand": "list tools",
          "confidence": 0.9
        }
        ```
        """)]
    [InlineData("""`{"intent":"tool_discovery","normalizedCommand":"list tools","confidence":0.9}`""")]
    [InlineData("""Here is the JSON: {"intent":"tool_discovery","normalizedCommand":"list tools","confidence":0.9} Done.""")]
    public async Task ParseAsync_WhenModelReturnsJsonInToleratedFormat_ReturnsIntent(string modelResponse)
    {
        var parser = CreateParser(new FakeLocalAIClient(modelResponse));

        var result = await parser.ParseAsync("what are your tools");

        Assert.Equal("tool_discovery", result.Intent);
        Assert.Equal("list tools", result.NormalizedCommand);
        Assert.Equal(0.9, result.Confidence);
    }

    [Theory]
    [InlineData("not json", null)]
    [InlineData("""```json {"intent":"tool_discovery"}""", """{"intent":"tool_discovery"}""")]
    [InlineData("""before {"intent":"tool_discovery","normalizedCommand":"list tools","confidence":0.9} after""", """{"intent":"tool_discovery","normalizedCommand":"list tools","confidence":0.9}""")]
    public void ExtractJsonObject_ExtractsFirstJsonObjectWhenPresent(string response, string? expected)
    {
        var extracted = LocalAIIntentParser.ExtractJsonObject(response);

        Assert.Equal(expected, extracted);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"intent":"delete_file","normalizedCommand":"delete stuff","confidence":0.95}""")]
    [InlineData("""{"intent":"open_url","normalizedCommand":"open google.com","confidence":0.2}""")]
    [InlineData("""{"intent":"open_url","normalizedCommand":"not a command","confidence":0.95}""")]
    [InlineData("""{"intent":"unknown","normalizedCommand":"","confidence":0}""")]
    public async Task ParseAsync_WhenModelOutputIsUnsafeOrUnsupported_ReturnsUnknown(string modelResponse)
    {
        var parser = CreateParser(new FakeLocalAIClient(modelResponse));

        var result = await parser.ParseAsync("something ambiguous");

        Assert.Null(result.Intent);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public async Task ParseAsync_PromptIncludesAssistantPolicy()
    {
        var client = new FakeLocalAIClient("""{"intent":"unknown","normalizedCommand":"","confidence":0}""");
        var parser = CreateParser(client, policy: "POLICY SENTINEL");

        await parser.ParseAsync("hello");

        Assert.Contains("Assistant policy:", client.LastPrompt);
        Assert.Contains("POLICY SENTINEL", client.LastPrompt);
    }

    [Fact]
    public async Task ParseAsync_PromptIncludesAllowedIntents()
    {
        var client = new FakeLocalAIClient("""{"intent":"unknown","normalizedCommand":"","confidence":0}""");
        var parser = CreateParser(client);

        await parser.ParseAsync("hello");

        Assert.Contains("Allowed intents:", client.LastPrompt);
        Assert.Contains("open_application", client.LastPrompt);
        Assert.Contains("open_url", client.LastPrompt);
        Assert.Contains("tool_discovery", client.LastPrompt);
        Assert.Contains("diagnostics", client.LastPrompt);
        Assert.Contains("confirmation", client.LastPrompt);
        Assert.Contains("general_conversation", client.LastPrompt);
        Assert.Contains("unsupported_action", client.LastPrompt);
        Assert.Contains("missing_capability", client.LastPrompt);
        Assert.Contains("unknown_input", client.LastPrompt);
        Assert.Contains("unknown", client.LastPrompt);
    }

    [Fact]
    public async Task ParseAsync_PromptExplainsClassificationMeanings()
    {
        var client = new FakeLocalAIClient("""{"intent":"unknown","normalizedCommand":"","confidence":0}""");
        var parser = CreateParser(client);

        await parser.ParseAsync("hello");

        Assert.Contains("The user asks for a reasonable capability Merlin does not currently have", client.LastPrompt);
        Assert.Contains("intentionally unsafe or disallowed", client.LastPrompt);
        Assert.Contains("The request is not understandable", client.LastPrompt);
        Assert.Contains("Safe conversation or a question", client.LastPrompt);
    }

    [Fact]
    public async Task ParseAsync_PromptIncludesToolMetadata()
    {
        var client = new FakeLocalAIClient("""{"intent":"unknown","normalizedCommand":"","confidence":0}""");
        var parser = CreateParser(client);

        await parser.ParseAsync("hello");

        Assert.Contains("Available tools:", client.LastPrompt);
        Assert.Contains("Open Application", client.LastPrompt);
        Assert.Contains("Opens allowlisted local applications.", client.LastPrompt);
        Assert.Contains("Open URL", client.LastPrompt);
        Assert.Contains("Tool Discovery", client.LastPrompt);
        Assert.Contains("General Conversation", client.LastPrompt);
    }

    private static LocalAIIntentParser CreateParser(
        ILocalAIClient client,
        bool enabled = true,
        double minimumConfidence = 0.70,
        string policy = "TEST POLICY")
    {
        return new LocalAIIntentParser(
            client,
            CreateLocalAIOptions(enabled, minimumConfidence),
            CreateToolRegistry(),
            new FakeAssistantPolicyProvider(policy),
            NullLogger<LocalAIIntentParser>.Instance,
            new LocalAIHealthService(
                client,
                CreateLocalAIOptions(enabled, minimumConfidence),
                NullLogger<LocalAIHealthService>.Instance));
    }

    private static IOptions<LocalAIOptions> CreateLocalAIOptions(
        bool enabled,
        double minimumConfidence)
    {
        return Options.Create(new LocalAIOptions
        {
            Enabled = enabled,
            Provider = "Ollama",
            Endpoint = "http://localhost:11434/api/generate",
            Model = "llama3.1:8b",
            MinimumConfidence = minimumConfidence,
            KeepAlive = "10m",
            WarmupOnStartup = true
        });
    }

    private static ToolRegistry CreateToolRegistry()
    {
        return new ToolRegistry(
        [
            new OpenApplicationTool(
                TestApplicationLaunchOptions.Create(),
                new FakeApplicationResolver(),
                new ConfirmationService(),
                new FakeProcessLauncher()),
            new OpenUrlTool(new FakeProcessLauncher()),
            new FakeToolDiscoveryTool(),
            new GeneralConversationTool(new FakeLocalAIChatService())
        ]);
    }

    private sealed class FakeLocalAIClient : ILocalAIClient
    {
        private readonly string? _response;

        public FakeLocalAIClient(string? response)
        {
            _response = response;
        }

        public int CallCount { get; private set; }

        public string LastPrompt { get; private set; } = string.Empty;

        public Task<string?> GenerateAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPrompt = prompt;
            return Task.FromResult(_response);
        }
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeApplicationResolver : IApplicationResolver
    {
        public string LastResolutionStatus => "Fake";

        public Task<ApplicationResolutionResult> ResolveAsync(
            string applicationName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ApplicationResolutionResult());
        }
    }

    private sealed class FakeToolDiscoveryTool : ITool
    {
        public string Name => "Tool Discovery";

        public string Description => "Lists available tools.";

        public IReadOnlyCollection<string> Examples { get; } = ["list tools"];

        public bool CanHandle(string command)
        {
            return string.Equals(command, "list tools", StringComparison.OrdinalIgnoreCase);
        }

        public Task<Merlin.Backend.Models.ToolResult> ExecuteAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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

    private sealed class FakeAssistantPolicyProvider : IAssistantPolicyProvider
    {
        private readonly string _policy;

        public FakeAssistantPolicyProvider(string policy)
        {
            _policy = policy;
        }

        public string GetPolicyText()
        {
            return _policy;
        }
    }
}
