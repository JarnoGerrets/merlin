namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class NoOpInterruptionPlaybackPort : IInterruptionPlaybackPort
{
    public Task PauseCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task CancelCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task StopCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
