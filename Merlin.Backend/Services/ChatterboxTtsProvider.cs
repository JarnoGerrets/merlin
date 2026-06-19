using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class ChatterboxTtsProvider : IVoiceSynthesisService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ChatterboxTtsProvider> _logger;
    private readonly TtsOptions _options;
    private readonly ChatterboxTimingLogService _timingLog;
    private readonly ChatterboxWorkerClient _workerClient;
    private readonly IGpuWorkScheduler _gpuWorkScheduler;

    public ChatterboxTtsProvider(
        IOptions<TtsOptions> options,
        ChatterboxWorkerClient workerClient,
        IGpuWorkScheduler gpuWorkScheduler,
        IWebHostEnvironment environment,
        ChatterboxTimingLogService timingLog,
        ILogger<ChatterboxTtsProvider> logger)
    {
        _options = options.Value;
        _workerClient = workerClient;
        _gpuWorkScheduler = gpuWorkScheduler;
        _environment = environment;
        _timingLog = timingLog;
        _logger = logger;
    }

    public async Task StreamSynthesizeAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var referenceVoicePath = ResolvePath(_options.ChatterboxReferenceVoicePath);
        if (!File.Exists(referenceVoicePath))
        {
            throw new FileNotFoundException("Chatterbox reference voice file was not found.", referenceVoicePath);
        }

        var chunks = ChatterboxChunkPlanner.Plan(text.Trim(), _options);
        var stopwatch = Stopwatch.StartNew();
        var totalBytes = 0;
        var totalDurationSeconds = 0.0;
        var metadataSent = false;
        var requestId = Guid.NewGuid().ToString("n");
        var chunkTimings = new List<ChatterboxTimingLogService.ChunkTiming>();
        var error = string.Empty;

        try
        {
            _logger.LogInformation(
                "TTS provider selected: Chatterbox. RequestId: {RequestId}. Model: {Model}. RequestedDevice: {Device}. Exaggeration: {Exaggeration}. CfgWeight: {CfgWeight}. Temperature: {Temperature}. RepetitionPenalty: {RepetitionPenalty}. TopP: {TopP}. MinP: {MinP}. TextChars: {Chars}. ChunkCount: {ChunkCount}.",
                requestId,
                _options.ChatterboxModel,
                _options.ChatterboxDevice,
                _options.ChatterboxExaggeration,
                _options.ChatterboxCfgWeight,
                _options.ChatterboxTemperature,
                _options.ChatterboxRepetitionPenalty,
                _options.ChatterboxTopP,
                _options.ChatterboxMinP,
                text.Length,
                chunks.Count);
            _logger.LogInformation(
                "Chatterbox chunk plan. RequestId: {RequestId}. Chunks: {Chunks}. FirstChars: {FirstChars}. TotalChars: {TotalChars}.",
                requestId,
                chunks.Count,
                chunks.Count > 0 ? chunks[0].Length : 0,
                text.Length);

            await _workerClient.EnsureLoadedAsync(cancellationToken);

            for (var index = 0; index < chunks.Count; index++)
            {
                var chunk = chunks[index];
                var chunkStopwatch = Stopwatch.StartNew();
                var cacheKey = BuildCacheKey(chunk, referenceVoicePath);
                var cached = await TryReadCacheAsync(cacheKey, cancellationToken);
                var cacheStatus = cached is not null ? "hit" : "miss";
                ChatterboxSynthesisResult result;
                _logger.LogInformation("Chatterbox chunk start. RequestId: {RequestId}. Chunk: {Chunk}/{ChunkCount}. Chars: {Chars}.", requestId, index + 1, chunks.Count, chunk.Length);
                if (cached is not null)
                {
                    result = cached;
                    var context = SpeechSynthesisLogContext.Current;
                    _logger.LogInformation(
                        "Chatterbox phrase cache hit. RequestId: {RequestId}. Chunk: {Chunk}/{ChunkCount}. Chars: {Chars}. CacheKey: {CacheKey}. AudioCacheKey: {AudioCacheKey}. SpokenText: {SpokenText}. Replayable: {Replayable}.",
                        requestId,
                        index + 1,
                        chunks.Count,
                        chunk.Length,
                        context?.CacheKey ?? "none",
                        cacheKey,
                        chunk,
                        context?.Replayable);
                }
                else
                {
                    var context = SpeechSynthesisLogContext.Current;
                    _logger.LogInformation(
                        "Chatterbox phrase cache miss. RequestId: {RequestId}. Chunk: {Chunk}/{ChunkCount}. Chars: {Chars}. CacheKey: {CacheKey}. AudioCacheKey: {AudioCacheKey}. SpokenText: {SpokenText}. Replayable: {Replayable}.",
                        requestId,
                        index + 1,
                        chunks.Count,
                        chunk.Length,
                        context?.CacheKey ?? "none",
                        cacheKey,
                        chunk,
                        context?.Replayable);
                    result = await _gpuWorkScheduler.RunAsync(
                        "ChatterboxTtsChunk",
                        GpuWorkPriority.Low,
                        token => _workerClient.SynthesizeAsync(chunk, referenceVoicePath, token),
                        cancellationToken);
                    await TryWriteCacheAsync(cacheKey, result, cancellationToken);
                }

                if (!metadataSent)
                {
                    metadataSent = true;
                    await onMetadataAsync(
                        new VoiceSynthesisStreamMetadata
                        {
                            SampleRate = result.SampleRate,
                            Channels = result.Channels,
                            Format = result.Format
                        },
                        cancellationToken);
                }

                await onAudioAsync(result.Audio, cancellationToken);
                totalBytes += result.Audio.Length;
                totalDurationSeconds += result.DurationSeconds;
                chunkStopwatch.Stop();

                var chunkRealtimeFactor = result.DurationSeconds > 0.0
                    ? chunkStopwatch.Elapsed.TotalSeconds / result.DurationSeconds
                    : 0.0;
                chunkTimings.Add(new ChatterboxTimingLogService.ChunkTiming
                {
                    Index = index + 1,
                    Count = chunks.Count,
                    Chars = chunk.Length,
                    CacheStatus = cacheStatus,
                    GenerationMs = result.GenerationMs,
                    ConditioningMs = result.ConditioningMs,
                    TotalChunkMs = chunkStopwatch.Elapsed.TotalMilliseconds,
                    Bytes = result.Audio.Length,
                    DurationSeconds = result.DurationSeconds,
                    RealtimeFactor = chunkRealtimeFactor,
                    TextPreview = Preview(chunk)
                });

                _logger.LogInformation(
                    "Chatterbox chunk complete. RequestId: {RequestId}. Chunk: {Chunk}/{ChunkCount}. Cache: {Cache}. GenerationMs: {GenerationMs}. ConditioningMs: {ConditioningMs}. TotalChunkMs: {TotalChunkMs}. AudioBytes: {Bytes}. DurationSeconds: {DurationSeconds}. RealtimeFactor: {RealtimeFactor}.",
                    requestId,
                    index + 1,
                    chunks.Count,
                    cacheStatus,
                    result.GenerationMs,
                    result.ConditioningMs,
                    chunkStopwatch.Elapsed.TotalMilliseconds,
                    result.Audio.Length,
                    result.DurationSeconds,
                    chunkRealtimeFactor);
            }

            stopwatch.Stop();
            var realtimeFactor = totalDurationSeconds > 0.0
                ? stopwatch.Elapsed.TotalSeconds / totalDurationSeconds
                : 0.0;
            _logger.LogInformation(
                "Chatterbox TTS complete. RequestId: {RequestId}. Chars: {Chars}. Chunks: {Chunks}. Bytes: {Bytes}. TotalMs: {TotalMs}. AudioDurationSeconds: {AudioDurationSeconds}. RealtimeFactor: {RealtimeFactor}.",
                requestId,
                text.Length,
                chunks.Count,
                totalBytes,
                stopwatch.Elapsed.TotalMilliseconds,
                totalDurationSeconds,
                realtimeFactor);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            error = exception.Message;
            throw;
        }
        finally
        {
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
            }

            var realtimeFactor = totalDurationSeconds > 0.0
                ? stopwatch.Elapsed.TotalSeconds / totalDurationSeconds
                : 0.0;
            _timingLog.RecordSynthesis(new ChatterboxTimingLogService.SynthesisTiming
            {
                RequestId = requestId,
                Chars = text.Length,
                ChunkCount = chunks.Count,
                RequestedDevice = _options.ChatterboxDevice,
                TotalWallMs = stopwatch.Elapsed.TotalMilliseconds,
                TotalGenerationMs = chunkTimings.Sum(chunk => chunk.GenerationMs),
                TotalConditioningMs = chunkTimings.Sum(chunk => chunk.ConditioningMs),
                AudioDurationSeconds = totalDurationSeconds,
                RealtimeFactor = realtimeFactor,
                Bytes = totalBytes,
                CacheHits = chunkTimings.Count(chunk => string.Equals(chunk.CacheStatus, "hit", StringComparison.OrdinalIgnoreCase)),
                CacheMisses = chunkTimings.Count(chunk => string.Equals(chunk.CacheStatus, "miss", StringComparison.OrdinalIgnoreCase)),
                TextPreview = Preview(text),
                Ok = string.IsNullOrWhiteSpace(error),
                Error = string.IsNullOrWhiteSpace(error) ? null : error,
                Chunks = chunkTimings
            });
        }
    }

    private async Task<ChatterboxSynthesisResult?> TryReadCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        if (!_options.ChatterboxEnablePhraseCache)
        {
            return null;
        }

        var cachePath = CachePath(cacheKey);
        var metadataPath = $"{cachePath}.json";
        if (!File.Exists(cachePath) || !File.Exists(metadataPath))
        {
            return null;
        }

        var metadataText = await File.ReadAllTextAsync(metadataPath, cancellationToken);
        var metadata = JsonSerializer.Deserialize<CacheMetadata>(metadataText);
        if (metadata is null)
        {
            return null;
        }

        return new ChatterboxSynthesisResult
        {
            SampleRate = metadata.SampleRate,
            Channels = metadata.Channels,
            Format = metadata.Format,
            DurationSeconds = metadata.DurationSeconds,
            GenerationMs = 0.0,
            Audio = await File.ReadAllBytesAsync(cachePath, cancellationToken)
        };
    }

    private async Task TryWriteCacheAsync(string cacheKey, ChatterboxSynthesisResult result, CancellationToken cancellationToken)
    {
        if (!_options.ChatterboxEnablePhraseCache)
        {
            return;
        }

        Directory.CreateDirectory(ResolvePath(_options.ChatterboxCacheDir));
        var cachePath = CachePath(cacheKey);
        await File.WriteAllBytesAsync(cachePath, result.Audio, cancellationToken);
        await File.WriteAllTextAsync(
            $"{cachePath}.json",
            JsonSerializer.Serialize(new CacheMetadata
            {
                SampleRate = result.SampleRate,
                Channels = result.Channels,
                Format = result.Format,
                DurationSeconds = result.DurationSeconds
            }),
            cancellationToken);
    }

    private string CachePath(string cacheKey)
    {
        return Path.Combine(ResolvePath(_options.ChatterboxCacheDir), $"{cacheKey}.pcm");
    }

    private string BuildCacheKey(string text, string referenceVoicePath)
    {
        var referenceIdentity = $"{referenceVoicePath}|{GetFileTimestamp(referenceVoicePath)}";
        var input = string.Join(
            "|",
            "chatterbox",
            _options.ChatterboxModel,
            _options.ChatterboxDevice,
            referenceIdentity,
            text);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static long GetFileTimestamp(string path)
    {
        return File.Exists(path)
            ? File.GetLastWriteTimeUtc(path).Ticks
            : 0;
    }

    private static string Preview(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 160
            ? normalized
            : $"{normalized[..157]}...";
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, _environment.ContentRootPath);
    }

    private sealed class CacheMetadata
    {
        public int SampleRate { get; init; }

        public int Channels { get; init; }

        public string Format { get; init; } = "s16le";

        public double DurationSeconds { get; init; }
    }
}
