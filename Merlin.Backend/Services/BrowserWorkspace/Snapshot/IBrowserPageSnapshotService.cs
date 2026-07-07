namespace Merlin.Backend.Services.BrowserWorkspace.Snapshot;

using Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;

public interface IBrowserPageSnapshotService
{
    BrowserPageSnapshot? LatestSnapshot { get; }

    Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(
        BrowserSnapshotFreshnessPolicy policy,
        CancellationToken cancellationToken = default);
}
