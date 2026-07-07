using Merlin.Backend.Services.BrowserWorkspace.Snapshot;

namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

public sealed record BrowserPageSafetyContext
{
    public BrowserPageAction Action { get; init; }

    public BrowserSnapshotElement? Element { get; init; }

    public string? SpokenCommand { get; init; }

    public string? Query { get; init; }

    public string? CurrentUrl { get; init; }

    public string? PageTitle { get; init; }

    public IReadOnlyList<BrowserSnapshotElement> NearbyElements { get; init; } = [];
}
