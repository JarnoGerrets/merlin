namespace Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;

public enum BrowserSnapshotFreshnessPolicy
{
    UseLatestIfFresh,
    ForceRefresh,
    RefreshIfOlderThan,
    RefreshIfStale
}
