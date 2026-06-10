using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.AspNetCore.Hosting;
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
    private readonly LocalAIOptions _localAIOptions;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly ICapabilityClassifier _capabilityClassifier;
    private readonly IServiceProvider _serviceProvider;
    private readonly IApplicationResolver _applicationResolver;
    private readonly IConversationSessionService _conversationSessionService;
    private readonly IConversationSummaryStore _conversationSummaryStore;
    private readonly ILongTermMemoryStore _memoryStore;
    private readonly IMemoryExtractionService _memoryExtractionService;
    private readonly ITrustedApplicationStore _trustedApplicationStore;
    private readonly ITrustedCommandStore _trustedCommandStore;

    public StatusTool(
        IRuntimeStateService runtimeStateService,
        ICapabilityClassifier capabilityClassifier,
        ILocalAIHealthService localAIHealthService,
        IConfirmationService confirmationService,
        IApplicationResolver applicationResolver,
        IConversationSessionService conversationSessionService,
        IConversationSummaryStore conversationSummaryStore,
        ILongTermMemoryStore memoryStore,
        IMemoryExtractionService memoryExtractionService,
        ITrustedApplicationStore trustedApplicationStore,
        ITrustedCommandStore trustedCommandStore,
        IServiceProvider serviceProvider,
        IOptions<LocalAIOptions> localAIOptions,
        IWebHostEnvironment environment,
        ILogger<StatusTool> logger)
    {
        _runtimeStateService = runtimeStateService;
        _capabilityClassifier = capabilityClassifier;
        _localAIHealthService = localAIHealthService;
        _confirmationService = confirmationService;
        _applicationResolver = applicationResolver;
        _conversationSessionService = conversationSessionService;
        _conversationSummaryStore = conversationSummaryStore;
        _memoryStore = memoryStore;
        _memoryExtractionService = memoryExtractionService;
        _trustedApplicationStore = trustedApplicationStore;
        _trustedCommandStore = trustedCommandStore;
        _serviceProvider = serviceProvider;
        _localAIOptions = localAIOptions.Value;
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

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Diagnostics requested.");

        var toolRegistry = _serviceProvider.GetRequiredService<ToolRegistry>();
        var registeredTools = toolRegistry.GetTools()
            .Select(tool => tool.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var conversationSession = _conversationSessionService.CurrentSession;
        var conversationSummaries = _conversationSummaryStore.GetAll();

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
            TrustedCommandCount = _trustedCommandStore.GetAll().Count,
            LastApplicationResolutionStatus = _applicationResolver.LastResolutionStatus,
            ConversationSessionId = conversationSession.SessionId,
            ConversationMessageCount = conversationSession.Messages.Count,
            ConversationSummaryLength = conversationSession.RunningSummary.Length,
            ConversationSessionCreatedUtc = conversationSession.CreatedAtUtc,
            ConversationSummaryCount = conversationSummaries.Count,
            LastConversationSummaryDate = conversationSummaries
                .OrderByDescending(summary => summary.LastUpdatedUtc)
                .FirstOrDefault()
                ?.LastUpdatedUtc,
            ConversationSummaryStoreHealthy = _conversationSummaryStore.IsHealthy,
            MemoryCount = _memoryStore.GetAll().Count,
            MemoryCandidateCount = _memoryExtractionService.PendingCandidates.Count,
            MemoryStoreHealthy = _memoryStore.IsHealthy,
            SupportedCapabilityCount = _capabilityClassifier.SupportedCapabilityCount,
            MissingCapabilityDetectionEnabled = _capabilityClassifier.MissingCapabilityDetectionEnabled
        };

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = "Merlin diagnostics",
            ToolName = Name,
            Intent = IntentName,
            Diagnostics = diagnostics
        });
    }
}
