using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

// PR6 adapter only. Real playback actions are gated by InterruptionHandlingOptions.EnableLivePlaybackActions.
public sealed class AssistantSpeechInterruptionPlaybackPort : IInterruptionPlaybackPort
{
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly InterruptionHandlingOptions _options;
    private readonly ILogger<AssistantSpeechInterruptionPlaybackPort> _logger;

    public AssistantSpeechInterruptionPlaybackPort(
        IAssistantSpeechPlaybackService playbackService,
        IOptions<InterruptionHandlingOptions> options,
        ILogger<AssistantSpeechInterruptionPlaybackPort> logger)
    {
        _playbackService = playbackService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PauseCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
    {
        if (!CanExecutePlaybackAction("pause", turnId, reason))
        {
            return;
        }

        await _playbackService.PauseCurrentSpeechAsync(cancellationToken);
    }

    public async Task CancelCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
    {
        if (!CanExecutePlaybackAction("cancel", turnId, reason))
        {
            return;
        }

        await _playbackService.StopCurrentAsync(cancellationToken);
        await _playbackService.ClearQueueAsync(cancellationToken);
    }

    public async Task StopCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
    {
        if (!CanExecutePlaybackAction("stop", turnId, reason))
        {
            return;
        }

        await _playbackService.StopCurrentAsync(cancellationToken);
    }

    public async Task FlushFinalAnswerSpeechForTurnAsync(string turnId, string reason, CancellationToken cancellationToken = default)
    {
        if (!CanExecutePlaybackAction("flush_final_answer", turnId, reason))
        {
            return;
        }

        await _playbackService.FlushFinalAnswerSpeechForTurnAsync(turnId, reason, cancellationToken);
    }

    public async Task<ProvisionalAudioHoldResult> ResumeProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!CanExecutePlaybackAction("resume_provisional_audio_hold", holdId, reason))
        {
            return ProvisionalAudioHoldResult.Failed(null, reason, "Live playback actions are disabled.", holdId);
        }

        return await _playbackService.ResumeProvisionalAudioHoldAsync(holdId, reason, cancellationToken);
    }

    public async Task<ProvisionalAudioHoldResult> FlushProvisionalAudioHoldAsync(
        string holdId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!CanExecutePlaybackAction("flush_provisional_audio_hold", holdId, reason))
        {
            return ProvisionalAudioHoldResult.Failed(null, reason, "Live playback actions are disabled.", holdId);
        }

        return await _playbackService.FlushProvisionalAudioHoldAsync(holdId, reason, cancellationToken);
    }

    private bool CanExecutePlaybackAction(string action, string turnId, string reason)
    {
        if (!_options.Enabled || _options.EnableLiveShadowMode || !_options.EnableLivePlaybackActions)
        {
            _logger.LogInformation(
                "interruption_playback_action_suppressed Action: {Action}. TurnId: {TurnId}. Reason: {Reason}. ShadowMode: {ShadowMode}. PlaybackActionsEnabled: {PlaybackActionsEnabled}.",
                action,
                turnId,
                reason,
                _options.EnableLiveShadowMode,
                _options.EnableLivePlaybackActions);
            return false;
        }

        return true;
    }
}
