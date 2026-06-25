using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class RecentlyYieldedSpokenTurnStore : IRecentlyYieldedSpokenTurnStore
{
    private readonly object _syncRoot = new();
    private readonly InterruptionHandlingOptions _options;
    private RecentlyYieldedSpokenTurnSnapshot? _snapshot;

    public RecentlyYieldedSpokenTurnStore(IOptions<InterruptionHandlingOptions> options)
    {
        _options = options.Value;
    }

    public void Record(RecentlyYieldedSpokenTurnSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (string.IsNullOrWhiteSpace(snapshot.TurnId))
        {
            return;
        }

        lock (_syncRoot)
        {
            _snapshot = snapshot;
        }
    }

    public RecentlyYieldedSpokenTurnSnapshot? TryGetFreshSnapshot(DateTimeOffset? nowUtc = null)
    {
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            if (_snapshot is null)
            {
                return null;
            }

            if (IsExpired(_snapshot, now))
            {
                _snapshot = null;
                return null;
            }

            return _snapshot;
        }
    }

    private bool IsExpired(RecentlyYieldedSpokenTurnSnapshot snapshot, DateTimeOffset now)
    {
        var ttl = TimeSpan.FromMilliseconds(Math.Max(0, _options.RecentlyYieldedTurnTtlMs));
        return ttl == TimeSpan.Zero || now - snapshot.YieldedAtUtc > ttl;
    }
}
