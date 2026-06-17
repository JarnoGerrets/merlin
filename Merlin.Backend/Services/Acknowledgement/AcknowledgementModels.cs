using Merlin.Backend.Models;

namespace Merlin.Backend.Services.Acknowledgement;

public enum AcknowledgementCategory
{
    GeneralReasoning,
    DeepTechnicalArchitecture,
    ResearchRecommendation,
    LocalSystemTool,
    MemorySearch,
    MemorySave,
    DeepInfraPendingProgress,
    ToolPendingProgress,
    MemoryPendingProgress,
    GenericStillWorkingProgress
}

public enum RequestProgressState
{
    WaitingOnDeepInfra,
    WaitingOnTool,
    WaitingOnMemory,
    StillWorking
}

public sealed record AcknowledgementPhrase(
    string Id,
    AcknowledgementCategory Category,
    string Text);

public sealed record AcknowledgementContext
{
    public required string UserText { get; init; }

    public required string NormalizedText { get; init; }

    public string? IntentDomain { get; init; }

    public string? Capability { get; init; }

    public required bool IsVoiceMode { get; init; }

    public required bool WillUseDeepInfra { get; init; }

    public required bool WillUseExternalTool { get; init; }

    public required bool IsExpectedFastLocalTool { get; init; }

    public required bool IsMemorySave { get; init; }

    public required bool IsMemorySearch { get; init; }

    public int RecentAcknowledgementCount { get; init; }

    public required DateTimeOffset Now { get; init; }
}

public sealed record AcknowledgementDecision
{
    public required bool ShouldSpeakInitialAcknowledgement { get; init; }

    public string? PhraseId { get; init; }

    public string? PhraseText { get; init; }

    public AcknowledgementCategory? InitialCategory { get; init; }

    public required TimeSpan FirstProgressAfter { get; init; }

    public required TimeSpan SecondProgressAfter { get; init; }

    public required TimeSpan LongWaitProgressAfter { get; init; }

    public required int MaxProgressUpdates { get; init; }

    public required RequestProgressState ProgressState { get; init; }

    public required string Reason { get; init; }

    public static AcknowledgementDecision Skipped(
        string reason,
        TimeSpan firstProgressAfter,
        TimeSpan secondProgressAfter,
        TimeSpan longWaitProgressAfter,
        int maxProgressUpdates,
        RequestProgressState progressState)
    {
        return new AcknowledgementDecision
        {
            ShouldSpeakInitialAcknowledgement = false,
            FirstProgressAfter = firstProgressAfter,
            SecondProgressAfter = secondProgressAfter,
            LongWaitProgressAfter = longWaitProgressAfter,
            MaxProgressUpdates = maxProgressUpdates,
            ProgressState = progressState,
            Reason = reason
        };
    }
}

public sealed record AcknowledgementPlaybackRequest
{
    public required string RequestId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset CommandReceivedAtUtc { get; init; }

    public required AcknowledgementDecision Decision { get; init; }

    public required Func<AssistantVisualEvent, CancellationToken, Task> SendEventAsync { get; init; }
}

public sealed record RequestProgressSpeechRequest
{
    public required string RequestId { get; init; }

    public required string CorrelationId { get; init; }

    public required DateTimeOffset CommandReceivedAtUtc { get; init; }

    public required AcknowledgementDecision Decision { get; init; }

    public required Func<AssistantVisualEvent, CancellationToken, Task> SendEventAsync { get; init; }
}
