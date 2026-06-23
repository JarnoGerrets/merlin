namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IInterruptionModelPort
{
    Task<ClarificationResult> GenerateClarificationAsync(
        ClarificationRequest request,
        CancellationToken cancellationToken = default);

    Task<ContinuationRecompositionResult> GenerateContinuationAsync(
        ContinuationRecompositionRequest request,
        CancellationToken cancellationToken = default);
}
