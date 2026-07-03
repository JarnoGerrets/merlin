using System.Diagnostics;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public enum GpuWorkPriority
{
    High,
    Medium,
    Low
}

public interface IGpuWorkScheduler
{
    bool HasPendingInterruptionStt { get; }

    Task<T> RunAsync<T>(
        string jobName,
        GpuWorkPriority priority,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);

    Task RunAsync(
        string jobName,
        GpuWorkPriority priority,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);
}

public sealed class GpuWorkScheduler : IGpuWorkScheduler
{
    private const string ChatterboxTtsChunkJobName = "ChatterboxTtsChunk";
    private const string InterruptionSttJobName = "InterruptionStt";

    private readonly ILogger<GpuWorkScheduler> _logger;
    private readonly GpuSchedulingOptions _options;
    private readonly object _syncRoot = new();
    private TaskCompletionSource _stateChanged = NewStateChanged();
    private string? _activeExclusiveJobId;
    private string? _activeExclusiveJobName;
    private string? _activeTtsJobId;
    private string? _activeSttJobId;
    private int _queuedInterruptionSttCount;
    private readonly Dictionary<string, bool> _ttsOverlapObserved = new(StringComparer.Ordinal);

    public GpuWorkScheduler(
        ILogger<GpuWorkScheduler> logger,
        IOptions<GpuSchedulingOptions>? options = null)
    {
        _logger = logger;
        _options = options?.Value ?? new GpuSchedulingOptions();
    }

    public bool HasPendingInterruptionStt
    {
        get
        {
            lock (_syncRoot)
            {
                return _queuedInterruptionSttCount > 0 || _activeSttJobId is not null;
            }
        }
    }

    public async Task<T> RunAsync<T>(
        string jobName,
        GpuWorkPriority priority,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var queuedAt = Stopwatch.GetTimestamp();
        var jobId = Guid.NewGuid().ToString("N");
        var queueState = RegisterQueuedJob(jobId, jobName);
        _logger.LogInformation(
            "GpuJobQueued. JobName: {JobName}. Priority: {Priority}. OverlapAllowed: {OverlapAllowed}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}. ActualWorkerMs: {ActualWorkerMs}. TotalElapsedMs: {TotalElapsedMs}. PeakGpuMemoryIfAvailable: {PeakGpuMemoryIfAvailable}.",
            jobName,
            priority,
            queueState.OverlapAllowed,
            queueState.OverlapMode,
            queueState.ActiveTtsJobId,
            queueState.ActiveSttJobId,
            0.0,
            0.0,
            0.0,
            null);

        if (queueState.OverlapAttempted)
        {
            _logger.LogInformation(
                "interruption_stt_overlap_attempted JobName: {JobName}. Priority: {Priority}. OverlapAllowed: {OverlapAllowed}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}.",
                jobName,
                priority,
                queueState.OverlapAllowed,
                queueState.OverlapMode,
                queueState.ActiveTtsJobId,
                queueState.ActiveSttJobId);
        }

        JobStartState startState;
        try
        {
            startState = await WaitForStartAsync(jobId, jobName, cancellationToken);
        }
        catch
        {
            UnregisterQueuedJob(jobName);
            throw;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var queuedToStartMs = ElapsedMs(queuedAt, startedAt);
        try
        {
            _logger.LogInformation(
                "GpuJobStarted. JobName: {JobName}. Priority: {Priority}. OverlapAllowed: {OverlapAllowed}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}. ActualWorkerMs: {ActualWorkerMs}. TotalElapsedMs: {TotalElapsedMs}. PeakGpuMemoryIfAvailable: {PeakGpuMemoryIfAvailable}.",
                jobName,
                priority,
                startState.OverlapAllowed,
                startState.OverlapMode,
                startState.ActiveTtsJobId,
                startState.ActiveSttJobId,
                queuedToStartMs,
                0.0,
                queuedToStartMs,
                null);

            if (startState.OverlapStarted)
            {
                _logger.LogInformation(
                    "interruption_stt_overlap_started JobName: {JobName}. Priority: {Priority}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}.",
                    jobName,
                    priority,
                    startState.OverlapMode,
                    startState.ActiveTtsJobId,
                    startState.ActiveSttJobId,
                    queuedToStartMs);
            }
            else if (IsInterruptionStt(jobName) && queueState.OverlapAttempted)
            {
                _logger.LogInformation(
                    "interruption_stt_overlap_rejected JobName: {JobName}. Priority: {Priority}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}.",
                    jobName,
                    priority,
                    startState.OverlapMode,
                    startState.ActiveTtsJobId,
                    startState.ActiveSttJobId,
                    queuedToStartMs);
            }

            var result = await action(cancellationToken);
            var completedAt = Stopwatch.GetTimestamp();
            var workerMs = ElapsedMs(startedAt, completedAt);
            var totalMs = ElapsedMs(queuedAt, completedAt);
            _logger.LogInformation(
                "GpuJobCompleted. JobName: {JobName}. Priority: {Priority}. OverlapAllowed: {OverlapAllowed}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}. ActualWorkerMs: {ActualWorkerMs}. TotalElapsedMs: {TotalElapsedMs}. PeakGpuMemoryIfAvailable: {PeakGpuMemoryIfAvailable}.",
                jobName,
                priority,
                startState.OverlapAllowed,
                startState.OverlapMode,
                startState.ActiveTtsJobId,
                startState.ActiveSttJobId,
                queuedToStartMs,
                workerMs,
                totalMs,
                null);

            if (IsInterruptionStt(jobName) && startState.OverlapStarted)
            {
                _logger.LogInformation(
                    "interruption_stt_overlap_completed JobName: {JobName}. Priority: {Priority}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}. ActualWorkerMs: {ActualWorkerMs}. TotalElapsedMs: {TotalElapsedMs}.",
                    jobName,
                    priority,
                    startState.OverlapMode,
                    startState.ActiveTtsJobId,
                    startState.ActiveSttJobId,
                    queuedToStartMs,
                    workerMs,
                    totalMs);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            var completedAt = Stopwatch.GetTimestamp();
            _logger.LogInformation(
                "GpuJobCancelled. JobName: {JobName}. Priority: {Priority}. OverlapAllowed: {OverlapAllowed}. OverlapMode: {OverlapMode}. ActiveTtsJobId: {ActiveTtsJobId}. ActiveSttJobId: {ActiveSttJobId}. QueuedToStartMs: {QueuedToStartMs}. ActualWorkerMs: {ActualWorkerMs}. TotalElapsedMs: {TotalElapsedMs}. PeakGpuMemoryIfAvailable: {PeakGpuMemoryIfAvailable}.",
                jobName,
                priority,
                startState.OverlapAllowed,
                startState.OverlapMode,
                startState.ActiveTtsJobId,
                startState.ActiveSttJobId,
                queuedToStartMs,
                ElapsedMs(startedAt, completedAt),
                ElapsedMs(queuedAt, completedAt),
                null);
            throw;
        }
        finally
        {
            CompleteJob(jobId, jobName);
        }
    }

    public Task RunAsync(
        string jobName,
        GpuWorkPriority priority,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            jobName,
            priority,
            async token =>
            {
                await action(token);
                return true;
            },
            cancellationToken);
    }

    private QueueState RegisterQueuedJob(string jobId, string jobName)
    {
        lock (_syncRoot)
        {
            if (IsInterruptionStt(jobName))
            {
                _queuedInterruptionSttCount++;
            }

            var overlapAllowed = IsInterruptionStt(jobName)
                && _options.EnableConcurrentInterruptionSttDuringTts;
            var overlapAttempted = overlapAllowed && _activeTtsJobId is not null;
            return new QueueState(
                overlapAllowed,
                overlapAttempted,
                overlapAttempted ? "interruption_stt_during_tts" : "serialized",
                _activeTtsJobId,
                _activeSttJobId);
        }
    }

    private void UnregisterQueuedJob(string jobName)
    {
        lock (_syncRoot)
        {
            if (IsInterruptionStt(jobName) && _queuedInterruptionSttCount > 0)
            {
                _queuedInterruptionSttCount--;
            }
        }
    }

    private async Task<JobStartState> WaitForStartAsync(
        string jobId,
        string jobName,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            Task waitTask;
            lock (_syncRoot)
            {
                if (CanStartLocked(jobName, out var overlapStarted, out var overlapMode))
                {
                    StartJobLocked(jobId, jobName, overlapStarted);
                    return new JobStartState(
                        IsInterruptionStt(jobName) && _options.EnableConcurrentInterruptionSttDuringTts,
                        overlapStarted,
                        overlapMode,
                        _activeTtsJobId,
                        _activeSttJobId);
                }

                waitTask = _stateChanged.Task;
            }

            await waitTask.WaitAsync(cancellationToken);
        }
    }

    private bool CanStartLocked(string jobName, out bool overlapStarted, out string overlapMode)
    {
        overlapStarted = false;
        overlapMode = "serialized";

        if (!_options.EnableConcurrentInterruptionSttDuringTts)
        {
            return _activeExclusiveJobId is null && _activeSttJobId is null;
        }

        if (IsInterruptionStt(jobName))
        {
            if (_activeSttJobId is not null)
            {
                overlapMode = "rejected_active_interruption_stt";
                return false;
            }

            if (_activeExclusiveJobId is null)
            {
                return true;
            }

            if (IsChatterboxTtsChunk(_activeExclusiveJobName) && _activeTtsJobId is not null)
            {
                overlapStarted = true;
                overlapMode = "interruption_stt_during_tts";
                return true;
            }

            overlapMode = "rejected_active_non_tts_job";
            return false;
        }

        return _activeExclusiveJobId is null && _activeSttJobId is null;
    }

    private void StartJobLocked(string jobId, string jobName, bool overlapStarted)
    {
        if (IsInterruptionStt(jobName))
        {
            if (_queuedInterruptionSttCount > 0)
            {
                _queuedInterruptionSttCount--;
            }

            _activeSttJobId = jobId;
            if (overlapStarted && _activeTtsJobId is not null)
            {
                _ttsOverlapObserved[_activeTtsJobId] = true;
            }

            return;
        }

        _activeExclusiveJobId = jobId;
        _activeExclusiveJobName = jobName;
        if (IsChatterboxTtsChunk(jobName))
        {
            _activeTtsJobId = jobId;
            _ttsOverlapObserved[jobId] = false;
        }
    }

    private void CompleteJob(string jobId, string jobName)
    {
        bool ttsOverlapObserved;
        lock (_syncRoot)
        {
            ttsOverlapObserved = IsChatterboxTtsChunk(jobName)
                && _ttsOverlapObserved.TryGetValue(jobId, out var observed)
                && observed;

            if (IsInterruptionStt(jobName) && string.Equals(_activeSttJobId, jobId, StringComparison.Ordinal))
            {
                _activeSttJobId = null;
            }

            if (string.Equals(_activeExclusiveJobId, jobId, StringComparison.Ordinal))
            {
                _activeExclusiveJobId = null;
                _activeExclusiveJobName = null;
            }

            if (string.Equals(_activeTtsJobId, jobId, StringComparison.Ordinal))
            {
                _activeTtsJobId = null;
                _ttsOverlapObserved.Remove(jobId);
            }

            var previous = _stateChanged;
            _stateChanged = NewStateChanged();
            previous.TrySetResult();
        }

        if (ttsOverlapObserved)
        {
            _logger.LogInformation(
                "tts_chunk_overlap_observed JobName: {JobName}. ActiveTtsJobId: {ActiveTtsJobId}.",
                jobName,
                jobId);
        }
    }

    private static bool IsChatterboxTtsChunk(string? jobName) =>
        string.Equals(jobName, ChatterboxTtsChunkJobName, StringComparison.Ordinal);

    private static bool IsInterruptionStt(string jobName) =>
        string.Equals(jobName, InterruptionSttJobName, StringComparison.Ordinal);

    private static TaskCompletionSource NewStateChanged() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static double ElapsedMs(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;

    private sealed record QueueState(
        bool OverlapAllowed,
        bool OverlapAttempted,
        string OverlapMode,
        string? ActiveTtsJobId,
        string? ActiveSttJobId);

    private sealed record JobStartState(
        bool OverlapAllowed,
        bool OverlapStarted,
        string OverlapMode,
        string? ActiveTtsJobId,
        string? ActiveSttJobId);
}
