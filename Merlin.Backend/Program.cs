using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Merlin.Backend.WebSocket;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApplicationLaunchOptions>(
    builder.Configuration.GetSection("ApplicationLaunch"));
builder.Services.Configure<LocalAIOptions>(
    builder.Configuration.GetSection("LocalAI"));
builder.Services.Configure<VoiceOptions>(
    builder.Configuration.GetSection("Voice"));
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

builder.Services.AddSingleton<IAIService, DummyAIService>();
builder.Services.AddSingleton<PythonVoiceService>();
builder.Services.AddSingleton<IVoiceService>(provider => provider.GetRequiredService<PythonVoiceService>());
builder.Services.AddSingleton<IVoiceWarmupService>(provider => provider.GetRequiredService<PythonVoiceService>());
builder.Services.AddSingleton<IAssistantPolicyProvider, AssistantPolicyProvider>();
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
builder.Services.AddHostedService<VoiceWarmupHostedService>();
builder.Services.AddHttpClient<ILocalAIClient, OllamaLocalAIClient>();
builder.Services.AddSingleton<ISystemResourceProvider, LocalSystemResourceProvider>();
builder.Services.AddSingleton<IProcessLauncher, DefaultProcessLauncher>();
builder.Services.AddSingleton<IRuntimeStateService, RuntimeStateService>();
builder.Services.AddSingleton<IApplicationResolver, ApplicationResolver>();
builder.Services.AddSingleton<IConfirmationService, ConfirmationService>();
builder.Services.AddSingleton<ITrustedApplicationStore, TrustedApplicationStore>();
builder.Services.AddSingleton<ITrustedCommandStore, TrustedCommandStore>();
builder.Services.AddSingleton<ITool, OpenApplicationTool>();
builder.Services.AddSingleton<ITool, OpenUrlTool>();
builder.Services.AddSingleton<ITool, ToolDiscoveryTool>();
builder.Services.AddSingleton<ITool, SystemResourceTool>();
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

app.MapPost("/api/voice/transcribe", async (
    HttpRequest request,
    ILogger<Program> logger,
    IVoiceService voiceService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var extension = request.Query.TryGetValue("extension", out var configuredExtension)
            ? configuredExtension.ToString()
            : ".wav";

        var transcription = await voiceService.TranscribeAsync(request.Body, extension, cancellationToken);
        return Results.Json(transcription);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Voice transcription failed.");
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/voice/warmup", async (
    ILogger<Program> logger,
    IVoiceWarmupService voiceWarmupService,
    CancellationToken cancellationToken) =>
{
    try
    {
        await voiceWarmupService.WarmupAsync(cancellationToken, force: true);
        return Results.Ok(new { warmed = true });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Voice warmup failed.");
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/voice/synthesize", async (
    Merlin.Backend.Models.SpeechSynthesisRequest request,
    ILogger<Program> logger,
    IVoiceService voiceService,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest("Text is required.");
        }

        var audio = await voiceService.SynthesizeAsync(request.Text, cancellationToken);
        return Results.File(audio, "audio/wav", "merlin-response.wav");
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Voice synthesis failed.");
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();
