namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IInterruptionOrchestrator
{
    Task<InterruptionHandlingResult> HandleInterruptionAsync(
        ConversationalInterruptionCandidate candidate,
        CancellationToken cancellationToken = default);
}
