namespace Merlin.Backend.Services;

public sealed class VoiceWarmupHostedService : BackgroundService
{
    private readonly ILogger<VoiceWarmupHostedService> _logger;
    private readonly IVoiceWarmupService _voiceWarmupService;

    public VoiceWarmupHostedService(
        IVoiceWarmupService voiceWarmupService,
        ILogger<VoiceWarmupHostedService> logger)
    {
        _voiceWarmupService = voiceWarmupService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Warming Python voice worker.");
            await _voiceWarmupService.WarmupAsync(stoppingToken);
            _logger.LogInformation("Python voice worker warmed.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown while warming is harmless.
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Python voice worker warmup failed. The first voice request will retry.");
        }
    }
}
