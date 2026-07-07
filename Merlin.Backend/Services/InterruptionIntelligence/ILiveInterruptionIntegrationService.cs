namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface ILiveInterruptionIntegrationService
{
    Task<LiveInterruptionHandlingOutcome?> TryHandleYieldedInterruptionAsync(
        YieldedInterruptionUtterance utterance,
        CancellationToken cancellationToken = default);

    Task<InterruptionHandlingResult?> TryHandleLiveInterruptionAsync(
        LiveInterruptionContext context,
        CancellationToken cancellationToken = default);

    Task<LiveInterruptionHandlingOutcome?> TryHandlePendingClarificationResponseAsync(
        PendingInterruptionClarificationResponse response,
        CancellationToken cancellationToken = default);
}
