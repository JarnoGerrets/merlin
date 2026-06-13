using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Merlin.Backend.WebSocket;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApplicationLaunchOptions>(
    builder.Configuration.GetSection("ApplicationLaunch"));
builder.Services.Configure<LocalAIOptions>(
    builder.Configuration.GetSection("LocalAI"));
builder.Services.Configure<VoiceOptions>(
    builder.Configuration.GetSection("Voice"));
builder.Services.Configure<PiperOptions>(
    builder.Configuration.GetSection("Piper"));
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
builder.Services.AddSingleton<IVoiceTranscriptionService>(provider => provider.GetRequiredService<PythonVoiceService>());
builder.Services.AddSingleton<IVoiceSynthesisService, PiperVoiceService>();
builder.Services.AddSingleton<IAssistantSpeechPlaybackService, AssistantSpeechPlaybackService>();
builder.Services.AddSingleton<ISpeechPolicyService, SpeechPolicyService>();
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

app.MapPost("/api/voice/synthesize-stream", async (
    Merlin.Backend.Models.SpeechSynthesisRequest request,
    HttpResponse response,
    ILogger<Program> logger,
    IVoiceSynthesisService voiceService,
    CancellationToken cancellationToken) =>
{
    var started = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            await response.WriteAsync("Text is required.", cancellationToken);
            return;
        }

        response.ContentType = "application/octet-stream";
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Accel-Buffering"] = "no";

        logger.LogInformation(
            "Voice timing: synthesize stream endpoint start. Provider: Piper. Chars: {Chars}.",
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
            "Voice timing: synthesize stream endpoint complete. Provider: Piper. Chars: {Chars}. ElapsedMs: {ElapsedMs}.",
            request.Text.Length,
            started.Elapsed.TotalMilliseconds);
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Piper voice stream synthesis failed.");
        if (!response.HasStarted)
        {
            response.StatusCode = StatusCodes.Status500InternalServerError;
            await response.WriteAsync(exception.Message, cancellationToken);
        }
    }
});

app.Run();
