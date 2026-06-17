namespace Merlin.Backend.Core.Memory.Models;

public static class TopicCloseReasons
{
    public const string TopicSwitch = "topic_switch";
    public const string UserRequestedSummary = "user_requested_summary";
    public const string SessionEnd = "session_end";
    public const string ManualClose = "manual_close";
    public const string InterruptedOrAbandoned = "interrupted_or_abandoned";
    public const string ImplementationCompleted = "implementation_completed";
}

public sealed record TopicCloseResult
{
    public required bool Closed { get; init; }
    public string? TopicId { get; init; }
    public string? MediumMemoryId { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<string> Concepts { get; init; } = [];
    public string? Reason { get; init; }
}
