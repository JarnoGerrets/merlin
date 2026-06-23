using Merlin.Backend.Configuration;
using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Tools;

public sealed class StatusTool : ITool
{
    private const string BackendVersion = "1.0.0";
    private const string IntentName = "diagnostics";
    private readonly IWebHostEnvironment _environment;
    private readonly IConfirmationService _confirmationService;
    private readonly ILocalAIHealthService _localAIHealthService;
    private readonly ILogger<StatusTool> _logger;
    private readonly CapabilityOptions _capabilityOptions;
    private readonly CoreMemoryOptions _coreMemoryOptions;
    private readonly LocalAIOptions _localAIOptions;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly ISystemResourceProvider _systemResourceProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly TrustedRegistryOptions _trustedRegistryOptions;
    private readonly IApplicationResolver _applicationResolver;
    private readonly ITrustedApplicationStore _trustedApplicationStore;
    private readonly ITrustedCommandStore _trustedCommandStore;
    private readonly ITrustedUrlStore _trustedUrlStore;

    public StatusTool(
        IRuntimeStateService runtimeStateService,
        ISystemResourceProvider systemResourceProvider,
        ILocalAIHealthService localAIHealthService,
        IConfirmationService confirmationService,
        IApplicationResolver applicationResolver,
        ITrustedApplicationStore trustedApplicationStore,
        ITrustedCommandStore trustedCommandStore,
        ITrustedUrlStore trustedUrlStore,
        IServiceProvider serviceProvider,
        IOptions<LocalAIOptions> localAIOptions,
        IOptions<CoreMemoryOptions> coreMemoryOptions,
        IOptions<TrustedRegistryOptions> trustedRegistryOptions,
        IOptions<CapabilityOptions> capabilityOptions,
        IWebHostEnvironment environment,
        ILogger<StatusTool> logger)
    {
        _runtimeStateService = runtimeStateService;
        _systemResourceProvider = systemResourceProvider;
        _localAIHealthService = localAIHealthService;
        _confirmationService = confirmationService;
        _applicationResolver = applicationResolver;
        _trustedApplicationStore = trustedApplicationStore;
        _trustedCommandStore = trustedCommandStore;
        _trustedUrlStore = trustedUrlStore;
        _serviceProvider = serviceProvider;
        _localAIOptions = localAIOptions.Value;
        _coreMemoryOptions = coreMemoryOptions.Value;
        _trustedRegistryOptions = trustedRegistryOptions.Value;
        _capabilityOptions = MergeWithDefaults(capabilityOptions.Value);
        _environment = environment;
        _logger = logger;
    }

    public string Name => "Status";

    public string Description => "Provides Merlin health, diagnostics, and runtime information.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "show status",
        "system status",
        "diagnostics",
        "health check",
        "merlin status"
    ];

    public bool CanHandle(string command)
    {
        var normalizedCommand = command.Trim();

        return string.Equals(normalizedCommand, "show status", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "system status", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "diagnostics", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "health check", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "merlin status", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Diagnostics requested.");

        var toolRegistry = _serviceProvider.GetRequiredService<ToolRegistry>();
        var registeredTools = toolRegistry.GetTools()
            .Select(tool => tool.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var coreMemory = await GetCoreMemoryDiagnosticsAsync(cancellationToken);

        var diagnostics = new DiagnosticsInfo
        {
            BackendVersion = BackendVersion,
            Uptime = _runtimeStateService.Uptime.ToString(@"hh\:mm\:ss"),
            LocalAiEnabled = _localAIOptions.Enabled,
            LocalAiAvailable = _localAIHealthService.IsAvailable,
            ChatToolEnabled = registeredTools.Contains("General Conversation", StringComparer.OrdinalIgnoreCase),
            LocalAiProvider = _localAIOptions.Provider,
            LocalAiModel = _localAIOptions.Model,
            LocalAiLastWarmupUtc = _localAIHealthService.LastWarmupUtc,
            LocalAiLastError = _localAIHealthService.LastError,
            LocalAiLastLatencyMs = _localAIHealthService.LastLatencyMs,
            RegisteredToolCount = registeredTools.Length,
            RegisteredTools = registeredTools,
            ActiveWebSocketConnections = _runtimeStateService.ActiveWebSocketConnections,
            LastIntentParserUsed = _runtimeStateService.LastIntentParserUsed,
            Environment = _environment.EnvironmentName,
            CurrentTimeUtc = DateTimeOffset.UtcNow,
            TotalRequestsProcessed = _runtimeStateService.TotalRequestsProcessed,
            TotalSuccessfulToolExecutions = _runtimeStateService.TotalSuccessfulToolExecutions,
            TotalFailedToolExecutions = _runtimeStateService.TotalFailedToolExecutions,
            PendingConfirmations = _confirmationService.PendingCount,
            ConfirmationExpiryDuration = _confirmationService.ExpiryDuration.ToString(@"mm\:ss"),
            ResolverStatus = "Configured, Trusted, StartMenu, PATH",
            TrustedApplicationCount = _trustedApplicationStore.GetAll().Count,
            TrustedUrlCount = _trustedUrlStore.GetAll().Count,
            TrustedCommandCount = _trustedCommandStore.GetAll().Count,
            TrustedCommandRoutingEnabled = _trustedRegistryOptions.EnableTrustedCommandParser,
            TrustedCommandMappingsQuarantined = true,
            LastApplicationResolutionStatus = _applicationResolver.LastResolutionStatus,
            ConversationSessionId = string.Empty,
            ConversationMessageCount = 0,
            ConversationSummaryLength = 0,
            ConversationSessionCreatedUtc = default,
            ConversationSummaryCount = 0,
            LastConversationSummaryDate = null,
            ConversationSummaryStoreHealthy = false,
            MemoryCount = coreMemory.MemoryCount,
            MemoryMode = "sqlite-core",
            CoreDatabaseAvailable = coreMemory.DatabaseAvailable,
            CoreMemoryHealthy = coreMemory.Healthy,
            RequireCoreMemoryForConversation = _coreMemoryOptions.RequireCoreMemoryForConversation,
            CoreMemoryCount = coreMemory.MemoryCount,
            ActiveProfileFactCount = coreMemory.ActiveProfileFactCount,
            ConceptCount = coreMemory.ConceptCount,
            LegacyJsonMemoryCount = 0,
            LegacyJsonEnabled = false,
            DegradedFallbackEnabled = false,
            MemoryCandidateCount = 0,
            MemoryStoreHealthy = false,
            SupportedCapabilityCount = _capabilityOptions.CapabilityDomains.Count(domain => domain.IsImplemented),
            MissingCapabilityDetectionEnabled = true,
            CapabilityDomainCount = _capabilityOptions.CapabilityDomains.Count,
            ImplementedCapabilityCount = _capabilityOptions.CapabilityDomains.Count(domain => domain.IsImplemented),
            MissingCapabilityCount = _capabilityOptions.CapabilityDomains.Count(domain =>
                string.Equals(domain.SafetyLevel, "missing", StringComparison.OrdinalIgnoreCase)),
            UnsupportedCapabilityCount = _capabilityOptions.CapabilityDomains.Count(domain =>
                string.Equals(domain.SafetyLevel, "unsupported", StringComparison.OrdinalIgnoreCase)),
            SystemResourceProviderEnabled = _systemResourceProvider is not null
        };

        return new ToolResult
        {
            Success = true,
            Message = "Merlin diagnostics",
            ToolName = Name,
            Intent = IntentName,
            Diagnostics = diagnostics
        };
    }

    private async Task<CoreMemoryDiagnostics> GetCoreMemoryDiagnosticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<MerlinDbContext>();
            if (db is null)
            {
                return new CoreMemoryDiagnostics(false, false, 0, 0, 0);
            }

            var healthService = scope.ServiceProvider.GetService<ICoreMemoryHealthService>();
            var health = healthService is null
                ? new CoreMemoryHealthStatus { IsHealthy = false, FailureReason = "Core Memory health service unavailable." }
                : await healthService.CheckAsync(cancellationToken);
            var memoryCount = await db.Memories.AsNoTracking().CountAsync(cancellationToken);
            var profileFactCount = await db.UserProfileFacts.AsNoTracking()
                .CountAsync(fact => fact.ProfileId == UserProfileDefaults.ProfileId && fact.Status == UserProfileFactStatuses.Active, cancellationToken);
            var conceptCount = await db.Concepts.AsNoTracking().CountAsync(cancellationToken);
            return new CoreMemoryDiagnostics(health.DatabaseAvailable, health.IsHealthy, memoryCount, profileFactCount, conceptCount);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to collect Core Memory diagnostics.");
            return new CoreMemoryDiagnostics(false, false, 0, 0, 0);
        }
    }

    private static CapabilityOptions MergeWithDefaults(CapabilityOptions configuredOptions)
    {
        if (configuredOptions.CapabilityDomains.Count == 0)
        {
            configuredOptions.CapabilityDomains = CapabilityOptions.CreateDefault().CapabilityDomains;
        }

        return configuredOptions;
    }

    private sealed record CoreMemoryDiagnostics(
        bool DatabaseAvailable,
        bool Healthy,
        int MemoryCount,
        int ActiveProfileFactCount,
        int ConceptCount);
}
