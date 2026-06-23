namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class NoOpInterruptionModelPort : IInterruptionModelPort
{
    public Task<ClarificationResult> GenerateClarificationAsync(
        ClarificationRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No live interruption model port is configured.");

    public Task<ContinuationRecompositionResult> GenerateContinuationAsync(
        ContinuationRecompositionRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("No live interruption model port is configured.");
}
