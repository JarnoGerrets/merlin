using Merlin.Backend.Services;

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

    Task FlushFinalAnswerSpeechForTurnAsync(
        string turnId,
        string reason,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    Task<ProvisionalAudioHoldResult> ResumeProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProvisionalAudioHoldResult.Failed(null, reason, "Provisional audio hold resume is not supported.", holdId));

    Task<ProvisionalAudioHoldResult> FlushProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ProvisionalAudioHoldResult.Failed(null, reason, "Provisional audio hold flush is not supported.", holdId));
}
