using Merlin.Backend.Configuration;
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

public sealed class StatusToolTests
{
    [Theory]
    [InlineData("show status")]
    [InlineData("system status")]
    [InlineData("diagnostics")]
    [InlineData("health check")]
    [InlineData("merlin status")]
    public void CanHandle_WhenCommandIsDiagnostics_ReturnsTrue(string command)
    {
        var tool = CreateServiceProvider().GetRequiredService<IEnumerable<ITool>>()
            .OfType<StatusTool>()
            .Single();

        Assert.True(tool.CanHandle(command));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDiagnosticsInfo()
    {
        await using var serviceProvider = CreateServiceProvider();
        var runtimeState = serviceProvider.GetRequiredService<IRuntimeStateService>();
        runtimeState.IncrementActiveWebSocketConnections();
        runtimeState.IncrementRequestsProcessed();
        runtimeState.IncrementSuccessfulToolExecutions();
        runtimeState.IncrementFailedToolExecutions();
        runtimeState.RecordIntentParserUsed("RuleBasedIntentParser", "diagnostics");

        var tool = serviceProvider.GetRequiredService<IEnumerable<ITool>>()
            .OfType<StatusTool>()
            .Single();

        var result = await tool.ExecuteAsync("show status");

        Assert.True(result.Success);
        Assert.Equal("Merlin diagnostics", result.Message);
        Assert.Equal("Status", result.ToolName);
        Assert.Equal("diagnostics", result.Intent);
        Assert.NotNull(result.Diagnostics);
        Assert.Equal("1.0.0", result.Diagnostics.BackendVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostics.Uptime));
        Assert.False(result.Diagnostics.LocalAiEnabled);
        Assert.False(result.Diagnostics.LocalAiAvailable);
        Assert.True(result.Diagnostics.ChatToolEnabled);
        Assert.Equal("Ollama", result.Diagnostics.LocalAiProvider);
        Assert.Equal("llama3.1:8b", result.Diagnostics.LocalAiModel);
        Assert.Null(result.Diagnostics.LocalAiLastWarmupUtc);
        Assert.Null(result.Diagnostics.LocalAiLastError);
        Assert.Null(result.Diagnostics.LocalAiLastLatencyMs);
        Assert.Equal(7, result.Diagnostics.RegisteredToolCount);
        Assert.Contains("Open Application", result.Diagnostics.RegisteredTools);
        Assert.Contains("Open URL", result.Diagnostics.RegisteredTools);
        Assert.Contains("Tool Discovery", result.Diagnostics.RegisteredTools);
        Assert.Contains("System Resource", result.Diagnostics.RegisteredTools);
        Assert.Contains("Status", result.Diagnostics.RegisteredTools);
        Assert.Contains("Confirmation", result.Diagnostics.RegisteredTools);
        Assert.Contains("General Conversation", result.Diagnostics.RegisteredTools);
        Assert.Equal(1, result.Diagnostics.ActiveWebSocketConnections);
        Assert.Equal("RuleBasedIntentParser", result.Diagnostics.LastIntentParserUsed);
        Assert.Equal("Development", result.Diagnostics.Environment);
        Assert.Equal(1, result.Diagnostics.TotalRequestsProcessed);
        Assert.Equal(1, result.Diagnostics.TotalSuccessfulToolExecutions);
        Assert.Equal(1, result.Diagnostics.TotalFailedToolExecutions);
        Assert.Equal(0, result.Diagnostics.PendingConfirmations);
        Assert.Equal("02:00", result.Diagnostics.ConfirmationExpiryDuration);
        Assert.Equal("Configured, Trusted, StartMenu, PATH", result.Diagnostics.ResolverStatus);
        Assert.Equal(0, result.Diagnostics.TrustedApplicationCount);
        Assert.Equal(0, result.Diagnostics.TrustedCommandCount);
        Assert.False(string.IsNullOrWhiteSpace(result.Diagnostics.ConversationSessionId));
        Assert.Equal(0, result.Diagnostics.ConversationMessageCount);
        Assert.Equal(0, result.Diagnostics.ConversationSummaryLength);
        Assert.True(result.Diagnostics.ConversationSessionCreatedUtc <= DateTimeOffset.UtcNow);
        Assert.Equal(0, result.Diagnostics.MemoryCount);
        Assert.Equal(0, result.Diagnostics.MemoryCandidateCount);
        Assert.True(result.Diagnostics.MemoryStoreHealthy);
        Assert.True(result.Diagnostics.SystemResourceProviderEnabled);
    }

    [Fact]
    public void RuntimeStateService_TracksCountersAndConnections()
    {
        var runtimeState = new RuntimeStateService();

        runtimeState.IncrementActiveWebSocketConnections();
        runtimeState.IncrementActiveWebSocketConnections();
        runtimeState.DecrementActiveWebSocketConnections();
        runtimeState.IncrementRequestsProcessed();
        runtimeState.IncrementSuccessfulToolExecutions();
        runtimeState.IncrementFailedToolExecutions();

        Assert.Equal(1, runtimeState.ActiveWebSocketConnections);
        Assert.Equal(1, runtimeState.TotalRequestsProcessed);
        Assert.Equal(1, runtimeState.TotalSuccessfulToolExecutions);
        Assert.Equal(1, runtimeState.TotalFailedToolExecutions);
        Assert.True(runtimeState.Uptime >= TimeSpan.Zero);
    }

    [Fact]
    public void Metadata_IsExposedForDiscovery()
    {
        var tool = CreateServiceProvider().GetRequiredService<IEnumerable<ITool>>()
            .OfType<StatusTool>()
            .Single();

        Assert.Equal("Status", tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Contains("show status", tool.Examples);
        Assert.Contains("health check", tool.Examples);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestApplicationLaunchOptions.Create());
        services.AddSingleton(Options.Create(new LocalAIOptions { Enabled = false }));
        services.AddSingleton(TestCapabilityOptions.Create());
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment());
        services.AddSingleton<ILogger<StatusTool>>(NullLogger<StatusTool>.Instance);
        services.AddSingleton<ICapabilityClassifier, CapabilityClassifier>();
        services.AddSingleton<ILocalAIHealthService>(new FakeLocalAIHealthService());
        services.AddSingleton<ILocalAIChatService, FakeLocalAIChatService>();
        services.AddSingleton<IConversationSummaryStore, FakeConversationSummaryStore>();
        services.AddSingleton<IConversationSessionService, ConversationSessionService>();
        services.AddSingleton<ILongTermMemoryStore, FakeLongTermMemoryStore>();
        services.AddSingleton<IMemoryExtractionService, FakeMemoryExtractionService>();
        services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
        services.AddSingleton<ISystemResourceProvider, FakeSystemResourceProvider>();
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        services.AddSingleton<ITrustedApplicationStore, FakeTrustedApplicationStore>();
        services.AddSingleton<ITrustedCommandStore, FakeTrustedCommandStore>();
        services.AddSingleton<IProcessLauncher, FakeProcessLauncher>();
        services.AddSingleton<IApplicationResolver, ApplicationResolver>();
        services.AddSingleton<ITool, OpenApplicationTool>();
        services.AddSingleton<ITool, OpenUrlTool>();
        services.AddSingleton<ITool, ToolDiscoveryTool>();
        services.AddSingleton<ITool, SystemResourceTool>();
        services.AddSingleton<ITool, StatusTool>();
        services.AddSingleton<ITool, ConfirmationTool>();
        services.AddSingleton<ITool, GeneralConversationTool>();
        services.AddSingleton<ToolRegistry>();

        return services.BuildServiceProvider();
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
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
            return TimeZoneInfo.Local;
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
