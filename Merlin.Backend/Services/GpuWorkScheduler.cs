namespace Merlin.Backend.Services;

public enum GpuWorkPriority
{
    High,
    Medium,
    Low
}

public interface IGpuWorkScheduler
{
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
    private readonly ILogger<GpuWorkScheduler> _logger;
    private readonly SemaphoreSlim _gpu = new(1, 1);

    public GpuWorkScheduler(ILogger<GpuWorkScheduler> logger)
    {
        _logger = logger;
    }

    public async Task<T> RunAsync<T>(
        string jobName,
        GpuWorkPriority priority,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "GpuJobQueued. JobName: {JobName}. Priority: {Priority}.",
            jobName,
            priority);

        await _gpu.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation(
                "GpuJobStarted. JobName: {JobName}. Priority: {Priority}.",
                jobName,
                priority);
            var result = await action(cancellationToken);
            _logger.LogInformation(
                "GpuJobCompleted. JobName: {JobName}. Priority: {Priority}.",
                jobName,
                priority);
            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation(
                "GpuJobCancelled. JobName: {JobName}. Priority: {Priority}.",
                jobName,
                priority);
            throw;
        }
        finally
        {
            _gpu.Release();
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
}
