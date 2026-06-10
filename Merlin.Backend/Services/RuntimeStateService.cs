namespace Merlin.Backend.Services;

public sealed class RuntimeStateService : IRuntimeStateService
{
    private int _activeWebSocketConnections;
    private string _lastIntentParserUsed = string.Empty;
    private string? _lastIntent;
    private long _totalFailedToolExecutions;
    private long _totalRequestsProcessed;
    private long _totalSuccessfulToolExecutions;

    public RuntimeStateService()
    {
        BackendStartedAtUtc = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset BackendStartedAtUtc { get; }

    public TimeSpan Uptime => DateTimeOffset.UtcNow - BackendStartedAtUtc;

    public int ActiveWebSocketConnections => Volatile.Read(ref _activeWebSocketConnections);

    public string LastIntentParserUsed => Volatile.Read(ref _lastIntentParserUsed);

    public string? LastIntent => Volatile.Read(ref _lastIntent);

    public long TotalRequestsProcessed => Interlocked.Read(ref _totalRequestsProcessed);

    public long TotalSuccessfulToolExecutions => Interlocked.Read(ref _totalSuccessfulToolExecutions);

    public long TotalFailedToolExecutions => Interlocked.Read(ref _totalFailedToolExecutions);

    public void IncrementActiveWebSocketConnections()
    {
        Interlocked.Increment(ref _activeWebSocketConnections);
    }

    public void DecrementActiveWebSocketConnections()
    {
        var value = Interlocked.Decrement(ref _activeWebSocketConnections);
        if (value < 0)
        {
            Interlocked.Exchange(ref _activeWebSocketConnections, 0);
        }
    }

    public void IncrementRequestsProcessed()
    {
        Interlocked.Increment(ref _totalRequestsProcessed);
    }

    public void IncrementSuccessfulToolExecutions()
    {
        Interlocked.Increment(ref _totalSuccessfulToolExecutions);
    }

    public void IncrementFailedToolExecutions()
    {
        Interlocked.Increment(ref _totalFailedToolExecutions);
    }

    public void RecordIntentParserUsed(string parserName, string? intent)
    {
        Volatile.Write(ref _lastIntentParserUsed, parserName);
        Volatile.Write(ref _lastIntent, intent);
    }
}
