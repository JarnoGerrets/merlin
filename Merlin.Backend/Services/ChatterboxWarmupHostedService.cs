using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Merlin.Backend.Services;

public sealed class ChatterboxWarmupHostedService : IHostedService
{
    private const string SilentWarmupText = "Ready when you are.";

    private readonly IWebHostEnvironment _environment;
    private readonly ChatterboxTtsProvider _ttsProvider;
    private readonly ChatterboxWorkerClient _workerClient;
    private readonly ILogger<ChatterboxWarmupHostedService> _logger;
    private readonly TtsOptions _options;

    public ChatterboxWarmupHostedService(
        IWebHostEnvironment environment,
        ChatterboxTtsProvider ttsProvider,
        ChatterboxWorkerClient workerClient,
        IOptions<TtsOptions> options,
        ILogger<ChatterboxWarmupHostedService> logger)
    {
        _environment = environment;
        _ttsProvider = ttsProvider;
        _workerClient = workerClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!string.Equals(_options.Provider, "chatterbox", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Chatterbox warmup skipped. TTS provider: {Provider}", _options.Provider);
            return;
        }

        if (!_options.ChatterboxKeepWarm)
        {
            _logger.LogInformation("Chatterbox keep-warm is disabled. Worker will load lazily.");
            return;
        }

        try
        {
            _logger.LogInformation(
                "Starting Chatterbox warmup. Model: {Model}. Device: {Device}. ReferenceVoicePath: {ReferenceVoicePath}.",
                _options.ChatterboxModel,
                _options.ChatterboxDevice,
                _options.ChatterboxReferenceVoicePath);
            await _workerClient.EnsureLoadedAsync(cancellationToken);
            await RunSilentSynthesisWarmupAsync(cancellationToken);
            await PrecacheCommonPhrasesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Chatterbox warmup failed. Chatterbox will load lazily.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task RunSilentSynthesisWarmupAsync(CancellationToken cancellationToken)
    {
        var referenceVoicePath = ResolvePath(_options.ChatterboxReferenceVoicePath);
        if (!File.Exists(referenceVoicePath))
        {
            _logger.LogWarning(
                "Chatterbox silent synthesis warmup skipped. Reference voice file not found: {ReferenceVoicePath}",
                referenceVoicePath);
            return;
        }

        _logger.LogInformation(
            "Chatterbox silent synthesis warmup started. Chars: {Chars}. ReferenceVoicePath: {ReferenceVoicePath}.",
            SilentWarmupText.Length,
            referenceVoicePath);

        var stopwatch = Stopwatch.StartNew();
        var result = await _workerClient.SynthesizeAsync(
            SilentWarmupText,
            referenceVoicePath,
            cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation(
            "Chatterbox silent synthesis warmup complete. ElapsedMs: {ElapsedMs}. GenerationMs: {GenerationMs}. ConditioningMs: {ConditioningMs}. AudioSeconds: {AudioSeconds}. BytesDiscarded: {Bytes}.",
            stopwatch.Elapsed.TotalMilliseconds,
            result.GenerationMs,
            result.ConditioningMs,
            result.DurationSeconds,
            result.Audio.Length);
    }

    private async Task PrecacheCommonPhrasesAsync(CancellationToken cancellationToken)
    {
        if (!_options.ChatterboxEnablePhraseCache)
        {
            _logger.LogInformation("Chatterbox common phrase precache skipped. Phrase cache disabled.");
            return;
        }

        _logger.LogInformation(
            "Chatterbox common phrase precache started. PhraseCount: {PhraseCount}.",
            ToolSpeechTemplates.CommonPhrases.Count);

        foreach (var phrase in ToolSpeechTemplates.CommonPhrases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var logContext = SpeechSynthesisLogContext.Push("tool.precache.common", true);
                await _ttsProvider.StreamSynthesizeAsync(
                    phrase,
                    (_, _) => Task.CompletedTask,
                    (_, _) => Task.CompletedTask,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Chatterbox common phrase precache failed. Phrase: {Phrase}",
                    phrase);
            }
        }

        _logger.LogInformation("Chatterbox common phrase precache complete.");
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, _environment.ContentRootPath);
    }
}
