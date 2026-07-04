using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Vision;

public sealed class VisionWarmupHostedService : IHostedService
{
    private readonly ILogger<VisionWarmupHostedService> _logger;
    private readonly VisionOptions _options;
    private readonly IVisionSidecarHost _sidecarHost;

    public VisionWarmupHostedService(
        IOptions<VisionOptions> options,
        IVisionSidecarHost sidecarHost,
        ILogger<VisionWarmupHostedService> logger)
    {
        _options = options.Value;
        _sidecarHost = sidecarHost;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.WarmOnStartup)
        {
            return;
        }

        try
        {
            await _sidecarHost.WarmAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Vision warm startup failed. Vision will retry lazily.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _sidecarHost.ShutdownAsync(cancellationToken);
    }
}
