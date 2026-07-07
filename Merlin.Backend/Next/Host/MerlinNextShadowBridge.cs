using Merlin.Backend.Next.Kernel.Requests;
using Merlin.Backend.Next.Kernel.Runtime;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Next.Host;

public sealed class MerlinNextShadowBridge : IMerlinNextShadowBridge
{
    private static readonly TimeSpan ShadowTimeout = TimeSpan.FromMilliseconds(100);

    private readonly ILogger<MerlinNextShadowBridge> _logger;
    private readonly IMerlinNextRuntime _runtime;
    private readonly IOptionsMonitor<MerlinNextRuntimeOptions> _options;

    public MerlinNextShadowBridge(
        IMerlinNextRuntime runtime,
        IOptionsMonitor<MerlinNextRuntimeOptions> options,
        ILogger<MerlinNextShadowBridge> logger)
    {
        _runtime = runtime;
        _options = options;
        _logger = logger;
    }

    public void TryStartShadow(MerlinRequest request)
    {
        var options = _options.CurrentValue;
        if (!ShouldRunShadow(options))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var timeout = new CancellationTokenSource(ShadowTimeout);
                await _runtime.RunShadowAsync(request, timeout.Token);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "MerlinNextShadowBridgeFailed RequestId: {RequestId}.",
                    request.RequestId);
            }
        });
    }

    private static bool ShouldRunShadow(MerlinNextRuntimeOptions options)
    {
        return options.Enabled
            && options.ShadowEnabled
            && options.Mode is MerlinNextRuntimeMode.Shadow;
    }
}
