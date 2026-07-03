namespace Merlin.Backend.Models;

public sealed class ToolResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? SpokenText { get; init; }

    public string? SpeechCacheKey { get; init; }

    public bool PreferPhraseCache { get; init; }

    public bool IsReplayableSpeech { get; init; }

    public bool SegmentedSpeechStarted { get; init; }

    public string? ErrorCode { get; init; }

    public string? ToolName { get; init; }

    public string? Intent { get; init; }

    public string? CapabilityId { get; init; }

    public string? CapabilityName { get; init; }

    public string? ResponseType { get; init; }

    public IReadOnlyCollection<ToolMetadata>? AvailableTools { get; init; }

    public DiagnosticsInfo? Diagnostics { get; init; }

    public PendingConfirmation? Confirmation { get; init; }

    public IReadOnlyList<ApplicationCandidate>? ApplicationCandidates { get; init; }

    public IReadOnlyList<DevVisualFlowStep>? DevVisualFlow { get; init; }
}
