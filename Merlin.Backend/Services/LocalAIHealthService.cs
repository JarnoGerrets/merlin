using System.Diagnostics;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class LocalAIHealthService : ILocalAIHealthService
{
    private const string WarmupPrompt =
        "Return only JSON: {\"intent\":\"unknown\",\"normalizedCommand\":\"\",\"confidence\":0.0}";

    private readonly ILocalAIClient _localAIClient;
    private readonly ILogger<LocalAIHealthService> _logger;
    private readonly object _syncRoot = new();
    private readonly LocalAIOptions _options;
    private bool _isAvailable;
    private string? _lastError;
    private long? _lastLatencyMs;
    private DateTimeOffset? _lastWarmupUtc;

    public LocalAIHealthService(
        ILocalAIClient localAIClient,
        IOptions<LocalAIOptions> options,
        ILogger<LocalAIHealthService> logger)
    {
        _localAIClient = localAIClient;
        _options = options.Value;

        if (!_options.Enabled)
        {
            MarkDisabled();
        }

        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    public bool IsAvailable => Volatile.Read(ref _isAvailable);

    public DateTimeOffset? LastWarmupUtc
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastWarmupUtc;
            }
        }
    }

    public string? LastError
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastError;
            }
        }
    }

    public long? LastLatencyMs
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastLatencyMs;
            }
        }
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            MarkDisabled();
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _localAIClient.GenerateAsync(WarmupPrompt, cancellationToken);
            stopwatch.Stop();
            MarkAvailable(stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Local AI warmup cancelled because the caller cancelled the active operation. ElapsedMs: {ElapsedMs}.",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            MarkUnavailable(exception.Message, stopwatch.ElapsedMilliseconds);
            _logger.LogWarning(exception, "Local AI warmup failed. Backend will continue without LocalAI fallback.");
        }
    }

    public void MarkDisabled()
    {
        Volatile.Write(ref _isAvailable, false);
        lock (_syncRoot)
        {
            _lastError = null;
            _lastLatencyMs = null;
            _lastWarmupUtc = null;
        }
    }

    public void MarkAvailable(long latencyMs)
    {
        Volatile.Write(ref _isAvailable, true);
        lock (_syncRoot)
        {
            _lastError = null;
            _lastLatencyMs = latencyMs;
            _lastWarmupUtc = DateTimeOffset.UtcNow;
        }
    }

    public void MarkUnavailable(string error, long? latencyMs = null)
    {
        Volatile.Write(ref _isAvailable, false);
        lock (_syncRoot)
        {
            _lastError = error;
            _lastLatencyMs = latencyMs;
            _lastWarmupUtc = DateTimeOffset.UtcNow;
        }
    }
}
