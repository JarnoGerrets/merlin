using System.Diagnostics;
using Merlin.Backend.Next.Kernel.Requests;

namespace Merlin.Backend.Next.Kernel.Runtime;

public sealed class MerlinNextShadowRuntime : IMerlinNextRuntime
{
    private readonly ILogger<MerlinNextShadowRuntime> _logger;

    public MerlinNextShadowRuntime(ILogger<MerlinNextShadowRuntime> logger)
    {
        _logger = logger;
    }

    public Task<MerlinNextShadowTrace> RunShadowAsync(
        MerlinRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trace = new MerlinNextShadowTrace
            {
                RequestId = request.RequestId,
                Source = request.Source,
                NormalizedInputText = request.UserText,
                ActiveSurfaceId = request.RequestedSurfaceId,
                RoutePrediction = "NoDecision",
                ExecutionDisabledReason = "disabled_shadow_mode",
                ElapsedMs = stopwatch.ElapsedMilliseconds
            };

            _logger.LogInformation(
                "MerlinNextShadowTrace request={RequestId} source={Source} text={Text} capability={CapabilityId} execution={ExecutionDisabledReason} elapsedMs={ElapsedMs}",
                trace.RequestId,
                trace.Source,
                trace.NormalizedInputText,
                trace.CapabilityId,
                trace.ExecutionDisabledReason,
                trace.ElapsedMs);

            return Task.FromResult(trace);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var trace = new MerlinNextShadowTrace
            {
                RequestId = request.RequestId,
                Source = request.Source,
                NormalizedInputText = request.UserText,
                ActiveSurfaceId = request.RequestedSurfaceId,
                ExecutionDisabledReason = "shadow_exception",
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                Exception = exception.GetType().Name
            };

            _logger.LogWarning(
                exception,
                "MerlinNextShadowTraceFailed request={RequestId} source={Source} elapsedMs={ElapsedMs}",
                trace.RequestId,
                trace.Source,
                trace.ElapsedMs);

            return Task.FromResult(trace);
        }
    }
}
