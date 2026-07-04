namespace Merlin.Backend.Services.Vision;

public interface IVisionSidecarHost
{
    VisionHealthState State { get; }

    Task WarmAsync(CancellationToken cancellationToken = default);

    Task StartTrackingAsync(CancellationToken cancellationToken = default);

    Task StopTrackingAsync(CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
