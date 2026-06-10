namespace Merlin.Backend.Services;

public interface IRuntimeStateService
{
    DateTimeOffset BackendStartedAtUtc { get; }

    TimeSpan Uptime { get; }

    int ActiveWebSocketConnections { get; }

    string LastIntentParserUsed { get; }

    string? LastIntent { get; }

    long TotalRequestsProcessed { get; }

    long TotalSuccessfulToolExecutions { get; }

    long TotalFailedToolExecutions { get; }

    void IncrementActiveWebSocketConnections();

    void DecrementActiveWebSocketConnections();

    void IncrementRequestsProcessed();

    void IncrementSuccessfulToolExecutions();

    void IncrementFailedToolExecutions();

    void RecordIntentParserUsed(string parserName, string? intent);
}
