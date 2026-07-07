namespace Merlin.Backend.Services.Context.ActiveSurface;

public interface IActiveSurfaceService
{
    event Func<ActiveSurfaceSnapshot, string, CancellationToken, Task>? ActiveSurfaceChanged;

    ActiveSurfaceSnapshot Current { get; }

    Task<ActiveSurfaceSnapshot> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task SetActiveSurfaceAsync(ActiveSurfaceUpdate update, CancellationToken cancellationToken = default);

    Task ResetToDashboardAsync(string reason, CancellationToken cancellationToken = default);

    bool CurrentSupports(string capability);
}
