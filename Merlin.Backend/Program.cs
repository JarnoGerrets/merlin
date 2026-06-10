using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Merlin.Backend.WebSocket;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApplicationLaunchOptions>(
    builder.Configuration.GetSection("ApplicationLaunch"));
builder.Services.Configure<LocalAIOptions>(
    builder.Configuration.GetSection("LocalAI"));

builder.Services.AddSingleton<IAIService, DummyAIService>();
builder.Services.AddSingleton<IAssistantPolicyProvider, AssistantPolicyProvider>();
builder.Services.AddSingleton<IIntentFallbackClassifier, IntentFallbackClassifier>();
builder.Services.AddSingleton<ICapabilityClassifier, CapabilityClassifier>();
builder.Services.AddSingleton<IResponsePolisher, ResponsePolisher>();
builder.Services.AddSingleton<IConversationSummaryStore, ConversationSummaryStore>();
builder.Services.AddSingleton<IConversationSessionService, ConversationSessionService>();
builder.Services.AddSingleton<ILongTermMemoryStore, LongTermMemoryStore>();
builder.Services.AddSingleton<IMemoryExtractionService, MemoryExtractionService>();
builder.Services.AddSingleton<TrustedCommandIntentParser>();
builder.Services.AddSingleton<RuleBasedIntentParser>();
builder.Services.AddSingleton<LocalAIIntentParser>();
builder.Services.AddSingleton<IIntentParser, HybridIntentParser>();
builder.Services.AddSingleton<ILocalAIHealthService, LocalAIHealthService>();
builder.Services.AddSingleton<ILocalAIChatService, LocalAIChatService>();
builder.Services.AddHostedService<LocalAIWarmupHostedService>();
builder.Services.AddHttpClient<ILocalAIClient, OllamaLocalAIClient>();
builder.Services.AddSingleton<IProcessLauncher, DefaultProcessLauncher>();
builder.Services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
builder.Services.AddSingleton<IApplicationResolver, ApplicationResolver>();
builder.Services.AddSingleton<IConfirmationService, ConfirmationService>();
builder.Services.AddSingleton<ITrustedApplicationStore, TrustedApplicationStore>();
builder.Services.AddSingleton<ITrustedCommandStore, TrustedCommandStore>();
builder.Services.AddSingleton<ITool, OpenApplicationTool>();
builder.Services.AddSingleton<ITool, OpenUrlTool>();
builder.Services.AddSingleton<ITool, ToolDiscoveryTool>();
builder.Services.AddSingleton<ITool, StatusTool>();
builder.Services.AddSingleton<ITool, ConfirmationTool>();
builder.Services.AddSingleton<ITool, GeneralConversationTool>();
builder.Services.AddSingleton<ToolRegistry>();
builder.Services.AddSingleton<CommandRouter>();
builder.Services.AddSingleton<WebSocketHandler>();

var app = builder.Build();

app.UseWebSockets();

app.Map("/ws", async context =>
{
    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
    await handler.HandleAsync(context);
});

app.Run();
