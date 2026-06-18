using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInCoordinator : IBargeInCoordinator, IAsyncDisposable
{
    private readonly IAcousticEchoCancellationService _aec;
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly IInterruptionClassifier _interruptionClassifier;
    private readonly ILiveAssistantTurnService _liveTurnService;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly ISpeakerDuckingService _speakerDuckingService;
    private readonly IBargeInSttService _sttService;
    private readonly IBargeInTriggerBuffer _triggerBuffer;
    private readonly IBargeInVadService _vadService;
    private readonly object _syncRoot = new();
    private BargeInSpeechContext? _activeContext;
    private AecMode _aecMode = AecMode.DegradedNoOp;
    private bool _handlingTrigger;
    private int _bargeInsThisTurn;

    public BargeInCoordinator(
        IPlaybackReferenceTap playbackReferenceTap,
        IAcousticEchoCancellationService aec,
        IBargeInVadService vadService,
        ISpeakerDuckingService speakerDuckingService,
        IBargeInTriggerBuffer triggerBuffer,
        IBargeInSttService sttService,
        IInterruptionClassifier interruptionClassifier,
        ILiveAssistantTurnService liveTurnService,
        IAssistantSpeechPlaybackService playbackService,
        IBargeInDiagnosticsLogger diagnostics,
        IOptionsMonitor<BargeInOptions> options)
    {
        _playbackReferenceTap = playbackReferenceTap;
        _aec = aec;
        _vadService = vadService;
        _speakerDuckingService = speakerDuckingService;
        _triggerBuffer = triggerBuffer;
        _sttService = sttService;
        _interruptionClassifier = interruptionClassifier;
        _liveTurnService = liveTurnService;
        _playbackService = playbackService;
        _diagnostics = diagnostics;
        _options = options;

        _playbackReferenceTap.SpeechStarted += OnSpeechStarted;
        _playbackReferenceTap.SpeechStopped += OnSpeechStopped;
    }

    public bool IsMonitoring
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeContext is not null;
            }
        }
    }

    public async Task ProcessMicrophoneFrameAsync(BargeInAudioFrame frame, CancellationToken cancellationToken = default)
    {
        BargeInSpeechContext? context;
        lock (_syncRoot)
        {
            context = _activeContext;
            if (context is null)
            {
                return;
            }
        }

        var options = _options.CurrentValue;
        if (!options.Enabled || !options.EnableVad)
        {
            return;
        }

        var reference = _playbackReferenceTap.GetLatestReferenceFrame(frame.Samples.Length);
        var aecResult = options.EnableAec
            ? _aec.ProcessFrame(frame.Samples, reference)
            : new AecProcessResult
            {
                EchoReducedFrame = frame.Samples,
                Mode = AecMode.Unavailable,
                IsEchoCancellationActive = false,
                Reason = "AEC disabled by configuration."
            };
        _aecMode = aecResult.Mode;
        if (options.RequireRealAecForBargeIn && !aecResult.IsEchoCancellationActive)
        {
            _diagnostics.Ignored(context, $"Real AEC is required before VAD; frame ignored. AecMode: {aecResult.Mode}. Reason: {aecResult.Reason}");
            return;
        }

        var echoReducedFrame = frame with { Samples = aecResult.EchoReducedFrame.ToArray() };
        _diagnostics.EchoReducedFrameProcessed(context, 0, aecResult.Mode);
        _triggerBuffer.AddFrame(echoReducedFrame);
        lock (_syncRoot)
        {
            if (_handlingTrigger)
            {
                return;
            }
        }

        var vad = _vadService.ProcessFrame(
            new VadFrameInput
            {
                Samples = echoReducedFrame.Samples,
                SampleRate = echoReducedFrame.SampleRate,
                Timestamp = echoReducedFrame.Timestamp
            },
            options);

        if (!vad.IsTriggered)
        {
            return;
        }

        _diagnostics.VadPossibleSpeech(context, vad, aecResult.Mode);
        if (options.EnableSpeakerDucking)
        {
            _speakerDuckingService.StartDucking(context);
        }
        _diagnostics.StateChanged(context, BargeInState.SoftPausedForUserSpeech, "VAD detected likely user speech; playback is ducked while capturing interruption.");
        if (options.PauseInsteadOfCancelOnSpeech)
        {
            await _playbackService.PauseCurrentSpeechAsync(cancellationToken);
        }

        lock (_syncRoot)
        {
            if (_handlingTrigger)
            {
                return;
            }

            _handlingTrigger = true;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await HandleTriggeredSpeechAsync(context, echoReducedFrame, vad, aecResult, cancellationToken);
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        _handlingTrigger = false;
                    }
                }
            },
            CancellationToken.None);
    }

    private async Task HandleTriggeredSpeechAsync(
        BargeInSpeechContext context,
        BargeInAudioFrame triggerFrame,
        VadFrameResult vad,
        AecProcessResult aecResult,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.EnableGatedStt)
        {
            _diagnostics.Ignored(context, "Gated STT disabled by configuration.");
            await ResumePreviousSpeechAsync(context, "gated_stt_disabled", cancellationToken);
            _vadService.Reset();
            return;
        }

        _diagnostics.StateChanged(context, BargeInState.CapturingInterruption, "Collecting post-trigger echo-reduced speech for gated STT.");
        await WaitForPostTriggerAudioAsync(triggerFrame, options, cancellationToken);
        var captured = _triggerBuffer.CaptureTriggeredWindow(triggerFrame, options);
        var duration = CalculateDuration(captured);
        _diagnostics.TriggerBufferCaptured(context, captured.Count, duration);
        _diagnostics.GatedSttStarted(context, duration);
        var stt = await _sttService.TranscribeTriggerAsync(captured, options, cancellationToken);
        _diagnostics.GatedSttResult(context, stt);
        _diagnostics.StateChanged(context, BargeInState.ClassifyingInterruption, $"Interruption transcript captured: {stt.Transcript}");

        var normalized = InterruptionClassifier.Normalize(stt.Transcript);
        var classification = _interruptionClassifier.Classify(
            new InterruptionClassificationInput
            {
                RawTranscript = stt.Transcript,
                NormalizedTranscript = normalized,
                AssistantTurnId = context.AssistantTurnId,
                CurrentSpeechType = context.SpeechType.ToString(),
                SpokenTextSoFar = context.SpokenText,
                VadConfidence = vad.Confidence,
                WasWakeWordPresent = options.WakeWords.Any(wakeWord => normalized.StartsWith(InterruptionClassifier.Normalize(wakeWord), StringComparison.OrdinalIgnoreCase)),
                IsAecDegraded = !aecResult.IsEchoCancellationActive
            },
            options);

        _diagnostics.ClassificationResult(context, classification);
        var decision = Decide(context, classification, options);
        _diagnostics.ActionSelected(context, decision.Action, decision.Reason);
        if (!decision.Accepted)
        {
            _diagnostics.Ignored(context, decision.Reason);
            await ResumePreviousSpeechAsync(context, decision.Reason, cancellationToken);
            _vadService.Reset();
            return;
        }

        _diagnostics.Accepted(context, classification);
        if (decision.Action is BargeInAction.Resume or BargeInAction.SideComment or BargeInAction.Clarification)
        {
            if (decision.Action is BargeInAction.Clarification)
            {
                _diagnostics.StateChanged(context, BargeInState.AnsweringClarificationThenResume, "Clarification answer/resume is deferred; resuming current speech.");
            }

            await ResumePreviousSpeechAsync(context, decision.Reason, cancellationToken);
            _vadService.Reset();
            return;
        }

        _bargeInsThisTurn++;
        if (options.EnableTurnCancellation)
        {
            var state = decision.Action is BargeInAction.Correction
                ? BargeInState.RegeneratingWithCorrection
                : BargeInState.CancellingCurrentTurn;
            _diagnostics.StateChanged(context, state, decision.Reason);
            await _playbackService.ClearQueueAsync(cancellationToken);
            await CancelLiveTurnAsync(context, classification, decision.Action, cancellationToken);
            _diagnostics.AssistantTurnCancelled(context, classification);
        }

        if (decision.Action is BargeInAction.Correction)
        {
            _diagnostics.CorrectionRegenerationStarted(context, classification.CorrectedUserMessage ?? classification.Reason);
            _diagnostics.Ignored(context, "Correction captured and current turn cancelled; DeepInfra correction regeneration is deferred until original-turn context is wired into barge-in.");
        }
    }

    private Task CancelLiveTurnAsync(
        BargeInSpeechContext context,
        InterruptionClassificationResult classification,
        BargeInAction action,
        CancellationToken cancellationToken)
    {
        var correlationId = string.IsNullOrWhiteSpace(context.CorrelationId)
            ? context.AssistantTurnId
            : context.CorrelationId;
        var reason = action is BargeInAction.Correction
            ? LiveAssistantTurnCancelReason.UserCorrection
            : LiveAssistantTurnCancelReason.UserHardStop;
        var correctionText = action is BargeInAction.Correction
            ? classification.CorrectedUserMessage ?? classification.Reason
            : null;

        return _liveTurnService.CancelTurnAsync(correlationId, reason, correctionText, cancellationToken);
    }

    private static async Task WaitForPostTriggerAudioAsync(
        BargeInAudioFrame triggerFrame,
        BargeInOptions options,
        CancellationToken cancellationToken)
    {
        var waitMs = Math.Clamp(
            options.TriggerPostSpeechWaitMs,
            0,
            Math.Max(0, options.TriggerMaxCaptureMs));
        if (waitMs <= 0 || triggerFrame.SampleRate <= 0)
        {
            return;
        }

        await Task.Delay(waitMs, cancellationToken);
    }

    private BargeInDecision Decide(
        BargeInSpeechContext context,
        InterruptionClassificationResult classification,
        BargeInOptions options)
    {
        if (_bargeInsThisTurn >= Math.Max(1, options.MaxBargeInsPerAssistantTurn))
        {
            return Rejected(classification, BargeInAction.Ignore, "Max barge-ins reached for this assistant turn.");
        }

        if (classification.Type is InterruptionType.None or InterruptionType.NoiseOrEcho)
        {
            return Rejected(classification, BargeInAction.Ignore, classification.Reason);
        }

        if (classification.Type is InterruptionType.Backchannel)
        {
            return options.BackchannelResumeEnabled
                ? Accepted(classification, BargeInAction.Resume, "Backchannel acknowledged; resume previous speech.")
                : Rejected(classification, BargeInAction.Ignore, "Backchannel resume is disabled.");
        }

        if (classification.Type is InterruptionType.SideComment)
        {
            return Accepted(classification, BargeInAction.SideComment, "Side comment captured; resume previous speech.");
        }

        if (classification.Type is InterruptionType.Pause)
        {
            return Accepted(classification, BargeInAction.Resume, "Pause phrase captured; true pause/resume is not implemented, so current speech resumes.");
        }

        if (classification.Type is InterruptionType.ClarificationQuestion)
        {
            return Accepted(classification, BargeInAction.Clarification, options.ClarificationResumeEnabled
                ? "Clarification captured; answer and resume is enabled."
                : "Clarification captured; answer-and-resume is not implemented yet, so current speech resumes.");
        }

        var requiredConfidence = classification.Type is InterruptionType.HardStop
            ? options.MinHardStopConfidence
            : options.MinClassifierConfidence;
        if (classification.Confidence < requiredConfidence)
        {
            return Rejected(classification, BargeInAction.Ignore, $"Classification confidence below threshold {requiredConfidence:N2}.");
        }

        return Accepted(
            classification,
            classification.Type is InterruptionType.Correction ? BargeInAction.Correction : BargeInAction.HardCancel,
            classification.Type is InterruptionType.Correction ? "Correction accepted; cancel current turn." : "Hard cancellation accepted.");
    }

    private static BargeInDecision Accepted(
        InterruptionClassificationResult classification,
        BargeInAction action,
        string reason)
    {
        return new BargeInDecision
        {
            Accepted = true,
            Action = action,
            Classification = classification,
            Reason = reason
        };
    }

    private static BargeInDecision Rejected(InterruptionClassificationResult classification, BargeInAction action, string reason)
    {
        return new BargeInDecision
        {
            Accepted = false,
            Action = action,
            Classification = classification,
            Reason = reason
        };
    }

    private async Task ResumePreviousSpeechAsync(BargeInSpeechContext context, string reason, CancellationToken cancellationToken)
    {
        _diagnostics.StateChanged(context, BargeInState.ResumingPreviousSpeech, reason);
        _speakerDuckingService.Restore(context, reason);
        await _playbackService.ResumeCurrentSpeechAsync(cancellationToken);
        _diagnostics.PlaybackResumed(context, reason);
    }

    private async void OnSpeechStarted(object? sender, BargeInSpeechContext context)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return;
        }

        await _aec.InitializeAsync(new AecConfiguration(options.AecSampleRate, options.FrameMs, options.AecProvider));
        lock (_syncRoot)
        {
            _activeContext = context;
            _handlingTrigger = false;
            _bargeInsThisTurn = 0;
        }

        _vadService.Reset();
        _triggerBuffer.Reset();
        _diagnostics.MonitorStarted(context, _aecMode);
    }

    private void OnSpeechStopped(object? sender, BargeInSpeechContext context)
    {
        var stopped = false;
        lock (_syncRoot)
        {
            if (_activeContext?.AssistantTurnId == context.AssistantTurnId)
            {
                _activeContext = null;
                _handlingTrigger = false;
                stopped = true;
            }
        }

        if (!stopped)
        {
            return;
        }

        _vadService.Reset();
        _triggerBuffer.Reset();
        _speakerDuckingService.Restore(context, "speech_stopped");
        _diagnostics.MonitorStopped(context);
    }

    private static TimeSpan CalculateDuration(IReadOnlyList<BargeInAudioFrame> frames)
    {
        if (frames.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var samples = frames.Sum(frame => frame.Samples.Length);
        var sampleRate = frames[0].SampleRate;
        return sampleRate <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(samples / (double)sampleRate);
    }

    public async ValueTask DisposeAsync()
    {
        _playbackReferenceTap.SpeechStarted -= OnSpeechStarted;
        _playbackReferenceTap.SpeechStopped -= OnSpeechStopped;
        await _aec.DisposeAsync();
    }
}
