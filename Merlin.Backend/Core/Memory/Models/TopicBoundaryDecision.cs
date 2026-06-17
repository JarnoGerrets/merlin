namespace Merlin.Backend.Core.Memory.Models;

public sealed record TopicBoundaryDecision
{
    public required bool IsNewTopic { get; init; }
    public required bool ShouldClosePreviousTopic { get; init; }
    public required double Confidence { get; init; }
    public string? SuggestedTopicTitle { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyList<string> DetectedConcepts { get; init; } = [];
    public IReadOnlyList<string> FollowUpCues { get; init; } = [];
}
