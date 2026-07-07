namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

public sealed record BrowserPageSafetyDecision
{
    public BrowserPageSafetyLevel Level { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyList<BrowserPageSafetyRisk> Risks { get; init; } = [];
}
