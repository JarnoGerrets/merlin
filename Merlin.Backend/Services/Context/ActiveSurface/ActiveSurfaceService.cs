namespace Merlin.Backend.Services.Context.ActiveSurface;

public sealed class ActiveSurfaceService : IActiveSurfaceService
{
    private readonly ILogger<ActiveSurfaceService> _logger;
    private readonly object _sync = new();
    private ActiveSurfaceSnapshot _current;

    public ActiveSurfaceService(ILogger<ActiveSurfaceService> logger)
    {
        _logger = logger;
        _current = KnownSurfaces.Dashboard(DateTimeOffset.UtcNow);
    }

    public event Func<ActiveSurfaceSnapshot, string, CancellationToken, Task>? ActiveSurfaceChanged;

    public ActiveSurfaceSnapshot Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public Task<ActiveSurfaceSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Current);
    }

    public async Task SetActiveSurfaceAsync(
        ActiveSurfaceUpdate update,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var confidence = Math.Clamp(update.Confidence, 0.0, 1.0);
        var capabilities = new HashSet<string>(
            update.Capabilities.Where(static capability => !string.IsNullOrWhiteSpace(capability)),
            StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, string>(
            update.Metadata
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
                .ToDictionary(static item => item.Key, static item => item.Value),
            StringComparer.OrdinalIgnoreCase);

        var next = new ActiveSurfaceSnapshot(
            update.Kind,
            string.IsNullOrWhiteSpace(update.SurfaceId) ? update.Kind.ToString().ToLowerInvariant() : update.SurfaceId,
            string.IsNullOrWhiteSpace(update.DisplayName) ? update.Kind.ToString() : update.DisplayName,
            confidence,
            update.Source,
            now,
            capabilities,
            metadata);

        ActiveSurfaceSnapshot previous;
        lock (_sync)
        {
            previous = _current;
            _current = next;
        }

        _logger.LogInformation(
            "ActiveSurfaceChanged OldSurfaceKind: {OldSurfaceKind}. NewSurfaceKind: {NewSurfaceKind}. SurfaceId: {SurfaceId}. Source: {Source}. Confidence: {Confidence}. Reason: {Reason}. CorrelationId: {CorrelationId}.",
            previous.Kind,
            next.Kind,
            next.SurfaceId,
            next.Source,
            next.Confidence,
            update.Reason,
            update.CorrelationId);

        var handlers = ActiveSurfaceChanged;
        if (handlers is not null)
        {
            await handlers.Invoke(next, update.Reason ?? "active_surface_changed", cancellationToken);
        }
    }

    public Task ResetToDashboardAsync(string reason, CancellationToken cancellationToken = default)
    {
        var dashboard = KnownSurfaces.Dashboard(
            DateTimeOffset.UtcNow,
            ActiveSurfaceSource.TimeoutFallback,
            string.IsNullOrWhiteSpace(reason) ? "reset_to_dashboard" : reason);
        return SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = dashboard.Kind,
            SurfaceId = dashboard.SurfaceId,
            DisplayName = dashboard.DisplayName,
            Source = dashboard.Source,
            Confidence = dashboard.Confidence,
            Capabilities = dashboard.Capabilities,
            Metadata = dashboard.Metadata,
            Reason = reason
        }, cancellationToken);
    }

    public bool CurrentSupports(string capability)
    {
        if (string.IsNullOrWhiteSpace(capability))
        {
            return false;
        }

        return Current.Capabilities.Contains(capability);
    }
}
