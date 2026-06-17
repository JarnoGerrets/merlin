namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInDiagnosticsLogger : IBargeInDiagnosticsLogger
{
    private readonly ILogger<BargeInDiagnosticsLogger> _logger;

    public BargeInDiagnosticsLogger(ILogger<BargeInDiagnosticsLogger> logger)
    {
        _logger = logger;
    }

    public void MonitorStarted(BargeInSpeechContext context, AecMode aecMode)
    {
        _logger.LogInformation(
            "Barge-in monitor started. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. AecMode: {AecMode}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            aecMode);
    }

    public void MonitorStopped(BargeInSpeechContext context)
    {
        _logger.LogInformation(
            "Barge-in monitor stopped. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType);
    }

    public void AecInitialized(AecMode mode, string reason)
    {
        if (mode is AecMode.Active)
        {
            _logger.LogInformation("AEC provider active. AecMode: {AecMode}. Reason: {Reason}", mode, reason);
            return;
        }

        _logger.LogWarning("AEC provider not active. AecMode: {AecMode}. Reason: {Reason}", mode, reason);
    }

    public void PlaybackReferenceFrameReceived(string? correlationId, int sampleCount)
    {
        _logger.LogDebug(
            "Playback reference frame received. CorrelationId: {CorrelationId}. Samples: {Samples}.",
            correlationId,
            sampleCount);
    }

    public void MicFrameProcessed(BargeInSpeechContext context, long frameCount)
    {
        _logger.LogDebug(
            "Mic frame processed. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. MicFrameCount: {MicFrameCount}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            frameCount);
    }

    public void EchoReducedFrameProcessed(BargeInSpeechContext context, long frameCount, AecMode aecMode)
    {
        _logger.LogDebug(
            "Echo-reduced frame processed. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. EchoReducedFrameCount: {EchoReducedFrameCount}. AecMode: {AecMode}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            frameCount,
            aecMode);
    }

    public void VadPossibleSpeech(BargeInSpeechContext context, VadFrameResult result, AecMode aecMode)
    {
        _logger.LogInformation(
            "VAD possible speech detected. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. VadConfidence: {VadConfidence}. Energy: {Energy}. NoiseFloor: {NoiseFloor}. ConsecutiveSpeechMs: {ConsecutiveSpeechMs}. AecMode: {AecMode}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            result.Confidence,
            result.Energy,
            result.NoiseFloor,
            result.ConsecutiveSpeechMs,
            aecMode);
    }

    public void TriggerBufferCaptured(BargeInSpeechContext context, int frameCount, TimeSpan duration)
    {
        _logger.LogInformation(
            "Trigger buffer captured. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. Frames: {Frames}. AudioMs: {AudioMs}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            frameCount,
            duration.TotalMilliseconds);
    }

    public void GatedSttStarted(BargeInSpeechContext context, TimeSpan duration)
    {
        _logger.LogInformation(
            "Gated STT started. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. AudioMs: {AudioMs}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            duration.TotalMilliseconds);
    }

    public void GatedSttResult(BargeInSpeechContext context, BargeInSttResult result)
    {
        _logger.LogInformation(
            "Gated STT result. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. SttTranscript: {SttTranscript}. AudioMs: {AudioMs}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            result.Transcript,
            result.AudioDuration.TotalMilliseconds);
    }

    public void ClassificationResult(BargeInSpeechContext context, InterruptionClassificationResult result)
    {
        _logger.LogInformation(
            "Interruption classifier result. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. ClassificationType: {ClassificationType}. ClassificationConfidence: {ClassificationConfidence}. Reason: {Reason}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            result.Type,
            result.Confidence,
            result.Reason);
    }

    public void StateChanged(BargeInSpeechContext context, BargeInState state, string reason)
    {
        _logger.LogInformation(
            "Barge-in state changed. State: {State}. Reason: {Reason}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}.",
            state,
            reason,
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType);
    }

    public void ActionSelected(BargeInSpeechContext context, BargeInAction action, string reason)
    {
        _logger.LogInformation(
            "Barge-in action selected. Action: {Action}. Reason: {Reason}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}.",
            action,
            reason,
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType);
    }

    public void PlaybackResumed(BargeInSpeechContext context, string reason)
    {
        _logger.LogInformation(
            "Barge-in playback resumed. Reason: {Reason}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}.",
            reason,
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType);
    }

    public void CorrectionRegenerationStarted(BargeInSpeechContext context, string correctionText)
    {
        _logger.LogInformation(
            "Correction regeneration requested. CorrectionText: {CorrectionText}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}.",
            correctionText,
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType);
    }

    public void Ignored(BargeInSpeechContext context, string reason)
    {
        _logger.LogInformation(
            "Barge-in ignored: {Reason}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}.",
            reason,
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType);
    }

    public void Accepted(BargeInSpeechContext context, InterruptionClassificationResult result)
    {
        _logger.LogInformation(
            "Barge-in accepted: {ClassificationType}. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. ClassificationConfidence: {ClassificationConfidence}. Reason: {Reason}.",
            result.Type,
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            result.Confidence,
            result.Reason);
    }

    public void AssistantTurnCancelled(BargeInSpeechContext context, InterruptionClassificationResult result)
    {
        _logger.LogInformation(
            "Assistant turn cancelled. CorrelationId: {CorrelationId}. AssistantTurnId: {AssistantTurnId}. SpeechType: {SpeechType}. InterruptionType: {InterruptionType}.",
            context.CorrelationId,
            context.AssistantTurnId,
            context.SpeechType,
            result.Type);
    }
}
