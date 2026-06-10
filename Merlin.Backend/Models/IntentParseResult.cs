namespace Merlin.Backend.Models;

public sealed class IntentParseResult
{
    public string? Intent { get; init; }

    public string NormalizedCommand { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public string OriginalMessage { get; init; } = string.Empty;

    public string? ParserUsed { get; init; }

    public string? CapabilityId { get; init; }

    public string? CapabilityName { get; init; }
}
