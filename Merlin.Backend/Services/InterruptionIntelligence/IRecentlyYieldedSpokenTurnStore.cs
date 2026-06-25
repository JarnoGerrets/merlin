namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IRecentlyYieldedSpokenTurnStore
{
    void Record(RecentlyYieldedSpokenTurnSnapshot snapshot);

    RecentlyYieldedSpokenTurnSnapshot? TryGetFreshSnapshot(DateTimeOffset? nowUtc = null);
}
