using Merlin.Backend.Configuration;
using Merlin.Backend.Core.Conversation;
using Merlin.Backend.Core.Memory.Search;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Infrastructure.Persistence.Repositories;
using Merlin.Backend.Infrastructure.Persistence.Seeding;
using Merlin.Backend.Infrastructure.TrustedRegistry;
using Merlin.Backend.Infrastructure.TrustedRegistry.Repositories;
using Merlin.Backend.Services;
using Merlin.Backend.Services.Acknowledgement;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Feedback;
using Merlin.Backend.Services.LiveUtterance;
using Merlin.Backend.Services.Motion;
using Merlin.Backend.Services.Motion.Profiles;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Services.IntentRouting;
using Merlin.Backend.Services.SpeechPresence;
using Merlin.Backend.Services.StreamingResponses;
using Merlin.Backend.Services.Vision;
using Merlin.Backend.Services.Web;
using Merlin.Backend.Tools;
using Merlin.Backend.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApplicationLaunchOptions>(
    builder.Configuration.GetSection("ApplicationLaunch"));
builder.Services.Configure<MerlinDbOptions>(
    builder.Configuration.GetSection("MerlinDatabase"));
builder.Services.Configure<CoreMemoryOptions>(
    builder.Configuration.GetSection("CoreMemory"));
builder.Services.Configure<TrustedRegistryOptions>(
    builder.Configuration.GetSection("TrustedRegistry"));
builder.Services.Configure<LocalAIOptions>(
    builder.Configuration.GetSection("LocalAI"));
builder.Services.Configure<LlmOptions>(
    builder.Configuration.GetSection("Llm"));
builder.Services.Configure<StreamingResponseOptions>(
    builder.Configuration.GetSection("StreamingResponses"));
builder.Services.Configure<WebSearchOptions>(
    builder.Configuration.GetSection("WebSearch"));
builder.Services.PostConfigure<WebSearchOptions>(options =>
{
    if (bool.TryParse(Environment.GetEnvironmentVariable("MERLIN_WEBSEARCH_ENABLED"), out var enabled))
    {
        options.Enabled = enabled;
    }

    options.Provider = Environment.GetEnvironmentVariable("MERLIN_WEBSEARCH_PROVIDER") ?? options.Provider;
    options.ApiKey = Environment.GetEnvironmentVariable("MERLIN_WEBSEARCH_API_KEY") ?? options.ApiKey;
    if (int.TryParse(Environment.GetEnvironmentVariable("MERLIN_WEBSEARCH_MAX_RESULTS"), out var maxResults))
    {
        options.MaxResults = maxResults;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("MERLIN_WEBSEARCH_TIMEOUT_SECONDS"), out var timeoutSeconds))
    {
        options.RequestTimeoutSeconds = timeoutSeconds;
    }
});
builder.Services.Configure<AcknowledgementSpeechOptions>(
    builder.Configuration.GetSection("AcknowledgementSpeech"));
builder.Services.Configure<ResponsiveFeedbackOptions>(
    builder.Configuration.GetSection("ResponsiveFeedback"));
builder.Services.Configure<InterruptionHandlingOptions>(
    builder.Configuration.GetSection("InterruptionHandling"));
builder.Services.Configure<BargeInOptions>(
    builder.Configuration.GetSection("BargeIn"));
builder.Services.Configure<BargeInDebugOptions>(
    builder.Configuration.GetSection("BargeIn"));
builder.Services.Configure<SpeechPresenceOptions>(
    builder.Configuration.GetSection("SpeechPresence"));
builder.Services.Configure<VoiceInputOptions>(
    builder.Configuration.GetSection("VoiceInput"));
builder.Services.Configure<ChatLogOptions>(
    builder.Configuration.GetSection("ChatLog"));
builder.Services.Configure<GpuSchedulingOptions>(
    builder.Configuration.GetSection("GpuScheduling"));
builder.Services.PostConfigure<LlmOptions>(options =>
{
    options.Provider = Environment.GetEnvironmentVariable("MERLIN_LLM_PROVIDER") ?? options.Provider;
    options.DeepInfraApiKey = Environment.GetEnvironmentVariable("DEEPINFRA_API_KEY") ?? options.DeepInfraApiKey;
    options.DeepInfraBaseUrl = Environment.GetEnvironmentVariable("DEEPINFRA_BASE_URL") ?? options.DeepInfraBaseUrl;
    options.DeepInfraModel = Environment.GetEnvironmentVariable("DEEPINFRA_MODEL") ?? options.DeepInfraModel;
    if (bool.TryParse(Environment.GetEnvironmentVariable("MERLIN_USE_LOCAL_LLM_FALLBACK"), out var useLocalFallback))
    {
        options.UseLocalFallback = useLocalFallback;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("DEEPINFRA_REQUEST_TIMEOUT_SECONDS"), out var requestTimeoutSeconds))
    {
        options.DeepInfraRequestTimeoutSeconds = requestTimeoutSeconds;
    }
});
builder.Services.PostConfigure<StreamingResponseOptions>(options =>
{
    if (bool.TryParse(Environment.GetEnvironmentVariable("MERLIN_STREAMING_RESPONSES_ENABLED"), out var enabled))
    {
        options.Enabled = enabled;
    }

    if (bool.TryParse(Environment.GetEnvironmentVariable("MERLIN_USE_DEEPINFRA_STREAMING"), out var useDeepInfraStreaming))
    {
        options.UseDeepInfraStreaming = useDeepInfraStreaming;
    }

    if (bool.TryParse(Environment.GetEnvironmentVariable("MERLIN_USE_SEGMENTED_TTS"), out var useSegmentedTts))
    {
        options.UseSegmentedTts = useSegmentedTts;
    }

    if (bool.TryParse(Environment.GetEnvironmentVariable("MERLIN_STREAMING_FALLBACK_TO_FULL_RESPONSE"), out var fallback))
    {
        options.FallbackToFullResponse = fallback;
    }
});
builder.Services.Configure<VoiceOptions>(
    builder.Configuration.GetSection("Voice"));
builder.Services.Configure<PiperOptions>(
    builder.Configuration.GetSection("Piper"));
builder.Services.Configure<TtsOptions>(
    builder.Configuration.GetSection("Tts"));
builder.Services.PostConfigure<TtsOptions>(options =>
{
    options.Provider = Environment.GetEnvironmentVariable("MERLIN_TTS_PROVIDER") ?? options.Provider;
    options.FallbackProvider = Environment.GetEnvironmentVariable("MERLIN_TTS_FALLBACK_PROVIDER") ?? options.FallbackProvider;
    options.ChatterboxDevice = Environment.GetEnvironmentVariable("CHATTERBOX_DEVICE") ?? options.ChatterboxDevice;
    options.ChatterboxReferenceVoicePath = Environment.GetEnvironmentVariable("CHATTERBOX_REFERENCE_VOICE_PATH") ?? options.ChatterboxReferenceVoicePath;
    options.ChatterboxCacheDir = Environment.GetEnvironmentVariable("CHATTERBOX_CACHE_DIR") ?? options.ChatterboxCacheDir;
    options.ChatterboxModel = Environment.GetEnvironmentVariable("CHATTERBOX_MODEL") ?? options.ChatterboxModel;
    options.ChatterboxPythonExecutable = Environment.GetEnvironmentVariable("CHATTERBOX_PYTHON_EXECUTABLE") ?? options.ChatterboxPythonExecutable;
    if (double.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_EXAGGERATION"), out var exaggeration))
    {
        options.ChatterboxExaggeration = exaggeration;
    }

    if (double.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_CFG_WEIGHT"), out var cfgWeight))
    {
        options.ChatterboxCfgWeight = cfgWeight;
    }

    if (double.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_TEMPERATURE"), out var temperature))
    {
        options.ChatterboxTemperature = temperature;
    }

    if (double.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_REPETITION_PENALTY"), out var repetitionPenalty))
    {
        options.ChatterboxRepetitionPenalty = repetitionPenalty;
    }

    if (double.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_TOP_P"), out var topP))
    {
        options.ChatterboxTopP = topP;
    }

    if (double.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_MIN_P"), out var minP))
    {
        options.ChatterboxMinP = minP;
    }

    if (bool.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_KEEP_WARM"), out var keepWarm))
    {
        options.ChatterboxKeepWarm = keepWarm;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_MAX_TEXT_CHARS_PER_CHUNK"), out var maxChars))
    {
        options.ChatterboxMaxTextCharsPerChunk = maxChars;
    }

    if (bool.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_ENABLE_INTERACTIVE_CHUNKING"), out var interactiveChunking))
    {
        options.ChatterboxEnableInteractiveChunking = interactiveChunking;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_FIRST_CHUNK_TARGET_CHARS"), out var firstChunkTarget))
    {
        options.ChatterboxFirstChunkTargetChars = firstChunkTarget;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_FIRST_CHUNK_MAX_CHARS"), out var firstChunkMax))
    {
        options.ChatterboxFirstChunkMaxChars = firstChunkMax;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_NEXT_CHUNK_TARGET_CHARS"), out var nextChunkTarget))
    {
        options.ChatterboxNextChunkTargetChars = nextChunkTarget;
    }

    if (int.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_NEXT_CHUNK_MAX_CHARS"), out var nextChunkMax))
    {
        options.ChatterboxNextChunkMaxChars = nextChunkMax;
    }

    if (bool.TryParse(Environment.GetEnvironmentVariable("CHATTERBOX_ENABLE_PHRASE_CACHE"), out var phraseCache))
    {
        options.ChatterboxEnablePhraseCache = phraseCache;
    }
});
builder.Services.Configure<CapabilityOptions>(options =>
{
    var configuredDomains = builder.Configuration
        .GetSection("CapabilityDomains")
        .Get<List<Merlin.Backend.Models.CapabilityDomain>>();

    if (configuredDomains is { Count: > 0 })
    {
        options.CapabilityDomains = configuredDomains;
    }
});

builder.Services.AddSingleton<MerlinDbPathResolver>();
builder.Services.AddDbContext<MerlinDbContext>((serviceProvider, options) =>
{
    var pathResolver = serviceProvider.GetRequiredService<MerlinDbPathResolver>();
    var dbPath = pathResolver.ResolveDatabasePath();

    options.UseSqlite($"Data Source={dbPath}");
});
builder.Services.AddHostedService<MerlinDbMigratorHostedService>();
builder.Services.AddScoped<IMemoryStore, EfMemoryStore>();
builder.Services.AddScoped<IConceptStore, EfConceptStore>();
builder.Services.AddScoped<IConversationStateStore, EfConversationStateStore>();
builder.Services.AddScoped<ITurnStateStore, EfTurnStateStore>();
builder.Services.AddScoped<IPromptCompilationStore, EfPromptCompilationStore>();
builder.Services.AddScoped<IUserProfileFactStore, EfUserProfileFactStore>();
builder.Services.AddScoped<MerlinConceptSeeder>();
builder.Services.AddScoped<IMemorySearchService, MemorySearchService>();
builder.Services.AddSingleton<IConceptExtractionService, LocalConceptExtractionService>();
builder.Services.AddSingleton<IRuntimeTopicSession, RuntimeTopicSession>();
builder.Services.AddHostedService<RuntimeTopicSessionStartupService>();
builder.Services.AddScoped<IConversationRuntimeState, ConversationRuntimeState>();
builder.Services.AddScoped<IAssistantTurnTracker, AssistantTurnTracker>();
builder.Services.AddScoped<IPromptCompilationLogger, PromptCompilationLogger>();
builder.Services.AddSingleton<ITokenEstimator, SimpleTokenEstimator>();
builder.Services.AddScoped<FollowUpCueDetector>();
builder.Services.AddScoped<ActiveConceptMerger>();
builder.Services.AddScoped<TopicBoundaryDetector>();
builder.Services.AddScoped<CurrentConversationMemoryService>();
builder.Services.AddScoped<ExplicitMemoryRequestDetector>();
builder.Services.AddScoped<MemoryTypeClassifier>();
builder.Services.AddScoped<MemoryWriter>();
builder.Services.AddScoped<UserProfileFactService>();
builder.Services.AddScoped<UserProfileFactDetector>();
builder.Services.AddScoped<TopicSummaryBuilder>();
builder.Services.AddScoped<TopicImportanceScorer>();
builder.Services.AddScoped<TopicClosingService>();
builder.Services.AddScoped<ConceptGraphActivationService>();
builder.Services.AddScoped<AssociativeRetriever>();
builder.Services.AddScoped<TokenBudgetService>();
builder.Services.AddScoped<PromptRenderer>();
builder.Services.AddScoped<PromptCompiler>();
builder.Services.AddScoped<MemoryOrchestrator>();
builder.Services.AddScoped<ICoreMemoryHealthService, CoreMemoryHealthService>();
builder.Services.AddScoped<MemoryDebugService>();

builder.Services.AddSingleton<TrustedRegistryDbPathResolver>();
builder.Services.AddDbContextFactory<TrustedRegistryDbContext>((serviceProvider, options) =>
{
    var pathResolver = serviceProvider.GetRequiredService<TrustedRegistryDbPathResolver>();
    var dbPath = pathResolver.ResolveDatabasePath();

    options.UseSqlite($"Data Source={dbPath}");
});
builder.Services.AddSingleton<TrustedRegistryLegacyJsonImporter>();
builder.Services.AddHostedService<TrustedRegistryMigratorHostedService>();

builder.Services.AddSingleton<ActiveSurfaceService>();
builder.Services.AddSingleton<IActiveSurfaceService>(provider => provider.GetRequiredService<ActiveSurfaceService>());
builder.Services.AddSingleton<IBrowserMediaCommandNormalizer, BrowserMediaCommandNormalizer>();

builder.Services.AddSingleton<IAIService, DummyAIService>();
builder.Services.AddSingleton<PythonVoiceService>();
builder.Services.AddSingleton<IVoiceTranscriptionService>(provider => provider.GetRequiredService<PythonVoiceService>());
builder.Services.AddSingleton<VoiceStreamSessionService>();
builder.Services.AddHostedService<PythonVoiceWarmupHostedService>();
builder.Services.AddSingleton<PiperVoiceService>();
builder.Services.AddSingleton<IGpuWorkScheduler, GpuWorkScheduler>();
builder.Services.AddSingleton<ChatterboxTimingLogService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ChatterboxTimingLogService>());
builder.Services.AddSingleton<ChatterboxWorkerClient>();
builder.Services.AddSingleton<ChatterboxTtsProvider>();
builder.Services.AddSingleton<ITtsTextSanitizer, TtsTextSanitizer>();
builder.Services.AddSingleton<ISpeakableTextSanitizer, SpeakableTextSanitizer>();
builder.Services.AddSingleton<IStreamedTextDetokenizer, StreamedTextDetokenizer>();
builder.Services.AddTransient<ISpeechSegmentQueue, SpeechSegmentQueue>();
builder.Services.AddSingleton<IVoiceSynthesisService, TtsRouter>();
builder.Services.AddHostedService<ChatterboxWarmupHostedService>();
builder.Services.AddSingleton<IBargeInDiagnosticsLogger, BargeInDiagnosticsLogger>();
builder.Services.AddSingleton<IBargeInDebugSnapshotService, BargeInDebugSnapshotService>();
builder.Services.AddSingleton<IInterruptionCaptureDiagnosticsWriter, InterruptionCaptureDiagnosticsWriter>();
builder.Services.AddSingleton<PlaybackReferenceTap>();
builder.Services.AddSingleton<IPlaybackReferenceTap>(provider => provider.GetRequiredService<PlaybackReferenceTap>());
builder.Services.AddSingleton<IAssistantPlaybackMonitor>(provider => provider.GetRequiredService<PlaybackReferenceTap>());
builder.Services.AddSingleton<IWindowsAecStatus, WindowsAecStatus>();
builder.Services.AddSingleton<DegradedAcousticEchoCancellationService>();
builder.Services.AddSingleton<WindowsWasapiAcousticEchoCancellationService>();
builder.Services.AddSingleton<WebRtcApmAcousticEchoCancellationService>();
builder.Services.AddSingleton<IAcousticEchoCancellationService>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<BargeInOptions>>().Value;
    if (string.Equals(options.AecProvider, "WebRtcApm", StringComparison.OrdinalIgnoreCase))
    {
        return serviceProvider.GetRequiredService<WebRtcApmAcousticEchoCancellationService>();
    }

    if (string.Equals(options.AecProvider, "WindowsWasapiAec", StringComparison.OrdinalIgnoreCase))
    {
        return serviceProvider.GetRequiredService<WindowsWasapiAcousticEchoCancellationService>();
    }

    return serviceProvider.GetRequiredService<DegradedAcousticEchoCancellationService>();
});
builder.Services.AddSingleton<IBargeInVadService, BargeInVadService>();
builder.Services.AddSingleton<ISpeakerDuckingService, SpeakerDuckingService>();
builder.Services.AddSingleton<ISelfSpeechGateDiagnosticsWriter, SelfSpeechGateDiagnosticsWriter>();
builder.Services.AddSingleton<ISelfSpeechSuppressionGate, SelfSpeechSuppressionGate>();
builder.Services.AddSingleton<SpeechPresenceDecisionLogService>();
builder.Services.AddSingleton<ISpeechPresenceDecisionLogSink>(provider => provider.GetRequiredService<SpeechPresenceDecisionLogService>());
builder.Services.AddSingleton<ISpeechPresenceDetector, SpeechPresenceDetector>();
builder.Services.AddSingleton<IFloorYieldController, FloorYieldController>();
builder.Services.AddSingleton<IConversationalInterruptionClassifier, ConversationalInterruptionClassifier>();
builder.Services.AddSingleton<IConversationalInterruptionCandidateFactory, ConversationalInterruptionCandidateFactory>();
builder.Services.AddSingleton<IRecentlyYieldedSpokenTurnStore, RecentlyYieldedSpokenTurnStore>();
builder.Services.AddSingleton<IActiveSpokenTurnResolver, ActiveSpokenTurnResolver>();
builder.Services.AddSingleton<IStopConfirmationPhraseSelector, StopConfirmationPhraseSelector>();
builder.Services.AddSingleton<ISpokenAnswerTracker, SpokenAnswerTracker>();
builder.Services.AddSingleton<IAnswerRecomposer, AnswerRecomposer>();
builder.Services.AddSingleton<IConversationFocusManager, ConversationFocusManager>();
builder.Services.AddSingleton<ILiveSpokenAnswerTrackingService, LiveSpokenAnswerTrackingService>();
builder.Services.AddSingleton<IInterruptionPlaybackPort, AssistantSpeechInterruptionPlaybackPort>();
builder.Services.AddSingleton<IInterruptionFeedbackPort, ResponsiveFeedbackInterruptionPort>();
builder.Services.AddSingleton<IInterruptionRequestRouterPort, NoOpInterruptionRequestRouterPort>();
builder.Services.AddSingleton<IInterruptionModelPort, LiveInterruptionModelPort>();
builder.Services.AddSingleton<IInterruptionSpeechOutputPort, AssistantSpeechInterruptionSpeechOutputPort>();
builder.Services.AddSingleton<IInterruptionOrchestrator, InterruptionOrchestrator>();
builder.Services.AddSingleton<ILiveInterruptionIntegrationService, LiveInterruptionIntegrationService>();
builder.Services.AddSingleton<IContinuousMicAudioBuffer, ContinuousMicAudioBuffer>();
builder.Services.AddSingleton<IBargeInTriggerBuffer, BargeInTriggerBuffer>();
builder.Services.AddSingleton<IBargeInSttService, BargeInSttService>();
builder.Services.AddSingleton<IInterruptionClassifier, InterruptionClassifier>();
builder.Services.Configure<LiveUtteranceGateOptions>(builder.Configuration.GetSection("LiveUtteranceGate"));
builder.Services.Configure<VisionOptions>(builder.Configuration.GetSection("Vision"));
builder.Services.Configure<BrowserWorkspaceOptions>(builder.Configuration.GetSection("BrowserWorkspace"));
builder.Services.Configure<WebDestinationOptions>(builder.Configuration.GetSection("WebDestinations"));
builder.Services.AddSingleton<MerlinAwakeStateService>();
builder.Services.AddSingleton<WakeResponsePhraseLibrary>();
builder.Services.AddSingleton<ILiveUtteranceGate, LiveUtteranceGate>();
builder.Services.AddSingleton<IBargeInCoordinator, BargeInCoordinator>();
builder.Services.AddSingleton<WindowsWasapiAecAudioCaptureService>();
builder.Services.AddSingleton<WebRtcApmBargeInAudioCaptureService>();
builder.Services.AddSingleton<IBargeInAudioCaptureService>(provider =>
{
    var options = provider.GetRequiredService<IOptions<BargeInOptions>>().Value;
    return string.Equals(options.AecProvider, "WebRtcApm", StringComparison.OrdinalIgnoreCase)
        ? provider.GetRequiredService<WebRtcApmBargeInAudioCaptureService>()
        : provider.GetRequiredService<WindowsWasapiAecAudioCaptureService>();
});
builder.Services.AddHostedService(provider => provider.GetRequiredService<WindowsWasapiAecAudioCaptureService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<WebRtcApmBargeInAudioCaptureService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SpeechPresenceDecisionLogService>());
builder.Services.AddHostedService<BargeInCoordinatorHostedService>();
builder.Services.AddSingleton<IAssistantSpeechPlaybackService, AssistantSpeechPlaybackService>();
builder.Services.AddSingleton<AssistantUiStateBroadcaster>();
builder.Services.AddSingleton<ISpeechPolicyService, SpeechPolicyService>();
builder.Services.AddSingleton<ILiveAssistantTurnService, LiveAssistantTurnService>();
builder.Services.AddSingleton<UiControlModeController>();
builder.Services.AddSingleton<VisionGestureEventRouter>();
builder.Services.AddSingleton<VisionSidecarClient>();
builder.Services.AddSingleton<IVisionSidecarHost, VisionSidecarHost>();
builder.Services.AddHostedService<VisionWarmupHostedService>();
builder.Services.AddSingleton<ICorrectionRequestBuilder, CorrectionRequestBuilder>();
builder.Services.AddSingleton<IAcknowledgementPhraseLibrary, AcknowledgementPhraseLibrary>();
builder.Services.AddSingleton<IAcknowledgementPolicy, AcknowledgementPolicy>();
builder.Services.AddSingleton<IAcknowledgementSpeechService, AcknowledgementSpeechService>();
builder.Services.AddSingleton<IRequestProgressSpeechService, RequestProgressSpeechService>();
builder.Services.AddSingleton<IFeedbackCardProvider, DefaultFeedbackCardProvider>();
builder.Services.AddSingleton<IFeedbackVectorBuilder, FeedbackVectorBuilder>();
builder.Services.AddSingleton<IFeedbackCooldownTracker, FeedbackCooldownTracker>();
builder.Services.AddSingleton<IFeedbackSelector, FeedbackSelector>();
builder.Services.AddSingleton<IFeedbackContextFactory, FeedbackContextFactory>();
builder.Services.AddSingleton<IInterruptionFeedbackAdapter, InterruptionFeedbackAdapter>();
builder.Services.AddSingleton<IResponsiveFeedbackOrchestrator, ResponsiveFeedbackOrchestrator>();
builder.Services.AddSingleton<IAssistantPolicyProvider, AssistantPolicyProvider>();
builder.Services.AddSingleton<ICapabilityClassifier, CapabilityClassifier>();
builder.Services.AddSingleton<TextNormalizer>();
builder.Services.AddSingleton<SpeechCommandNormalizer>();
builder.Services.AddSingleton<EmergencyIntentRouter>();
builder.Services.AddSingleton<DomainRouter>();
builder.Services.AddSingleton<CapabilityRegistry>();
builder.Services.AddSingleton<CapabilityRouter>();
builder.Services.AddSingleton<IIntentClassifier, DeterministicIntentClassifier>();
builder.Services.AddSingleton<ITargetScopeDetector, TargetScopeDetector>();
builder.Services.AddSingleton<ICapabilitySafetyClassifier, CapabilitySafetyClassifier>();
builder.Services.AddSingleton<ScopeAwareCapabilityRouter>();
builder.Services.AddSingleton<MerlinIntentRouter>();
builder.Services.AddSingleton<IResponsePolisher, ResponsePolisher>();
builder.Services.AddSingleton<IAssistantResponsePresentationFormatter, AssistantSpeechResponseFormatter>();
builder.Services.AddSingleton<TrustedCommandIntentParser>();
builder.Services.AddSingleton<RuleBasedIntentParser>();
builder.Services.AddSingleton<LocalAIIntentParser>();
builder.Services.AddSingleton<IIntentParser, HybridIntentParser>();
builder.Services.AddSingleton<ILocalAIHealthService, LocalAIHealthService>();
builder.Services.AddSingleton<ILocalAIChatService, LocalAIChatService>();
builder.Services.AddSingleton<LocalLlmProvider>();
builder.Services.AddHostedService<LocalAIWarmupHostedService>();
builder.Services.AddHttpClient<ILocalAIClient, OllamaLocalAIClient>();
builder.Services.AddHttpClient<DeepInfraLlmProvider>();
builder.Services.AddHttpClient<DeepInfraStreamingChatClient>();
builder.Services.AddSingleton<IWebSearchProvider, FakeWebSearchProvider>();
builder.Services.AddSingleton<WebSearchService>();
builder.Services.AddSingleton<ISystemResourceProvider, LocalSystemResourceProvider>();
builder.Services.AddSingleton<IProcessLauncher, DefaultProcessLauncher>();
builder.Services.AddSingleton<IBrowserPageSafetyGuard, BrowserPageSafetyGuard>();
builder.Services.AddSingleton<BrowserWorkspaceService>();
builder.Services.AddSingleton<IBrowserWorkspaceService>(sp => sp.GetRequiredService<BrowserWorkspaceService>());
builder.Services.AddSingleton<IBrowserPageSnapshotService>(sp => sp.GetRequiredService<BrowserWorkspaceService>());
builder.Services.AddSingleton<BrowserPointerMapper>();
builder.Services.AddSingleton<BrowserMotionOverlayModeService>();
builder.Services.AddSingleton<BrowserPinchClickController>();
builder.Services.AddSingleton<IMotionControlProfile, DashboardMotionProfile>();
builder.Services.AddSingleton<IMotionControlProfile, BrowserWorkspaceMotionProfile>();
builder.Services.AddSingleton<IMotionControlProfile, NeutralMotionProfile>();
builder.Services.AddSingleton<IMotionControlProfileRegistry, MotionControlProfileRegistry>();
builder.Services.AddSingleton<IMotionControlModeService, MotionControlModeService>();
builder.Services.AddSingleton<IWebDestinationParser, WebDestinationParser>();
builder.Services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
builder.Services.AddSingleton<IApplicationResolver, ApplicationResolver>();
builder.Services.AddSingleton<IConfirmationService, ConfirmationService>();
builder.Services.AddSingleton<IPendingInteractionService, PendingInteractionService>();
builder.Services.AddSingleton<ITrustedApplicationStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TrustedRegistryOptions>>().Value;
    return options.Enabled
        ? ActivatorUtilities.CreateInstance<EfTrustedApplicationStore>(serviceProvider)
        : ActivatorUtilities.CreateInstance<TrustedApplicationStore>(serviceProvider);
});
builder.Services.AddSingleton<ITrustedCommandStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TrustedRegistryOptions>>().Value;
    return options.Enabled
        ? ActivatorUtilities.CreateInstance<EfTrustedCommandStore>(serviceProvider)
        : ActivatorUtilities.CreateInstance<TrustedCommandStore>(serviceProvider);
});
builder.Services.AddSingleton<ITrustedUrlStore>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TrustedRegistryOptions>>().Value;
    return options.Enabled
        ? ActivatorUtilities.CreateInstance<EfTrustedUrlStore>(serviceProvider)
        : ActivatorUtilities.CreateInstance<TrustedUrlStore>(serviceProvider);
});
builder.Services.AddSingleton<ITool, OpenApplicationTool>();
builder.Services.AddSingleton<ITool, OpenUrlTool>();
builder.Services.AddSingleton<ITool, ToolDiscoveryTool>();
builder.Services.AddSingleton<ITool, SystemResourceTool>();
builder.Services.AddSingleton<ITool, StatusTool>();
builder.Services.AddSingleton<ITool, ConfirmationTool>();
builder.Services.AddSingleton<ITool, WakeMerlinTool>();
builder.Services.AddSingleton<ITool, EditBrowserMappingTool>();
builder.Services.AddSingleton<ITool, DeleteBrowserMappingTool>();
builder.Services.AddSingleton<ITool, DevVisualStateTool>();
builder.Services.AddSingleton<ITool, WebSearchTool>();
builder.Services.AddSingleton<ITool, GeneralConversationTool>();
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

app.UseWebSockets();

if (app.Environment.IsDevelopment())
{
    app.MapMemoryDebugEndpoints();
}

app.Map("/ws", async context =>
{
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
    await handler.HandleAsync(context);
});

app.MapPost("/api/voice/transcribe", async (
    HttpRequest request,
    ILogger<Program> logger,
    IVoiceTranscriptionService voiceService,
    CancellationToken cancellationToken) =>
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        var extension = request.Query.TryGetValue("extension", out var configuredExtension)
            ? configuredExtension.ToString()
            : ".wav";

        logger.LogInformation("Voice timing: upload/send received. ContentLength: {ContentLength}.", request.ContentLength);
        var transcription = await voiceService.TranscribeAsync(request.Body, extension, cancellationToken);
        logger.LogInformation(
            "Voice timing: transcribe endpoint complete. ElapsedMs: {ElapsedMs}.",
            started.Elapsed.TotalMilliseconds);
        return Results.Json(transcription);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Voice transcription failed.");
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/voice/stream-pcm-test", async (
    HttpResponse response,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    const int sampleRate = 24000;
    const int channels = 1;
    const int chunkDurationMs = 100;
    const int totalDurationMs = 5000;
    const double frequency = 440.0;

    response.ContentType = "application/octet-stream";
    response.Headers.CacheControl = "no-store";
    response.Headers["X-Accel-Buffering"] = "no";

    logger.LogInformation("Voice stream POC: request start.");
    var metadata = JsonSerializer.Serialize(new
    {
        sampleRate,
        channels,
        format = "s16le"
    }) + "\n";

    await response.WriteAsync(metadata, cancellationToken);
    await response.Body.FlushAsync(cancellationToken);
    logger.LogInformation("Voice stream POC: metadata sent.");

    var totalSamples = sampleRate * totalDurationMs / 1000;
    var samplesPerChunk = sampleRate * chunkDurationMs / 1000;
    var sentSamples = 0;
    var chunkIndex = 0;

    while (sentSamples < totalSamples && !cancellationToken.IsCancellationRequested)
    {
        var sampleCount = Math.Min(samplesPerChunk, totalSamples - sentSamples);
        var buffer = new byte[sampleCount * sizeof(short)];

        for (var index = 0; index < sampleCount; index++)
        {
            var sampleNumber = sentSamples + index;
            var fadeIn = Math.Min(1.0, sampleNumber / (sampleRate * 0.05));
            var fadeOut = Math.Min(1.0, (totalSamples - sampleNumber) / (sampleRate * 0.05));
            var envelope = Math.Min(fadeIn, fadeOut);
            var value = Math.Sin(2.0 * Math.PI * frequency * sampleNumber / sampleRate) * 0.22 * envelope;
            var pcm = (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
            buffer[index * 2] = (byte)(pcm & 0xff);
            buffer[index * 2 + 1] = (byte)((pcm >> 8) & 0xff);
        }

        await response.Body.WriteAsync(buffer, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);

        if (chunkIndex == 0)
        {
            logger.LogInformation("Voice stream POC: first PCM chunk sent. Bytes: {Bytes}.", buffer.Length);
        }

        sentSamples += sampleCount;
        chunkIndex++;
        await Task.Delay(chunkDurationMs, cancellationToken);
    }

    logger.LogInformation("Voice stream POC: stream complete. Chunks: {Chunks}. Samples: {Samples}.", chunkIndex, sentSamples);
});

app.MapGet("/api/tts/health", async (
    IOptions<TtsOptions> options,
    ChatterboxWorkerClient chatterboxWorker,
    IWebHostEnvironment environment,
    CancellationToken cancellationToken) =>
{
    var ttsOptions = options.Value;
    var referencePath = Path.IsPathRooted(ttsOptions.ChatterboxReferenceVoicePath)
        ? ttsOptions.ChatterboxReferenceVoicePath
        : Path.GetFullPath(ttsOptions.ChatterboxReferenceVoicePath, environment.ContentRootPath);

    if (!string.Equals(ttsOptions.Provider, "chatterbox", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Ok(new
        {
            provider = ttsOptions.Provider,
            fallbackProvider = ttsOptions.FallbackProvider,
            chatterboxEnabled = false,
            referenceVoiceExists = File.Exists(referencePath)
        });
    }

    try
    {
        await chatterboxWorker.EnsureLoadedAsync(cancellationToken);
        return Results.Ok(new
        {
            provider = ttsOptions.Provider,
            fallbackProvider = ttsOptions.FallbackProvider,
            chatterboxEnabled = true,
            model = ttsOptions.ChatterboxModel,
            device = ttsOptions.ChatterboxDevice,
            referenceVoicePath = referencePath,
            referenceVoiceExists = File.Exists(referencePath)
        });
    }
    catch (Exception exception)
    {
        return Results.Problem(
            title: "Chatterbox health check failed.",
            detail: exception.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/api/voice/synthesize-stream", async (
    Merlin.Backend.Models.SpeechSynthesisRequest request,
    HttpResponse response,
    ILogger<Program> logger,
    ChatterboxTimingLogService timingLog,
    IVoiceSynthesisService voiceService,
    CancellationToken cancellationToken) =>
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    var error = string.Empty;
    try
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            error = "Text is required.";
            response.StatusCode = StatusCodes.Status400BadRequest;
            await response.WriteAsync("Text is required.", cancellationToken);
            return;
        }

        response.ContentType = "application/octet-stream";
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Accel-Buffering"] = "no";

        logger.LogInformation(
            "Voice timing: synthesize stream endpoint start. Chars: {Chars}.",
            request.Text.Length);

        await voiceService.StreamSynthesizeAsync(
            request.Text,
            async (metadata, token) =>
            {
                var metadataLine = JsonSerializer.Serialize(new
                {
                    sampleRate = metadata.SampleRate,
                    channels = metadata.Channels,
                    format = metadata.Format
                }) + "\n";
                await response.WriteAsync(metadataLine, token);
                await response.Body.FlushAsync(token);
            },
            async (audio, token) =>
            {
                await response.Body.WriteAsync(audio, token);
                await response.Body.FlushAsync(token);
            },
            cancellationToken);

        logger.LogInformation(
            "Voice timing: synthesize stream endpoint complete. Chars: {Chars}. ElapsedMs: {ElapsedMs}.",
            request.Text.Length,
            started.Elapsed.TotalMilliseconds);
    }
    catch (Exception exception)
    {
        error = exception.Message;
        logger.LogError(exception, "Voice stream synthesis failed.");
        if (!response.HasStarted)
        {
            response.StatusCode = StatusCodes.Status500InternalServerError;
            await response.WriteAsync(exception.Message, cancellationToken);
        }
    }
    finally
    {
        started.Stop();
        timingLog.RecordEndpoint(new ChatterboxTimingLogService.EndpointTiming
        {
            Endpoint = "/api/voice/synthesize-stream",
            Chars = request.Text?.Length ?? 0,
            ElapsedMs = started.Elapsed.TotalMilliseconds,
            ResponseStarted = response.HasStarted,
            Ok = string.IsNullOrWhiteSpace(error),
            Error = string.IsNullOrWhiteSpace(error) ? null : error
        });
    }
});

app.Run();
