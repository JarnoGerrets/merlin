namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

public sealed record BrowserPagePendingConfirmation
{
    public BrowserPageAction Action { get; init; }

    public string ElementId { get; init; } = string.Empty;

    public string? ElementText { get; init; }

    public string? ElementHref { get; init; }

    public string? CurrentUrl { get; init; }

    public DateTimeOffset SnapshotCapturedAtUtc { get; init; }

    public IReadOnlyList<BrowserPageSafetyRisk> Risks { get; init; } = [];
}
