namespace Merlin.Backend.Models;

public sealed class AssistantResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ToolName { get; init; }

    public string? Intent { get; init; }

    public double? IntentConfidence { get; init; }

    public string? OriginalMessage { get; init; }

    public string? ParserUsed { get; init; }

    public string? CapabilityId { get; init; }

    public string? CapabilityName { get; init; }

    public string? ResponseType { get; init; }

    public IReadOnlyCollection<ToolMetadata>? AvailableTools { get; init; }

    public DiagnosticsInfo? Diagnostics { get; init; }

    public PendingConfirmation? Confirmation { get; init; }

    public IReadOnlyList<ApplicationCandidate>? ApplicationCandidates { get; init; }
}
