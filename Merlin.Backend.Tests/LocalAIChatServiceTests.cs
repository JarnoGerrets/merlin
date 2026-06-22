using Merlin.Backend.Configuration;
using Merlin.Backend.Core.Conversation;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Infrastructure.Persistence.Repositories;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    public async Task GenerateResponseAsync_WhenCoreMemoryUnavailable_FailsClosedBeforeLlmOrLegacyMemory()
    {
        var client = new FakeLocalAIClient("This should not be used.");
        var service = CreateService(
            client,
            enabled: true,
            available: true,
            requireCoreMemory: true,
            coreMemoryHealthy: false);

        var result = await service.GenerateResponseAsync("tell me a joke");

        Assert.False(result.Success);
        Assert.Equal(LocalAIChatService.CoreMemoryUnavailableErrorCode, result.ErrorCode);
        Assert.Equal(LocalAIChatService.CoreMemoryUnavailableMessage, result.Message);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GenerateResponseAsync_WhenCoreMemoryHealthy_UsesNormalConversationPath()
    {
        var client = new FakeLocalAIClient("Core memory is healthy.");
        var service = CreateService(
            client,
            enabled: true,
            available: true,
            requireCoreMemory: true,
            coreMemoryHealthy: true);

        var result = await service.GenerateResponseAsync("tell me a joke");

        Assert.True(result.Success);
        Assert.Equal("Core memory is healthy.", result.Message);
        Assert.Equal(1, client.CallCount);
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
    public async Task GenerateResponseAsync_PromptExcludesLegacyJsonSessionAndIncludesCurrentMessage()
    {
        var client = new FakeLocalAIClient("The first tool opens applications.");
        var service = CreateService(
            client,
            enabled: true,
            available: true);

        await service.GenerateResponseAsync("Tell me more about the first one.");

        Assert.DoesNotContain("Conversation summary:", client.LastPrompt);
        Assert.DoesNotContain("Recent conversation messages:", client.LastPrompt);
        Assert.DoesNotContain("Relevant long-term memories:", client.LastPrompt);
        Assert.Contains("USER:\nTell me more about the first one.", client.LastPrompt);
        Assert.Contains("Tell me more about the first one.", client.LastPrompt);
    }

    [Fact]
    public async Task GenerateResponseAsync_DoesNotUseLegacyJsonSessionAsFallbackBrain()
    {
        var client = new FakeLocalAIClient("I am Merlin.");
        var service = CreateService(
            client,
            enabled: true,
            available: true);

        await service.GenerateResponseAsync("Who are you?");

        Assert.DoesNotContain("Conversation summary:", client.LastPrompt);
        Assert.DoesNotContain("Recent conversation messages:", client.LastPrompt);
    }

    [Fact]
    public async Task GenerateResponseAsync_DoesNotUseLegacyJsonLongTermMemory()
    {
        var client = new FakeLocalAIClient("Merlin uses Godot.");
        var service = CreateService(
            client,
            enabled: true,
            available: true);

        await service.GenerateResponseAsync("What frontend does Merlin use?");

        Assert.DoesNotContain("Relevant long-term memories:", client.LastPrompt);
        Assert.DoesNotContain("Merlin uses Godot frontend.", client.LastPrompt);
        Assert.DoesNotContain("localhost:11434", client.LastPrompt);
    }

    private static LocalAIChatService CreateService(
        ILocalAIClient client,
        bool enabled,
        bool available,
        bool warmupSucceeds = true,
        string policy = "TEST POLICY",
        LlmOptions? llmOptions = null,
        bool requireCoreMemory = false,
        bool coreMemoryHealthy = true)
    {
        var localOptions = Options.Create(new LocalAIOptions { Enabled = enabled });
        var configuredLlmOptions = Options.Create(llmOptions ?? new LlmOptions { Provider = "local" });
        var coreMemoryOptions = Options.Create(new CoreMemoryOptions
        {
            RequireCoreMemoryForConversation = requireCoreMemory
        });
        var healthService = new FakeLocalAIHealthService(available, warmupSucceeds);
        return new LocalAIChatService(
            new DeepInfraLlmProvider(new HttpClient(), configuredLlmOptions, NullLogger<DeepInfraLlmProvider>.Instance),
            new LocalLlmProvider(client, healthService, localOptions, NullLogger<LocalLlmProvider>.Instance),
            configuredLlmOptions,
            new FakeAssistantPolicyProvider(policy),
            NullLogger<LocalAIChatService>.Instance,
            coreMemoryOptions,
            requireCoreMemory ? CreateCoreMemoryScopeFactory(coreMemoryHealthy) : null);
    }

    private static IServiceScopeFactory CreateCoreMemoryScopeFactory(bool healthy)
    {
        if (healthy)
        {
            return CreateHealthyCoreMemoryScopeFactory();
        }

        var services = new ServiceCollection();
        services.AddSingleton<ICoreMemoryHealthService>(new FakeCoreMemoryHealthService(healthy));
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static IServiceScopeFactory CreateHealthyCoreMemoryScopeFactory()
    {
        var services = new ServiceCollection();
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        services.AddSingleton(connection);
        services.AddDbContext<MerlinDbContext>(options => options.UseSqlite(connection));
        services.AddLogging();
        services.AddScoped<IMemoryStore, EfMemoryStore>();
        services.AddScoped<IConceptStore, EfConceptStore>();
        services.AddScoped<IConversationStateStore, EfConversationStateStore>();
        services.AddScoped<IPromptCompilationStore, EfPromptCompilationStore>();
        services.AddScoped<IUserProfileFactStore, EfUserProfileFactStore>();
        services.AddSingleton<IConceptExtractionService, LocalConceptExtractionService>();
        services.AddScoped<FollowUpCueDetector>();
        services.AddScoped<ActiveConceptMerger>();
        services.AddScoped<TopicBoundaryDetector>();
        services.AddScoped<CurrentConversationMemoryService>();
        services.AddScoped<ExplicitMemoryRequestDetector>();
        services.AddScoped<MemoryTypeClassifier>();
        services.AddScoped<MemoryWriter>();
        services.AddScoped<UserProfileFactDetector>();
        services.AddScoped<UserProfileFactService>();
        services.AddScoped<TopicSummaryBuilder>();
        services.AddScoped<TopicImportanceScorer>();
        services.AddScoped<TopicClosingService>();
        services.AddScoped<ConceptGraphActivationService>();
        services.AddScoped<AssociativeRetriever>();
        services.AddSingleton<ITokenEstimator, SimpleTokenEstimator>();
        services.AddScoped<TokenBudgetService>();
        services.AddScoped<PromptRenderer>();
        services.AddScoped<PromptCompiler>();
        services.AddScoped<ICoreMemoryHealthService, CoreMemoryHealthService>();
        services.AddScoped<MemoryOrchestrator>();
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<MerlinDbContext>().Database.EnsureCreated();
        return serviceProvider.GetRequiredService<IServiceScopeFactory>();
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

    private sealed class FakeCoreMemoryHealthService : ICoreMemoryHealthService
    {
        private readonly bool _healthy;

        public FakeCoreMemoryHealthService(bool healthy)
        {
            _healthy = healthy;
        }

        public Task<CoreMemoryHealthStatus> CheckAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CoreMemoryHealthStatus
            {
                IsHealthy = _healthy,
                DatabaseAvailable = _healthy,
                CanQueryMemory = _healthy,
                CanQueryProfileFacts = _healthy,
                FailureReason = _healthy ? null : "test core memory unavailable"
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
