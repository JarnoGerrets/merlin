using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LocalAIChatServiceTests
{
    [Fact]
    public async Task GenerateResponseAsync_WhenLocalAIEnabledAndAvailable_ReturnsModelResponse()
    {
        var client = new FakeLocalAIClient("Hello from Merlin.");
        var service = CreateService(client, enabled: true, available: true);

        var result = await service.GenerateResponseAsync("who are you");

        Assert.True(result.Success);
        Assert.Equal("Hello from Merlin.", result.Message);
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenLocalAIIsDisabled_ReturnsUnavailable()
    {
        var service = CreateService(new FakeLocalAIClient("unused"), enabled: false, available: true);

        var result = await service.GenerateResponseAsync("tell me a joke");

        Assert.False(result.Success);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, result.ErrorCode);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, result.Message);
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenLocalAIIsUnavailable_ReturnsUnavailable()
    {
        var service = CreateService(new FakeLocalAIClient("unused"), enabled: true, available: false, warmupSucceeds: false);

        var result = await service.GenerateResponseAsync("tell me a joke");

        Assert.False(result.Success);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenDeepInfraTurnIsCancelled_DoesNotFallBackToLocalAI()
    {
        var client = new FakeLocalAIClient("Local answer should not be used.");
        var service = CreateService(
            client,
            enabled: true,
            available: true,
            llmOptions: new LlmOptions
            {
                Provider = "deepinfra",
                UseLocalFallback = true
            });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateResponseAsync("tell me something slow", cancellation.Token));

        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenLocalAIIsCold_WarmsOnDemand()
    {
        var service = CreateService(new FakeLocalAIClient("Local answer."), enabled: true, available: false, warmupSucceeds: true);

        var result = await service.GenerateResponseAsync("tell me a joke");

        Assert.True(result.Success);
        Assert.Equal("Local answer.", result.Message);
    }

    [Fact]
    public async Task GenerateResponseAsync_PromptIncludesConstitutionText()
    {
        var client = new FakeLocalAIClient("Hello.");
        var service = CreateService(client, enabled: true, available: true, policy: "CONSTITUTION SENTINEL");

        await service.GenerateResponseAsync("how do you work");

        Assert.Contains("CONSTITUTION SENTINEL", client.LastPrompt);
        Assert.Contains("You are Merlin.", client.LastPrompt);
        Assert.Contains("Follow this policy silently.", client.LastPrompt);
        Assert.Contains("must not mention the Merlin Constitution", client.LastPrompt);
        Assert.Contains("You must not execute actions.", client.LastPrompt);
        Assert.Contains("USER:", client.LastPrompt);
        Assert.Contains("how do you work", client.LastPrompt);
    }

    [Fact]
    public async Task GenerateResponseAsync_PromptIncludesRunningSummaryRecentMessagesAndCurrentMessage()
    {
        var client = new FakeLocalAIClient("The first tool opens applications.");
        var sessionService = new ConversationSessionService(new FakeConversationSummaryStore());
        sessionService.UpdateRunningSummary("User asked about Merlin tools.");
        sessionService.AddUserMessage("What tools do you have?");
        sessionService.AddAssistantMessage("Open Application is listed first.");
        var service = CreateService(
            client,
            enabled: true,
            available: true,
            sessionService: sessionService);

        await service.GenerateResponseAsync("Tell me more about the first one.");

        Assert.Contains("Conversation summary:", client.LastPrompt);
        Assert.Contains("User asked about Merlin tools.", client.LastPrompt);
        Assert.Contains("Recent conversation messages:", client.LastPrompt);
        Assert.Contains("USER:\nWhat tools do you have?", client.LastPrompt);
        Assert.Contains("ASSISTANT:\nOpen Application is listed first.", client.LastPrompt);
        Assert.Contains("USER:\nTell me more about the first one.", client.LastPrompt);
        Assert.Contains("Tell me more about the first one.", client.LastPrompt);
    }

    [Fact]
    public async Task GenerateResponseAsync_StoresUserAndAssistantMessages()
    {
        var sessionService = new ConversationSessionService(new FakeConversationSummaryStore());
        var service = CreateService(
            new FakeLocalAIClient("I am Merlin."),
            enabled: true,
            available: true,
            sessionService: sessionService);

        await service.GenerateResponseAsync("Who are you?");

        var messages = sessionService.GetRecentMessages();
        Assert.Equal(2, messages.Count);
        Assert.Equal("User", messages[0].Role);
        Assert.Equal("Who are you?", messages[0].Content);
        Assert.Equal("Assistant", messages[1].Role);
        Assert.Equal("I am Merlin.", messages[1].Content);
    }

    [Fact]
    public async Task GenerateResponseAsync_PromptIncludesOnlyRelevantMemories()
    {
        var client = new FakeLocalAIClient("Merlin uses Godot.");
        var memoryStore = new FakeLongTermMemoryStore();
        memoryStore.SaveMemory(new MemoryRecord
        {
            Category = "project",
            Key = "frontend",
            Value = "Merlin uses Godot frontend.",
            Source = "test",
            Confidence = 0.9
        });
        memoryStore.SaveMemory(new MemoryRecord
        {
            Category = "fact",
            Key = "ollama_endpoint",
            Value = "Ollama hosts LocalAI on localhost:11434.",
            Source = "test",
            Confidence = 0.9
        });
        var service = CreateService(
            client,
            enabled: true,
            available: true,
            memoryStore: memoryStore);

        await service.GenerateResponseAsync("What frontend does Merlin use?");

        Assert.Contains("Relevant long-term memories:", client.LastPrompt);
        Assert.Contains("Merlin uses Godot frontend.", client.LastPrompt);
        Assert.DoesNotContain("localhost:11434", client.LastPrompt);
    }

    private static LocalAIChatService CreateService(
        ILocalAIClient client,
        bool enabled,
        bool available,
        bool warmupSucceeds = true,
        string policy = "TEST POLICY",
        IConversationSessionService? sessionService = null,
        ILongTermMemoryStore? memoryStore = null,
        LlmOptions? llmOptions = null)
    {
        var localOptions = Options.Create(new LocalAIOptions { Enabled = enabled });
        var configuredLlmOptions = Options.Create(llmOptions ?? new LlmOptions { Provider = "local" });
        var healthService = new FakeLocalAIHealthService(available, warmupSucceeds);
        return new LocalAIChatService(
            new DeepInfraLlmProvider(new HttpClient(), configuredLlmOptions, NullLogger<DeepInfraLlmProvider>.Instance),
            new LocalLlmProvider(client, healthService, localOptions, NullLogger<LocalLlmProvider>.Instance),
            configuredLlmOptions,
            new FakeAssistantPolicyProvider(policy),
            sessionService ?? new ConversationSessionService(new FakeConversationSummaryStore()),
            memoryStore ?? new FakeLongTermMemoryStore(),
            NullLogger<LocalAIChatService>.Instance);
    }

    private sealed class FakeLocalAIClient : ILocalAIClient
    {
        private readonly string? _response;

        public FakeLocalAIClient(string? response)
        {
            _response = response;
        }

        public string LastPrompt { get; private set; } = string.Empty;

        public int CallCount { get; private set; }

        public Task<string?> GenerateAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            LastPrompt = prompt;
            return Task.FromResult(_response);
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

    private sealed class FakeLocalAIHealthService : ILocalAIHealthService
    {
        private readonly bool _warmupSucceeds;

        public FakeLocalAIHealthService(bool isAvailable, bool warmupSucceeds)
        {
            IsAvailable = isAvailable;
            _warmupSucceeds = warmupSucceeds;
        }

        public bool IsEnabled => true;

        public bool IsAvailable { get; private set; }

        public DateTimeOffset? LastWarmupUtc => null;

        public string? LastError => null;

        public long? LastLatencyMs => null;

        public Task WarmupAsync(CancellationToken cancellationToken = default)
        {
            IsAvailable = _warmupSucceeds;
            return Task.CompletedTask;
        }

        public void MarkDisabled()
        {
            IsAvailable = false;
        }

        public void MarkAvailable(long latencyMs)
        {
            IsAvailable = true;
        }

        public void MarkUnavailable(string error, long? latencyMs = null)
        {
            IsAvailable = false;
        }
    }
}
