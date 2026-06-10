using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class LocalAIWarmupHostedService : IHostedService
{
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(10);
    private readonly ILocalAIHealthService _healthService;
    private readonly ILogger<LocalAIWarmupHostedService> _logger;
    private readonly LocalAIOptions _options;

    public LocalAIWarmupHostedService(
        ILocalAIHealthService healthService,
        IOptions<LocalAIOptions> options,
        ILogger<LocalAIWarmupHostedService> logger)
    {
        _healthService = healthService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _healthService.MarkDisabled();
            _logger.LogInformation("Local AI is disabled. Skipping warmup.");
            return;
        }

        if (!_options.WarmupOnStartup)
        {
            _logger.LogInformation("Local AI warmup is disabled by configuration.");
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(WarmupTimeout);

        _logger.LogInformation("Starting Local AI warmup. Provider: {Provider}. Model: {Model}", _options.Provider, _options.Model);
        await _healthService.WarmupAsync(timeoutCts.Token);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
