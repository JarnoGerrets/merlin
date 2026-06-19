namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInCoordinatorHostedService : IHostedService
{
    private readonly IBargeInCoordinator _coordinator;
    private readonly ILogger<BargeInCoordinatorHostedService> _logger;

    public BargeInCoordinatorHostedService(
        IBargeInCoordinator coordinator,
        ILogger<BargeInCoordinatorHostedService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Barge-in coordinator initialized.");
        return _coordinator.StartLiveMonitoringAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
