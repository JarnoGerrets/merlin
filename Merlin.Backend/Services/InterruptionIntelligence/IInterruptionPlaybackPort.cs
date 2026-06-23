namespace Merlin.Backend.Services.InterruptionIntelligence;

public interface IInterruptionPlaybackPort
{
    Task PauseCurrentAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default);

    Task CancelCurrentAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default);

    Task StopCurrentAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default);
}
