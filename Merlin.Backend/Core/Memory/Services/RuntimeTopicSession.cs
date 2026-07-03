using System.Collections.Concurrent;

namespace Merlin.Backend.Core.Memory.Services;

public interface IRuntimeTopicSession
{
    DateTimeOffset BackendStartedAtUtc { get; }
    bool IsTopicTouchedInCurrentProcess(string? topicId);
    void MarkTopicTouched(string? topicId);
}

public sealed class RuntimeTopicSession : IRuntimeTopicSession
{
    private readonly ConcurrentDictionary<string, byte> _touchedTopicIds = new(StringComparer.OrdinalIgnoreCase);

    public RuntimeTopicSession(ILogger<RuntimeTopicSession> logger)
    {
        BackendStartedAtUtc = DateTimeOffset.UtcNow;
        logger.LogInformation(
            "runtime_topic_state_cleared_on_startup. BackendStartedAt: {BackendStartedAt}. Reason: {Reason}.",
            BackendStartedAtUtc,
            "new_backend_process");
    }

    public DateTimeOffset BackendStartedAtUtc { get; }

    public bool IsTopicTouchedInCurrentProcess(string? topicId) =>
        !string.IsNullOrWhiteSpace(topicId) && _touchedTopicIds.ContainsKey(topicId);

    public void MarkTopicTouched(string? topicId)
    {
        if (!string.IsNullOrWhiteSpace(topicId))
        {
            _touchedTopicIds.TryAdd(topicId, 0);
        }
    }
}

public sealed class RuntimeTopicSessionStartupService : IHostedService
{
    public RuntimeTopicSessionStartupService(IRuntimeTopicSession runtimeTopicSession)
    {
        _ = runtimeTopicSession;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
