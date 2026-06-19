using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public interface IBargeInDebugSnapshotService
{
    event Func<BargeInDebugSnapshot, CancellationToken, Task>? SnapshotAvailable;

    bool IsEnabled { get; }

    void Publish(BargeInDebugSnapshot snapshot, bool force = false);
}

public sealed class BargeInDebugSnapshotService : IBargeInDebugSnapshotService
{
    private readonly IOptionsMonitor<BargeInDebugOptions> _options;
    private readonly object _syncRoot = new();
    private DateTimeOffset _lastPublishedAt = DateTimeOffset.MinValue;

    public BargeInDebugSnapshotService(IOptionsMonitor<BargeInDebugOptions> options)
    {
        _options = options;
    }

    public event Func<BargeInDebugSnapshot, CancellationToken, Task>? SnapshotAvailable;

    public bool IsEnabled => _options.CurrentValue.DebugOverlayEnabled;

    public void Publish(BargeInDebugSnapshot snapshot, bool force = false)
    {
        if (!IsEnabled)
        {
            return;
        }

        var handler = SnapshotAvailable;
        if (handler is null)
        {
            return;
        }

        if (!force)
        {
            var hz = Math.Clamp(_options.CurrentValue.DebugOverlaySnapshotHz, 1, 60);
            var minimumInterval = TimeSpan.FromSeconds(1.0 / hz);
            lock (_syncRoot)
            {
                if (snapshot.TimestampUtc - _lastPublishedAt < minimumInterval)
                {
                    return;
                }

                _lastPublishedAt = snapshot.TimestampUtc;
            }
        }
        else
        {
            lock (_syncRoot)
            {
                _lastPublishedAt = snapshot.TimestampUtc;
            }
        }

        _ = Task.Run(() => handler(snapshot, CancellationToken.None), CancellationToken.None);
    }
}
