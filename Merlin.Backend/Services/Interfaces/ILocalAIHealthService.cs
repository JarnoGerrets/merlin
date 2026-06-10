namespace Merlin.Backend.Services;

public interface ILocalAIHealthService
{
    bool IsEnabled { get; }

    bool IsAvailable { get; }

    DateTimeOffset? LastWarmupUtc { get; }

    string? LastError { get; }

    long? LastLatencyMs { get; }

    Task WarmupAsync(CancellationToken cancellationToken = default);

    void MarkDisabled();

    void MarkAvailable(long latencyMs);

    void MarkUnavailable(string error, long? latencyMs = null);
}
