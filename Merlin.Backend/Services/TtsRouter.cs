using System.Diagnostics;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class TtsRouter : IVoiceSynthesisService
{
    private readonly ChatterboxTtsProvider _chatterboxProvider;
    private readonly ILogger<TtsRouter> _logger;
    private readonly PiperVoiceService _piperProvider;
    private readonly ChatterboxTimingLogService _timingLog;
    private readonly TtsOptions _options;
    private readonly object _circuitBreakerLock = new();
    private DateTimeOffset? _chatterboxUnhealthyUntilUtc;
    private int _chatterboxConsecutiveFailures;

    public TtsRouter(
        ChatterboxTtsProvider chatterboxProvider,
        PiperVoiceService piperProvider,
        IOptions<TtsOptions> options,
        ChatterboxTimingLogService timingLog,
        ILogger<TtsRouter> logger)
    {
        _chatterboxProvider = chatterboxProvider;
        _piperProvider = piperProvider;
        _timingLog = timingLog;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StreamSynthesizeAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
        CancellationToken cancellationToken) =>
        await StreamSynthesizeChunksAsync(
            text,
            onMetadataAsync,
            (chunk, token) => onAudioAsync(chunk.Audio, token),
            cancellationToken);

    public async Task StreamSynthesizeChunksAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<VoiceSynthesisAudioChunk, CancellationToken, Task> onAudioChunkAsync,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var provider = _options.Provider.Trim().ToLowerInvariant();
        if (provider == "piper")
        {
            _logger.LogInformation("TTS provider selected: Piper.");
            try
            {
                await _piperProvider.StreamSynthesizeChunksAsync(text, onMetadataAsync, onAudioChunkAsync, cancellationToken);
            }
            finally
            {
                stopwatch.Stop();
                _timingLog.RecordRouter(new ChatterboxTimingLogService.RouterTiming
                {
                    ConfiguredProvider = _options.Provider,
                    SelectedProvider = "piper",
                    UsedFallback = false,
                    Reason = "configured provider",
                    Chars = text.Length,
                    ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
                });
            }
            return;
        }

        if (provider != "chatterbox")
        {
            _logger.LogWarning("Unknown TTS provider '{Provider}'. Using Piper.", _options.Provider);
            try
            {
                await _piperProvider.StreamSynthesizeChunksAsync(text, onMetadataAsync, onAudioChunkAsync, cancellationToken);
            }
            finally
            {
                stopwatch.Stop();
                _timingLog.RecordRouter(new ChatterboxTimingLogService.RouterTiming
                {
                    ConfiguredProvider = _options.Provider,
                    SelectedProvider = "piper",
                    UsedFallback = true,
                    Reason = "unknown configured provider",
                    Chars = text.Length,
                    ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
                });
            }
            return;
        }

        if (ChatterboxCircuitOpen())
        {
            const string reason = "Chatterbox circuit breaker is open.";
            try
            {
                await UseFallbackAsync(text, onMetadataAsync, onAudioChunkAsync, reason, cancellationToken);
            }
            finally
            {
                stopwatch.Stop();
                _timingLog.RecordRouter(new ChatterboxTimingLogService.RouterTiming
                {
                    ConfiguredProvider = _options.Provider,
                    SelectedProvider = "piper",
                    UsedFallback = true,
                    Reason = reason,
                    Chars = text.Length,
                    ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
                });
            }
            return;
        }

        try
        {
            await _chatterboxProvider.StreamSynthesizeChunksAsync(text, onMetadataAsync, onAudioChunkAsync, cancellationToken);
            MarkChatterboxSuccess();
            stopwatch.Stop();
            _timingLog.RecordRouter(new ChatterboxTimingLogService.RouterTiming
            {
                ConfiguredProvider = _options.Provider,
                SelectedProvider = "chatterbox",
                UsedFallback = false,
                Reason = "success",
                Chars = text.Length,
                ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Chatterbox TTS failed. Falling back to Piper for this request.");
            MarkChatterboxFailure(exception.Message);
            try
            {
                await UseFallbackAsync(text, onMetadataAsync, onAudioChunkAsync, exception.Message, cancellationToken);
            }
            finally
            {
                stopwatch.Stop();
                _timingLog.RecordRouter(new ChatterboxTimingLogService.RouterTiming
                {
                    ConfiguredProvider = _options.Provider,
                    SelectedProvider = "piper",
                    UsedFallback = true,
                    Reason = exception.Message,
                    Chars = text.Length,
                    ElapsedMs = stopwatch.Elapsed.TotalMilliseconds
                });
            }
        }
    }

    private async Task UseFallbackAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<VoiceSynthesisAudioChunk, CancellationToken, Task> onAudioChunkAsync,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(_options.FallbackProvider, "piper", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Configured TTS fallback provider is not supported: {_options.FallbackProvider}");
        }

        _logger.LogWarning("TTS degraded fallback mode. Provider: Piper. Reason: {Reason}", reason);
        await _piperProvider.StreamSynthesizeChunksAsync(text, onMetadataAsync, onAudioChunkAsync, cancellationToken);
    }

    private bool ChatterboxCircuitOpen()
    {
        lock (_circuitBreakerLock)
        {
            if (_chatterboxUnhealthyUntilUtc is null)
            {
                return false;
            }

            if (DateTimeOffset.UtcNow < _chatterboxUnhealthyUntilUtc.Value)
            {
                _logger.LogWarning("Chatterbox circuit breaker open until {UnhealthyUntilUtc}. Skipping Chatterbox.", _chatterboxUnhealthyUntilUtc);
                return true;
            }

            _chatterboxUnhealthyUntilUtc = null;
            _chatterboxConsecutiveFailures = 0;
            _logger.LogInformation("Chatterbox circuit breaker cooldown elapsed. Trying Chatterbox again.");
            return false;
        }
    }

    private void MarkChatterboxSuccess()
    {
        lock (_circuitBreakerLock)
        {
            if (_chatterboxConsecutiveFailures > 0 || _chatterboxUnhealthyUntilUtc is not null)
            {
                _logger.LogInformation("Chatterbox circuit breaker deactivated after successful synthesis.");
            }

            _chatterboxConsecutiveFailures = 0;
            _chatterboxUnhealthyUntilUtc = null;
        }
    }

    private void MarkChatterboxFailure(string reason)
    {
        lock (_circuitBreakerLock)
        {
            _chatterboxConsecutiveFailures++;
            if (_chatterboxConsecutiveFailures < Math.Max(1, _options.ChatterboxCircuitBreakerFailures))
            {
                return;
            }

            _chatterboxUnhealthyUntilUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, _options.ChatterboxCircuitBreakerCooldownSeconds));
            _logger.LogWarning(
                "Chatterbox circuit breaker activated after {Failures} consecutive failures. CooldownSeconds: {CooldownSeconds}. Reason: {Reason}",
                _chatterboxConsecutiveFailures,
                _options.ChatterboxCircuitBreakerCooldownSeconds,
                reason);
        }
    }
}
