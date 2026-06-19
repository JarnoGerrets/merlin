using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInCoordinator : IBargeInCoordinator, IAsyncDisposable
{
    private enum CaptureKind
    {
        NormalInterruption,
        FastHardStop
    }

    private readonly IAcousticEchoCancellationService _aec;
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly IInterruptionClassifier _interruptionClassifier;
    private readonly ILiveAssistantTurnService _liveTurnService;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly IInterruptionCaptureDiagnosticsWriter _captureDiagnosticsWriter;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly ILogger<BargeInCoordinator> _logger;
    private readonly IAssistantPlaybackMonitor _assistantPlaybackMonitor;
    private readonly ISpeakerDuckingService _speakerDuckingService;
    private readonly ISelfSpeechSuppressionGate _selfSpeechGate;
    private readonly IContinuousMicAudioBuffer _continuousMicAudioBuffer;
    private readonly IBargeInSttService _sttService;
    private readonly IBargeInTriggerBuffer _triggerBuffer;
    private readonly IBargeInVadService _vadService;
    private readonly object _syncRoot = new();
    private BargeInSpeechContext? _activeContext;
    private BargeInCaptureSession? _activeCaptureSession;
    private CancellationTokenSource? _duckingRestoreCancellation;
    private string? _duckingOwner;
    private long _duckingRestoreGeneration;
    private AecMode _aecMode = AecMode.DegradedNoOp;
    private bool _handlingTrigger;
    private int _bargeInsThisTurn;
    private int _fastHardStopSpeechMs;
    private BargeInAudioFrame? _fastHardStopLastSpeechFrame;
    private BargeInSpeechContext? _liveMonitorContext;
    private bool _suppressedFastHardStopAttemptSaved;
    private bool _suppressedVadTriggeredAttemptSaved;
    private int _fastNearEndSpeechMs;
    private DateTimeOffset? _fastNearEndFirstSpeechAt;
    private readonly BurstCaptureCandidateState _burstCaptureCandidate = new();

    public event Func<CorrectionRegenerationRequested, CancellationToken, Task>? CorrectionRegenerationRequested;

    public event Func<LiveUserUtteranceRouted, CancellationToken, Task>? LiveUserUtteranceRouted;

    public BargeInCoordinator(
        IPlaybackReferenceTap playbackReferenceTap,
        IAssistantPlaybackMonitor assistantPlaybackMonitor,
        IAcousticEchoCancellationService aec,
        IBargeInVadService vadService,
        ISpeakerDuckingService speakerDuckingService,
        ISelfSpeechSuppressionGate selfSpeechGate,
        IContinuousMicAudioBuffer continuousMicAudioBuffer,
        IBargeInTriggerBuffer triggerBuffer,
        IBargeInSttService sttService,
        IInterruptionClassifier interruptionClassifier,
        ILiveAssistantTurnService liveTurnService,
        IAssistantSpeechPlaybackService playbackService,
        IInterruptionCaptureDiagnosticsWriter captureDiagnosticsWriter,
        IBargeInDiagnosticsLogger diagnostics,
        ILogger<BargeInCoordinator> logger,
        IOptionsMonitor<BargeInOptions> options)
    {
        _playbackReferenceTap = playbackReferenceTap;
        _assistantPlaybackMonitor = assistantPlaybackMonitor;
        _aec = aec;
        _vadService = vadService;
        _speakerDuckingService = speakerDuckingService;
        _selfSpeechGate = selfSpeechGate;
        _continuousMicAudioBuffer = continuousMicAudioBuffer;
        _triggerBuffer = triggerBuffer;
        _sttService = sttService;
        _interruptionClassifier = interruptionClassifier;
        _liveTurnService = liveTurnService;
        _playbackService = playbackService;
        _captureDiagnosticsWriter = captureDiagnosticsWriter;
        _diagnostics = diagnostics;
        _logger = logger;
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
            if (context is null && _liveTurnService.TryGetCurrentActiveTurn(out var activeTurn))
            {
                context = CreateLiveContext(activeTurn);
                _activeContext = context;
            }

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
        var correlation = SelfSpeechCorrelationDetector.Analyze(
            frame.Samples.Span,
            frame.SampleRate,
            _playbackReferenceTap,
            options.SelfSpeechSuppression);
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
        BargeInCaptureSession? activeCaptureSession;
        lock (_syncRoot)
        {
            if (_handlingTrigger)
            {
                activeCaptureSession = _activeCaptureSession;
            }
            else
            {
                activeCaptureSession = null;
            }
        }

        if (activeCaptureSession is not null)
        {
            var activeVad = _vadService.ProcessFrame(
                new VadFrameInput
                {
                    Samples = echoReducedFrame.Samples,
                    SampleRate = echoReducedFrame.SampleRate,
                    Timestamp = echoReducedFrame.Timestamp
                },
                options);
            var activeGateResult = EvaluateSelfSpeechGate(
                echoReducedFrame,
                activeVad,
                aecResult,
                correlation,
                "active_capture_endpointing");
            activeCaptureSession.Observe(CreateCaptureFrameObservation(
                frame,
                echoReducedFrame,
                activeVad,
                activeGateResult,
                options));
            UpdateLiveDucking(context, echoReducedFrame, reference, correlation, options, cancellationToken);
            return;
        }

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

        var comfortGateResult = HandleFastNearEndDucking(
            context,
            echoReducedFrame,
            vad,
            aecResult,
            correlation,
            options,
            cancellationToken);
        if (TryPromoteBurstCapture(
                context,
                frame,
                echoReducedFrame,
                reference,
                vad,
                comfortGateResult,
                correlation,
                options,
                out var burstPromotionSummary))
        {
            _suppressedFastHardStopAttemptSaved = false;
            _suppressedVadTriggeredAttemptSaved = false;
            ResetFastHardStopCandidate();
            ResetFastNearEndDuckingCandidate();
            await StartTriggeredSpeechAsync(
                context,
                echoReducedFrame,
                vad,
                aecResult,
                options,
                CaptureKind.NormalInterruption,
                burstPromotionSummary,
                cancellationToken);
            return;
        }

        if (!vad.IsTriggered)
        {
            if (!vad.IsSpeech)
            {
                _suppressedFastHardStopAttemptSaved = false;
                _suppressedVadTriggeredAttemptSaved = false;
            }

            if (vad.IsSpeech)
            {
                var speechGateResult = EvaluateSelfSpeechGate(
                    echoReducedFrame,
                    vad,
                    aecResult,
                    correlation,
                    "fast_hard_stop_candidate");
                if (speechGateResult.Decision is not SelfSpeechDecision.Allow)
                {
                    _diagnostics.Ignored(context, $"Fast hard-stop candidate suppressed by self-speech gate. Decision: {speechGateResult.Decision}. Reason: {speechGateResult.Reason}");
                    if (!_suppressedFastHardStopAttemptSaved)
                    {
                        _suppressedFastHardStopAttemptSaved = true;
                        ScheduleSuppressedInterruptionDiagnostics(
                            context,
                            echoReducedFrame,
                            vad,
                            aecResult,
                            options,
                            "suppressed_fast_hard_stop_candidate",
                            $"Fast hard-stop candidate suppressed by self-speech gate. Decision: {speechGateResult.Decision}. Reason: {speechGateResult.Reason}",
                            CancellationToken.None);
                    }

                    ResetFastHardStopCandidate();
                    return;
                }
            }

            if (_assistantPlaybackMonitor.IsPlaybackActive
                && TryConsumeFastHardStopCandidate(echoReducedFrame, reference, vad, options, out var fastTriggerFrame))
            {
                await StartTriggeredSpeechAsync(
                    context,
                    fastTriggerFrame,
                    vad,
                    aecResult,
                    options,
                    CaptureKind.FastHardStop,
                    null,
                    cancellationToken);
            }

            return;
        }

        var gateResult = EvaluateSelfSpeechGate(
            echoReducedFrame,
            vad,
            aecResult,
            correlation,
            "vad_triggered_capture");
        if (gateResult.Decision is not SelfSpeechDecision.Allow)
        {
            _diagnostics.Ignored(context, $"Barge-in suppressed by self-speech gate. Decision: {gateResult.Decision}. Reason: {gateResult.Reason}");
            if (!_suppressedVadTriggeredAttemptSaved)
            {
                _suppressedVadTriggeredAttemptSaved = true;
                ScheduleSuppressedInterruptionDiagnostics(
                    context,
                    echoReducedFrame,
                    vad,
                    aecResult,
                    options,
                    "suppressed_vad_triggered_capture",
                    $"VAD-triggered capture suppressed by self-speech gate. Decision: {gateResult.Decision}. Reason: {gateResult.Reason}",
                    CancellationToken.None);
            }

            _vadService.Reset();
            ResetFastHardStopCandidate();
            return;
        }

        _suppressedFastHardStopAttemptSaved = false;
        _suppressedVadTriggeredAttemptSaved = false;
        ResetFastHardStopCandidate();
        ResetFastNearEndDuckingCandidate();
        await StartTriggeredSpeechAsync(
            context,
            echoReducedFrame,
            vad,
            aecResult,
            options,
            CaptureKind.NormalInterruption,
            null,
            cancellationToken);
    }

    public async Task StartLiveMonitoringAsync(CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return;
        }

        await _aec.InitializeAsync(new AecConfiguration(options.AecSampleRate, options.FrameMs, options.AecProvider), cancellationToken);
        var context = new BargeInSpeechContext
        {
            AssistantTurnId = "live-utterance-monitor",
            CorrelationId = null,
            SpeechType = SpeechPlaybackItemType.FinalAnswer,
            SpokenText = string.Empty
        };

        lock (_syncRoot)
        {
            _liveMonitorContext = context;
            _activeContext ??= context;
            _handlingTrigger = false;
            _activeCaptureSession = null;
            _duckingOwner = null;
            _suppressedFastHardStopAttemptSaved = false;
            _suppressedVadTriggeredAttemptSaved = false;
            ResetFastHardStopCandidate();
            _selfSpeechGate.Reset();
            CancelPendingDuckingRestore();
        }

        _vadService.Reset();
        _triggerBuffer.Reset("live_monitor_started", context.AssistantTurnId);
        _diagnostics.MonitorStarted(context, _aecMode);
    }

    private async Task StartTriggeredSpeechAsync(
        BargeInSpeechContext context,
        BargeInAudioFrame echoReducedFrame,
        VadFrameResult vad,
        AecProcessResult aecResult,
        BargeInOptions options,
        CaptureKind captureKind,
        BurstCapturePromotionSummary? burstPromotionSummary,
        CancellationToken cancellationToken)
    {
        _diagnostics.VadPossibleSpeech(context, vad, aecResult.Mode);
        if (options.EnableSpeakerDucking)
        {
            CancelPendingDuckingRestore();
            StartOwnedDucking(
                context,
                captureKind is CaptureKind.FastHardStop ? "fast_hard_stop_candidate" : "vad_triggered");
        }
        _diagnostics.StateChanged(
            context,
            BargeInState.SoftPausedForUserSpeech,
            captureKind is CaptureKind.FastHardStop
                ? "Fast hard-stop candidate detected while assistant is speaking; playback is ducked while capturing short interruption."
                : "VAD detected likely user speech; playback is ducked while capturing interruption.");
        if (options.PauseInsteadOfCancelOnSpeech)
        {
            await _playbackService.PauseCurrentSpeechAsync(cancellationToken);
        }

        BargeInCaptureSession captureSession;
        lock (_syncRoot)
        {
            if (_handlingTrigger)
            {
                return;
            }

            _handlingTrigger = true;
            captureSession = captureKind is CaptureKind.FastHardStop
                ? BargeInCaptureSession.CreateFastHardStop(echoReducedFrame.Timestamp, options)
                : BargeInCaptureSession.CreateNormal(echoReducedFrame.Timestamp, options);
            captureSession.Observe(CreateCaptureFrameObservation(
                echoReducedFrame,
                echoReducedFrame,
                vad,
                null,
                options));
            _activeCaptureSession = captureSession;
            ResetFastNearEndDuckingCandidate();
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await HandleTriggeredSpeechAsync(context, echoReducedFrame, vad, aecResult, captureSession, captureKind, burstPromotionSummary, cancellationToken);
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        _handlingTrigger = false;
                        if (ReferenceEquals(_activeCaptureSession, captureSession))
                        {
                            _activeCaptureSession = null;
                        }
                    }
                }
            },
            CancellationToken.None);
    }

    private bool TryConsumeFastHardStopCandidate(
        BargeInAudioFrame frame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        BargeInOptions options,
        out BargeInAudioFrame triggerFrame)
    {
        triggerFrame = frame;
        if (!options.EnableFastHardStopCapture)
        {
            return false;
        }

        if (vad.IsSpeech)
        {
            _fastHardStopSpeechMs = Math.Max(_fastHardStopSpeechMs, vad.ConsecutiveSpeechMs);
            _fastHardStopLastSpeechFrame = frame;
            return false;
        }

        var speechMs = _fastHardStopSpeechMs;
        var lastSpeechFrame = _fastHardStopLastSpeechFrame;
        ResetFastHardStopCandidate();
        if (lastSpeechFrame is null)
        {
            return false;
        }

        var normalTriggerMs = Math.Max(options.VadMinSpeechMs, options.VadTriggerSpeechMs);
        if (speechMs < Math.Max(1, options.FastHardStopMinSpeechMs) || speechMs >= normalTriggerMs)
        {
            return false;
        }

        triggerFrame = lastSpeechFrame;
        return true;
    }

    private void ResetFastHardStopCandidate()
    {
        _fastHardStopSpeechMs = 0;
        _fastHardStopLastSpeechFrame = null;
    }

    private SelfSpeechGateResult? HandleFastNearEndDucking(
        BargeInSpeechContext context,
        BargeInAudioFrame frame,
        VadFrameResult vad,
        AecProcessResult aecResult,
        SelfSpeechCorrelationResult correlation,
        BargeInOptions options,
        CancellationToken cancellationToken)
    {
        var duckingOptions = options.FastNearEndDucking;
        var comfortOptions = options.ComfortDucking;
        if (!options.EnableSpeakerDucking || !duckingOptions.Enabled || !comfortOptions.Enabled)
        {
            ResetFastNearEndDuckingCandidate();
            return null;
        }

        if (duckingOptions.RequireAssistantPlayback && !_assistantPlaybackMonitor.IsPlaybackActive)
        {
            ResetFastNearEndDuckingCandidate();
            return null;
        }

        if (!IsFastNearEndSpeechCandidate(vad, duckingOptions))
        {
            ResetFastNearEndDuckingCandidate();
            ScheduleDuckingRestore(context, Math.Max(0, comfortOptions.HangoverMs), cancellationToken);
            return null;
        }

        _fastNearEndSpeechMs = Math.Max(_fastNearEndSpeechMs, vad.ConsecutiveSpeechMs);
        _fastNearEndFirstSpeechAt ??= frame.Timestamp;
        if (_fastNearEndSpeechMs < Math.Max(1, comfortOptions.MinSpeechMs))
        {
            return null;
        }

        var gateResult = duckingOptions.UseSelfSpeechGate
            ? EvaluateSelfSpeechGate(
                frame,
                vad,
                aecResult,
                correlation,
                string.IsNullOrWhiteSpace(comfortOptions.InputReason)
                    ? "comfort_ducking"
                    : comfortOptions.InputReason)
            : new SelfSpeechGateResult
            {
                Decision = SelfSpeechDecision.Allow,
                Confidence = 1.0,
                Reason = "Fast near-end ducking self-speech gate disabled.",
                MicEnergy = vad.Energy,
                PlaybackEnergy = 0.0,
                EstimatedEchoEnergy = 0.0,
                UserSpeechScore = 1.0,
                SustainedUncertainFrames = 0
            };
        if (gateResult.Decision is SelfSpeechDecision.SuppressAsSelfEcho
            && (!comfortOptions.SuppressOnlyStrongSelfEcho || IsStrongSelfEchoSuppression(gateResult)))
        {
            var holdReason = _speakerDuckingService.IsDucked
                ? " Existing ducking is held through fast near-end hangover."
                : "";
            _diagnostics.Ignored(context, $"Comfort duck suppressed by confident self echo. DuckingOwner: {_duckingOwner ?? "(none)"}. RestoreGeneration: {_duckingRestoreGeneration}. InputReason: {comfortOptions.InputReason}. SelfSpeechDecision: {gateResult.Decision}. SelfSpeechReason: {gateResult.Reason}.{holdReason} ConsecutiveMs: {_fastNearEndSpeechMs}. VadConfidence: {vad.Confidence:N2}. Energy: {vad.Energy:N4}. NoiseFloor: {vad.NoiseFloor:N4}.");
            ResetFastNearEndDuckingCandidate();
            ScheduleDuckingRestore(context, Math.Max(0, comfortOptions.HangoverMs), cancellationToken);
            return gateResult;
        }

        if (gateResult.Decision is not SelfSpeechDecision.Allow && !comfortOptions.AllowUncertain)
        {
            _diagnostics.Ignored(context, $"Comfort duck ignored because uncertain near-end speech is disabled. DuckingOwner: {_duckingOwner ?? "(none)"}. RestoreGeneration: {_duckingRestoreGeneration}. InputReason: {comfortOptions.InputReason}. SelfSpeechDecision: {gateResult.Decision}. SelfSpeechReason: {gateResult.Reason}. ConsecutiveMs: {_fastNearEndSpeechMs}. VadConfidence: {vad.Confidence:N2}. Energy: {vad.Energy:N4}. NoiseFloor: {vad.NoiseFloor:N4}.");
            ResetFastNearEndDuckingCandidate();
            ScheduleDuckingRestore(context, Math.Max(0, comfortOptions.HangoverMs), cancellationToken);
            return gateResult;
        }

        var alreadyDucked = _speakerDuckingService.IsDucked;
        CancelPendingDuckingRestore();
        var duckReason = gateResult.Decision is not SelfSpeechDecision.Allow
            ? "comfort_ducking_uncertain_near_end"
            : "comfort_ducking_likely_user";
        StartOwnedDucking(context, duckReason);
        if (!alreadyDucked)
        {
            var latencyMs = _fastNearEndFirstSpeechAt is null
                ? 0
                : Math.Max(0, (int)Math.Round((DateTimeOffset.UtcNow - _fastNearEndFirstSpeechAt.Value).TotalMilliseconds));
            _diagnostics.StateChanged(
                context,
                BargeInState.SoftPausedForUserSpeech,
                $"Comfort duck started. DuckingOwner: {duckReason}. RestoreGeneration: {_duckingRestoreGeneration}. InputReason: {comfortOptions.InputReason}. SelfSpeechDecision: {gateResult.Decision}. SelfSpeechReason: {gateResult.Reason}. ConsecutiveMs: {_fastNearEndSpeechMs}. MinSpeechMs: {comfortOptions.MinSpeechMs}. VadConfidence: {vad.Confidence:N2}. Energy: {vad.Energy:N4}. NoiseFloor: {vad.NoiseFloor:N4}. DuckApplyLatencyMs: {latencyMs}. CaptureActive: {IsInterruptionCaptureActive()}.");
        }
        else
        {
            _diagnostics.Ignored(context, $"Comfort duck held. DuckingOwner: {_duckingOwner ?? "(none)"}. RestoreGeneration: {_duckingRestoreGeneration}. InputReason: {comfortOptions.InputReason}. SelfSpeechDecision: {gateResult.Decision}. SelfSpeechReason: {gateResult.Reason}. ConsecutiveMs: {_fastNearEndSpeechMs}. VadConfidence: {vad.Confidence:N2}. Energy: {vad.Energy:N4}. NoiseFloor: {vad.NoiseFloor:N4}. CaptureActive: {IsInterruptionCaptureActive()}.");
        }

        return gateResult;
    }

    private static bool IsFastNearEndSpeechCandidate(
        VadFrameResult vad,
        FastNearEndDuckingOptions options)
    {
        if (!vad.IsSpeech)
        {
            return false;
        }

        var noiseFloor = Math.Max(0.000001, vad.NoiseFloor);
        var energyRatio = vad.Energy / noiseFloor;
        return vad.Confidence >= Math.Clamp(options.MinVadConfidence, 0.0, 1.0)
            && vad.Energy >= Math.Max(0.0, options.MinAbsoluteEnergy)
            && energyRatio >= Math.Max(0.0, options.MinEnergyRatioOverNoise);
    }

    private static bool IsStrongSelfEchoSuppression(SelfSpeechGateResult gateResult)
    {
        return string.Equals(
                gateResult.CorrelationDecision,
                SelfSpeechCorrelationDecision.SelfEcho,
                StringComparison.Ordinal)
            || gateResult.Confidence >= 0.85;
    }

    private void ResetFastNearEndDuckingCandidate()
    {
        _fastNearEndSpeechMs = 0;
        _fastNearEndFirstSpeechAt = null;
    }

    private bool TryPromoteBurstCapture(
        BargeInSpeechContext context,
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult correlation,
        BargeInOptions options,
        out BurstCapturePromotionSummary summary)
    {
        summary = null!;
        var promotionOptions = options.BurstCapturePromotion;
        if (!promotionOptions.Enabled)
        {
            _burstCaptureCandidate.Reset();
            return false;
        }

        if (promotionOptions.RequireAssistantPlayback && !_assistantPlaybackMonitor.IsPlaybackActive)
        {
            ResetBurstCandidate(context, "assistant playback inactive");
            return false;
        }

        if (IsInterruptionCaptureActive())
        {
            return false;
        }

        var rawEnergy = CalculateRms(rawFrame.Samples.Span);
        var aecEnergy = CalculateRms(echoReducedFrame.Samples.Span);
        if (IsLikelyPlaybackLeakage(echoReducedFrame, playbackReference, vad, options, out var leakageReason))
        {
            ResetBurstCandidate(context, leakageReason);
            return false;
        }

        var comfortDuckingActive = _speakerDuckingService.IsDucked
            || string.Equals(_duckingOwner, "comfort_ducking_likely_user", StringComparison.Ordinal)
            || string.Equals(_duckingOwner, "comfort_ducking_uncertain_near_end", StringComparison.Ordinal);
        var speechLikeEnergy =
            rawEnergy >= Math.Max(0.0, options.CaptureContinuationRawEnergyThreshold)
            || aecEnergy >= Math.Max(0.0, options.CaptureContinuationAecEnergyThreshold);
        var gateContributes = gateResult?.Decision is SelfSpeechDecision.Allow or SelfSpeechDecision.Uncertain;
        var candidateActive = vad.IsSpeech || comfortDuckingActive || speechLikeEnergy || gateContributes;

        if (!candidateActive)
        {
            ResetBurstCandidate(context, "speech-like activity stopped");
            return false;
        }

        var strongSelfEcho = IsStrongSelfEchoForBurst(gateResult, correlation, options);
        var observation = new BurstCaptureObservation
        {
            TimestampUtc = echoReducedFrame.Timestamp,
            DurationMs = echoReducedFrame.DurationMs > 0
                ? echoReducedFrame.DurationMs
                : echoReducedFrame.SampleRate <= 0
                    ? Math.Max(1, options.FrameMs)
                    : Math.Max(1, (int)Math.Round(echoReducedFrame.Samples.Length * 1000.0 / echoReducedFrame.SampleRate)),
            VadSaysSpeech = vad.IsSpeech,
            ComfortDuckingActive = comfortDuckingActive,
            GateDecision = gateResult?.Decision,
            StrongSelfEcho = strongSelfEcho,
            RawEnergy = rawEnergy,
            AecEnergy = aecEnergy,
            CorrelationScore = gateResult?.CorrelationScore ?? correlation.CorrelationScore
        };

        var wasActive = _burstCaptureCandidate.HasFrames;
        var snapshot = _burstCaptureCandidate.Add(observation, promotionOptions);
        if (!wasActive)
        {
            _diagnostics.StateChanged(
                context,
                BargeInState.SoftPausedForUserSpeech,
                FormatBurstDiagnostic("Burst capture candidate started", snapshot, "speech-like activity observed"));
        }
        else
        {
            _diagnostics.Ignored(context, FormatBurstDiagnostic("Burst capture candidate updated", snapshot, "collecting sustained near-end evidence"));
        }

        if (snapshot.StrongSelfEchoFrames >= Math.Max(1, promotionOptions.StrongSelfEchoVetoMinFrames)
            && snapshot.StrongSelfEchoRatio >= Math.Clamp(promotionOptions.StrongSelfEchoVetoRatio, 0.0, 1.0))
        {
            _diagnostics.Ignored(
                context,
                FormatBurstDiagnostic("Burst capture promotion blocked by strong self-echo", snapshot, "strong self-echo dominates candidate burst"));
            _burstCaptureCandidate.Reset();
            return false;
        }

        if (snapshot.CandidateBurstMs < Math.Max(1, promotionOptions.MinBurstMs)
            || snapshot.TotalFrames < Math.Max(1, promotionOptions.MinCandidateFrames))
        {
            return false;
        }

        var speechEvidence =
            snapshot.VadSpeechFrameRatio >= Math.Clamp(promotionOptions.MinVadSpeechFrameRatio, 0.0, 1.0)
            || snapshot.ComfortDuckingFrames > 0
            || snapshot.AllowFrames > 0
            || (promotionOptions.AllowUncertainPromotion && snapshot.UncertainFrames > 0);
        if (!speechEvidence)
        {
            return false;
        }

        summary = new BurstCapturePromotionSummary
        {
            CaptureStartReason = "burst_capture_promotion",
            CandidateBurstMsAtPromotion = snapshot.CandidateBurstMs,
            BurstTotalFrames = snapshot.TotalFrames,
            BurstVadSpeechFrames = snapshot.VadSpeechFrames,
            BurstComfortDuckingFrames = snapshot.ComfortDuckingFrames,
            BurstAllowFrames = snapshot.AllowFrames,
            BurstUncertainFrames = snapshot.UncertainFrames,
            BurstSuppressAsSelfEchoFrames = snapshot.SuppressAsSelfEchoFrames,
            BurstStrongSelfEchoFrames = snapshot.StrongSelfEchoFrames,
            BurstStrongSelfEchoRatio = snapshot.StrongSelfEchoRatio,
            BurstPromotionReason = promotionOptions.AllowUncertainPromotion && snapshot.UncertainFrames > 0
                ? "sustained_uncertain_near_end_speech"
                : "sustained_near_end_speech"
        };
        _diagnostics.StateChanged(
            context,
            BargeInState.CapturingInterruption,
            FormatBurstDiagnostic("Burst capture promoted to normal capture", snapshot, summary.BurstPromotionReason));
        _burstCaptureCandidate.Reset();
        return true;
    }

    private void ResetBurstCandidate(BargeInSpeechContext context, string reason)
    {
        if (!_burstCaptureCandidate.HasFrames)
        {
            return;
        }

        var snapshot = _burstCaptureCandidate.Snapshot();
        _burstCaptureCandidate.Reset();
        _diagnostics.Ignored(context, FormatBurstDiagnostic("Burst capture candidate reset", snapshot, reason));
    }

    private static bool IsStrongSelfEchoForBurst(
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult correlation,
        BargeInOptions options)
    {
        var correlationDecision = gateResult?.CorrelationDecision ?? correlation.Decision;
        var correlationScore = gateResult?.CorrelationScore ?? correlation.CorrelationScore;
        if (string.Equals(correlationDecision, SelfSpeechCorrelationDecision.SelfEcho, StringComparison.Ordinal)
            && correlationScore >= Math.Clamp(options.SelfSpeechSuppression.CorrelationSelfEchoThreshold, 0.0, 1.0))
        {
            return true;
        }

        return gateResult is
        {
            Decision: SelfSpeechDecision.SuppressAsSelfEcho
        } && gateResult.Reason.Contains("strongly correlates", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBurstDiagnostic(
        string eventName,
        BurstCaptureSnapshot snapshot,
        string reason)
    {
        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "{0}. candidateBurstMs={1}. windowMs={2}. totalFrames={3}. vadSpeechFrames={4}. comfortDuckingFrames={5}. allowFrames={6}. uncertainFrames={7}. suppressAsSelfEchoFrames={8}. strongSelfEchoFrames={9}. strongSelfEchoRatio={10:N2}. rawEnergyMin={11:N4}. rawEnergyMax={12:N4}. rawEnergyAverage={13:N4}. aecEnergyMin={14:N4}. aecEnergyMax={15:N4}. aecEnergyAverage={16:N4}. correlationScoreMax={17:N3}. Reason: {18}.",
            eventName,
            snapshot.CandidateBurstMs,
            snapshot.WindowMs,
            snapshot.TotalFrames,
            snapshot.VadSpeechFrames,
            snapshot.ComfortDuckingFrames,
            snapshot.AllowFrames,
            snapshot.UncertainFrames,
            snapshot.SuppressAsSelfEchoFrames,
            snapshot.StrongSelfEchoFrames,
            snapshot.StrongSelfEchoRatio,
            snapshot.RawEnergyMin,
            snapshot.RawEnergyMax,
            snapshot.RawEnergyAverage,
            snapshot.AecEnergyMin,
            snapshot.AecEnergyMax,
            snapshot.AecEnergyAverage,
            snapshot.CorrelationScoreMax,
            reason);
    }

    private async Task HandleTriggeredSpeechAsync(
        BargeInSpeechContext context,
        BargeInAudioFrame triggerFrame,
        VadFrameResult vad,
        AecProcessResult aecResult,
        BargeInCaptureSession captureSession,
        CaptureKind captureKind,
        BurstCapturePromotionSummary? burstPromotionSummary,
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

        _diagnostics.StateChanged(context, BargeInState.CapturingInterruption, "Collecting post-trigger echo-reduced speech until end-of-utterance for gated STT.");
        var capturedUntil = await captureSession.WaitForEndpointAsync(cancellationToken);
        var triggeredCapture = _triggerBuffer.CaptureTriggeredWindowWithDiagnostics(
            triggerFrame,
            options,
            capturedUntil,
            context.AssistantTurnId);
        var requestedPreRollMs = Math.Max(0, options.TriggerPreRollMs);
        var continuousRange = _continuousMicAudioBuffer.GetAudioRange(
            triggerFrame.Timestamp,
            triggerFrame.Timestamp - TimeSpan.FromMilliseconds(requestedPreRollMs),
            capturedUntil,
            requestedPreRollMs,
            options);
        var builtFromContinuousRecorder = continuousRange.Frames.Count > 0;
        var captured = builtFromContinuousRecorder
            ? continuousRange.Frames
            : triggeredCapture.Frames;
        var duration = CalculateDuration(captured);
        _diagnostics.TriggerBufferCaptured(context, captured.Count, duration);
        _diagnostics.GatedSttStarted(context, duration);
        var stt = await _sttService.TranscribeTriggerAsync(captured, options, cancellationToken);
        _diagnostics.GatedSttResult(context, stt);
        _diagnostics.StateChanged(context, BargeInState.ClassifyingInterruption, $"Interruption transcript captured: {stt.Transcript}");

        var normalized = InterruptionClassifier.Normalize(stt.Transcript);
        var utterance = CreateUserUtterance(context, stt.Transcript, vad.Confidence);
        _diagnostics.StateChanged(
            context,
            BargeInState.ClassifyingInterruption,
            $"UserUtteranceCaptured. activeTurnId={utterance.ActiveTurnId ?? "(none)"} stateWhenCaptured={utterance.StateWhenCaptured} assistantWasSpeaking={utterance.AssistantWasSpeaking} text={utterance.Text}");
        _logger.LogInformation(
            "UserUtteranceCaptured. Text: {Text}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. AssistantWasSpeaking: {AssistantWasSpeaking}. Confidence: {Confidence}.",
            utterance.Text,
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            utterance.StateWhenCaptured,
            utterance.AssistantWasSpeaking,
            utterance.Confidence);
        var routeDecision = RouteUtterance(utterance);
        _logger.LogInformation(
            "UserUtteranceRouted. Text: {Text}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. Route: {Route}. Confidence: {Confidence}. Action: {Action}. Reason: {Reason}.",
            utterance.Text,
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            utterance.StateWhenCaptured,
            routeDecision.Kind,
            routeDecision.Confidence,
            routeDecision.Action,
            routeDecision.Reason);
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
        var decision = captureKind is CaptureKind.FastHardStop
            ? DecideFastHardStop(context, classification, options)
            : Decide(context, classification, options);
        _diagnostics.ActionSelected(context, decision.Action, decision.Reason);
        await SaveInterruptionCaptureDiagnosticsAsync(
            context,
            captured,
            duration,
            stt,
            normalized,
            classification,
            decision,
            vad,
            aecResult,
            options,
            triggeredCapture,
            continuousRange,
            builtFromContinuousRecorder,
            captureSession,
            triggerFrame,
            capturedUntil,
            captureKind,
            burstPromotionSummary,
            cancellationToken);
        if (await TryHandleLiveUtteranceRouteAsync(context, utterance, routeDecision, cancellationToken))
        {
            _vadService.Reset();
            return;
        }

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
            var correctionText = classification.CorrectedUserMessage ?? classification.Reason;
            _diagnostics.CorrectionRegenerationStarted(context, correctionText);
            await RaiseCorrectionRegenerationRequestedAsync(context, correctionText, cancellationToken);
        }
    }

    private void ScheduleSuppressedInterruptionDiagnostics(
        BargeInSpeechContext context,
        BargeInAudioFrame triggerFrame,
        VadFrameResult vad,
        AecProcessResult aecResult,
        BargeInOptions options,
        string captureKind,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!options.SaveDebugAudio)
        {
            return;
        }

        var contextSnapshot = context;
        var triggerSnapshot = triggerFrame;
        var vadSnapshot = vad;
        var aecSnapshot = aecResult;
        var optionsSnapshot = options;
        var captureKindSnapshot = captureKind;
        var reasonSnapshot = reason;
        var delayMs = Math.Clamp(
            Math.Max(options.VadEndSilenceMs, options.FastHardStopPostSpeechPaddingMs),
            150,
            750);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, cancellationToken);
                await SaveSuppressedInterruptionDiagnosticsAsync(
                    contextSnapshot,
                    triggerSnapshot,
                    vadSnapshot,
                    aecSnapshot,
                    optionsSnapshot,
                    captureKindSnapshot,
                    reasonSnapshot,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }, CancellationToken.None);
    }

    private Task SaveSuppressedInterruptionDiagnosticsAsync(
        BargeInSpeechContext context,
        BargeInAudioFrame triggerFrame,
        VadFrameResult vad,
        AecProcessResult aecResult,
        BargeInOptions options,
        string captureKind,
        string reason,
        CancellationToken cancellationToken)
    {
        var requestedPreRollMs = Math.Max(0, options.TriggerPreRollMs);
        var postMs = Math.Clamp(
            Math.Max(options.VadEndSilenceMs, options.FastHardStopPostSpeechPaddingMs),
            150,
            750);
        var captureEndUtc = triggerFrame.Timestamp.AddMilliseconds(postMs);
        var continuousRange = _continuousMicAudioBuffer.GetAudioRange(
            triggerFrame.Timestamp,
            triggerFrame.Timestamp - TimeSpan.FromMilliseconds(requestedPreRollMs),
            captureEndUtc,
            requestedPreRollMs,
            options);
        var captured = continuousRange.Frames.Count > 0
            ? continuousRange.Frames
            : [triggerFrame];
        if (captured.Count == 0)
        {
            return Task.CompletedTask;
        }

        var duration = CalculateDuration(captured);
        var diagnostic = new InterruptionCaptureDiagnostic
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            CaptureKind = captureKind,
            AssistantTurnId = context.AssistantTurnId,
            CorrelationId = context.CorrelationId,
            SpeechType = context.SpeechType.ToString(),
            AssistantWasSpeaking = _assistantPlaybackMonitor.IsPlaybackActive,
            DuckingWasActive = _speakerDuckingService.IsDucked,
            FrameCount = captured.Count,
            AudioMs = (int)Math.Round(duration.TotalMilliseconds),
            PreRollMs = requestedPreRollMs,
            RequestedPreRollMs = continuousRange.RequestedPreRollMs,
            ActualPreRollMsAvailable = continuousRange.ActualPreRollMsAvailable,
            ActualPreRollMsIncluded = continuousRange.ActualPreRollMsIncluded,
            PreRollFramesIncluded = continuousRange.PreRollFramesIncluded,
            OldestBufferedFrameAgeMs = continuousRange.OldestBufferedFrameAgeMs,
            BufferResetReason = "continuous_recorder",
            BufferOwnerAssistantTurnId = context.AssistantTurnId,
            CurrentAssistantTurnId = context.AssistantTurnId,
            PostPaddingMs = postMs,
            MaxCaptureMs = Math.Max(1, options.FastHardStopCaptureWindowMs),
            CaptureEndReason = "suppressed_before_stt",
            FirstSpeechFrameRelativeMs = 0,
            LastSpeechFrameRelativeMs = (int)Math.Round(duration.TotalMilliseconds),
            CapturedSpeechMs = (int)Math.Round(duration.TotalMilliseconds),
            RawSpeechFrames = captured.Count,
            AecSpeechFrames = aecResult.IsEchoCancellationActive ? captured.Count : 0,
            VadSpeechFrames = vad.IsSpeech ? captured.Count : 0,
            CaptureSpeechFrames = captured.Count,
            FalseSilenceFramesWhileVadSpeech = 0,
            CaptureStartUtc = captured[0].Timestamp,
            CaptureEndUtc = captured[^1].Timestamp,
            CaptureWallClockMs = (int)Math.Max(0, Math.Round((captured[^1].Timestamp - captured[0].Timestamp).TotalMilliseconds)),
            SttInputAudioMs = (int)Math.Round(duration.TotalMilliseconds),
            ContinuousRecorderBufferMs = continuousRange.ContinuousRecorderBufferMs,
            AnalysisFramesDropped = triggerFrame.AnalysisFramesDropped,
            ContinuousFramesDropped = continuousRange.ContinuousFramesDropped,
            MaxProcessingLagMs = 0,
            AverageProcessingLagMs = 0,
            FrameGapCount = continuousRange.FrameGapCount,
            MaxCaptureFrameGapMs = continuousRange.MaxCaptureFrameGapMs,
            BuiltFromContinuousRecorder = true,
            SampleRate = captured[0].SampleRate,
            SampleCount = captured.Sum(frame => frame.Samples.Length),
            SttTranscript = string.Empty,
            NormalizedTranscript = string.Empty,
            ClassificationType = InterruptionType.NoiseOrEcho.ToString(),
            ClassificationConfidence = 0,
            ClassificationReason = reason,
            DecisionAction = BargeInAction.Ignore.ToString(),
            DecisionAccepted = false,
            DecisionReason = reason,
            VadConfidence = vad.Confidence,
            WasWakeWordPresent = false,
            IsAecDegraded = !aecResult.IsEchoCancellationActive,
            AecMode = aecResult.Mode.ToString(),
            WavPath = null,
            JsonPath = null,
            FramesJsonlPath = null
        };

        return _captureDiagnosticsWriter.SaveAsync(
            diagnostic,
            captured,
            [],
            cancellationToken);
    }

    private Task SaveInterruptionCaptureDiagnosticsAsync(
        BargeInSpeechContext context,
        IReadOnlyList<BargeInAudioFrame> captured,
        TimeSpan duration,
        BargeInSttResult stt,
        string normalizedTranscript,
        InterruptionClassificationResult classification,
        BargeInDecision decision,
        VadFrameResult vad,
        AecProcessResult aecResult,
        BargeInOptions options,
        BargeInTriggeredCapture triggeredCapture,
        ContinuousMicAudioRange continuousRange,
        bool builtFromContinuousRecorder,
        BargeInCaptureSession captureSession,
        BargeInAudioFrame triggerFrame,
        DateTimeOffset capturedUntil,
        CaptureKind captureKind,
        BurstCapturePromotionSummary? burstPromotionSummary,
        CancellationToken cancellationToken)
    {
        var diagnostic = new InterruptionCaptureDiagnostic
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            CaptureKind = GetCaptureKindName(captureKind),
            AssistantTurnId = context.AssistantTurnId,
            CorrelationId = context.CorrelationId,
            SpeechType = context.SpeechType.ToString(),
            AssistantWasSpeaking = _assistantPlaybackMonitor.IsPlaybackActive,
            DuckingWasActive = _speakerDuckingService.IsDucked,
            FrameCount = captured.Count,
            AudioMs = (int)Math.Round(duration.TotalMilliseconds),
            PreRollMs = Math.Max(0, options.TriggerPreRollMs),
            RequestedPreRollMs = builtFromContinuousRecorder ? continuousRange.RequestedPreRollMs : triggeredCapture.RequestedPreRollMs,
            ActualPreRollMsAvailable = builtFromContinuousRecorder ? continuousRange.ActualPreRollMsAvailable : triggeredCapture.ActualPreRollMsAvailable,
            ActualPreRollMsIncluded = builtFromContinuousRecorder ? continuousRange.ActualPreRollMsIncluded : triggeredCapture.ActualPreRollMsIncluded,
            PreRollFramesIncluded = builtFromContinuousRecorder ? continuousRange.PreRollFramesIncluded : triggeredCapture.PreRollFramesIncluded,
            OldestBufferedFrameAgeMs = builtFromContinuousRecorder ? continuousRange.OldestBufferedFrameAgeMs : triggeredCapture.OldestBufferedFrameAgeMs,
            BufferResetReason = triggeredCapture.BufferResetReason,
            BufferOwnerAssistantTurnId = triggeredCapture.BufferOwnerAssistantTurnId,
            CurrentAssistantTurnId = triggeredCapture.CurrentAssistantTurnId,
            PostPaddingMs = captureKind is CaptureKind.FastHardStop
                ? Math.Max(0, options.FastHardStopPostSpeechPaddingMs)
                : Math.Max(0, options.VadEndSilenceMs),
            MaxCaptureMs = captureKind is CaptureKind.FastHardStop
                ? Math.Max(1, options.FastHardStopCaptureWindowMs)
                : BargeInCaptureTiming.GetMaxCaptureMs(options),
            CaptureEndReason = captureSession.EndReason,
            FirstSpeechFrameRelativeMs = captureSession.FirstSpeechFrameRelativeMs,
            LastSpeechFrameRelativeMs = captureSession.LastSpeechFrameRelativeMs,
            CapturedSpeechMs = captureSession.CapturedSpeechMs,
            RawSpeechFrames = captureSession.RawSpeechFrames,
            AecSpeechFrames = captureSession.AecSpeechFrames,
            VadSpeechFrames = captureSession.VadSpeechFrames,
            CaptureSpeechFrames = captureSession.CaptureSpeechFrames,
            FalseSilenceFramesWhileVadSpeech = captureSession.FalseSilenceFramesWhileVadSpeech,
            CaptureStartUtc = triggerFrame.Timestamp,
            CaptureEndUtc = capturedUntil,
            CaptureWallClockMs = (int)Math.Max(0, Math.Round((capturedUntil - triggerFrame.Timestamp).TotalMilliseconds)),
            SttInputAudioMs = (int)Math.Round(duration.TotalMilliseconds),
            ContinuousRecorderBufferMs = continuousRange.ContinuousRecorderBufferMs,
            AnalysisFramesDropped = captureSession.AnalysisFramesDropped,
            ContinuousFramesDropped = continuousRange.ContinuousFramesDropped,
            MaxProcessingLagMs = captureSession.MaxProcessingLagMs,
            AverageProcessingLagMs = captureSession.AverageProcessingLagMs,
            FrameGapCount = builtFromContinuousRecorder ? continuousRange.FrameGapCount : 0,
            MaxCaptureFrameGapMs = builtFromContinuousRecorder ? continuousRange.MaxCaptureFrameGapMs : 0,
            BuiltFromContinuousRecorder = builtFromContinuousRecorder,
            SampleRate = captured.Count == 0 ? 0 : captured[0].SampleRate,
            SampleCount = captured.Count == 0 ? 0 : captured.Sum(frame => frame.Samples.Length),
            SttTranscript = stt.Transcript,
            NormalizedTranscript = normalizedTranscript,
            ClassificationType = classification.Type.ToString(),
            ClassificationConfidence = classification.Confidence,
            ClassificationReason = classification.Reason,
            DecisionAction = decision.Action.ToString(),
            DecisionAccepted = decision.Accepted,
            DecisionReason = decision.Reason,
            VadConfidence = vad.Confidence,
            WasWakeWordPresent = options.WakeWords.Any(wakeWord => normalizedTranscript.StartsWith(InterruptionClassifier.Normalize(wakeWord), StringComparison.OrdinalIgnoreCase)),
            IsAecDegraded = !aecResult.IsEchoCancellationActive,
            AecMode = aecResult.Mode.ToString(),
            CaptureStartReason = burstPromotionSummary?.CaptureStartReason,
            CandidateBurstMsAtPromotion = burstPromotionSummary?.CandidateBurstMsAtPromotion,
            BurstTotalFrames = burstPromotionSummary?.BurstTotalFrames,
            BurstVadSpeechFrames = burstPromotionSummary?.BurstVadSpeechFrames,
            BurstComfortDuckingFrames = burstPromotionSummary?.BurstComfortDuckingFrames,
            BurstAllowFrames = burstPromotionSummary?.BurstAllowFrames,
            BurstUncertainFrames = burstPromotionSummary?.BurstUncertainFrames,
            BurstSuppressAsSelfEchoFrames = burstPromotionSummary?.BurstSuppressAsSelfEchoFrames,
            BurstStrongSelfEchoFrames = burstPromotionSummary?.BurstStrongSelfEchoFrames,
            BurstStrongSelfEchoRatio = burstPromotionSummary?.BurstStrongSelfEchoRatio,
            BurstPromotionReason = burstPromotionSummary?.BurstPromotionReason,
            WavPath = null,
            JsonPath = null,
            FramesJsonlPath = null
        };

        return _captureDiagnosticsWriter.SaveAsync(
            diagnostic,
            captured,
            captureSession.GetFrameDiagnostics(),
            cancellationToken);
    }

    private static string GetCaptureKindName(CaptureKind captureKind)
    {
        return captureKind is CaptureKind.FastHardStop
            ? "fast_hard_stop"
            : "normal_interruption";
    }

    private async Task RaiseCorrectionRegenerationRequestedAsync(
        BargeInSpeechContext context,
        string correctionText,
        CancellationToken cancellationToken)
    {
        var correlationId = string.IsNullOrWhiteSpace(context.CorrelationId)
            ? context.AssistantTurnId
            : context.CorrelationId;
        var handlers = CorrectionRegenerationRequested;
        if (handlers is null)
        {
            _diagnostics.Ignored(context, "Correction captured and current turn cancelled; no correction regeneration handler is attached.");
            return;
        }

        var request = new CorrectionRegenerationRequested
        {
            OriginalCorrelationId = correlationId,
            CorrectionText = correctionText,
            SpeechContext = context
        };

        foreach (Func<CorrectionRegenerationRequested, CancellationToken, Task> handler in handlers.GetInvocationList())
        {
            await handler(request, cancellationToken);
        }
    }

    private async Task RaiseLiveUserUtteranceRoutedAsync(
        UserUtterance utterance,
        UtteranceRouteDecision decision,
        CancellationToken cancellationToken)
    {
        var handlers = LiveUserUtteranceRouted;
        if (handlers is null)
        {
            return;
        }

        var routed = new LiveUserUtteranceRouted
        {
            Utterance = utterance,
            Decision = decision
        };

        foreach (Func<LiveUserUtteranceRouted, CancellationToken, Task> handler in handlers.GetInvocationList())
        {
            await handler(routed, cancellationToken);
        }
    }

    private void UpdateLiveDucking(
        BargeInSpeechContext context,
        BargeInAudioFrame frame,
        ReadOnlyMemory<float> playbackReference,
        SelfSpeechCorrelationResult correlation,
        BargeInOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.EnableSpeakerDucking)
        {
            return;
        }

        if (IsSpeechFrame(frame, options))
        {
            var gateResult = EvaluateSelfSpeechGate(
                frame,
                null,
                null,
                correlation,
                "live_ducking");
            if (gateResult.Decision is not SelfSpeechDecision.Allow)
            {
                _diagnostics.Ignored(context, $"Ducking gate did not allow active capture frame. Decision: {gateResult.Decision}. Reason: {gateResult.Reason}. Existing capture ducking is held until sustained silence or capture completion.");
                CancelPendingDuckingRestore();
                StartOwnedDucking(context, "capture_active_frame");
                return;
            }

            CancelPendingDuckingRestore();
            StartOwnedDucking(context, "vad_active_frame");
            return;
        }

        ScheduleDuckingRestore(context, options.DuckingSpeechHangoverMs, cancellationToken);
    }

    private void ScheduleDuckingRestore(
        BargeInSpeechContext context,
        int hangoverMs,
        CancellationToken cancellationToken)
    {
        if (!_speakerDuckingService.IsDucked)
        {
            return;
        }

        CancellationTokenSource restoreCancellation;
        CancellationToken restoreToken;
        long restoreGeneration;
        string? restoreOwner;
        lock (_syncRoot)
        {
            if (_duckingRestoreCancellation is not null)
            {
                return;
            }

            if (IsInterruptionCaptureActiveLocked())
            {
                return;
            }

            _duckingRestoreCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            restoreCancellation = _duckingRestoreCancellation;
            restoreToken = restoreCancellation.Token;
            restoreGeneration = _duckingRestoreGeneration;
            restoreOwner = _duckingOwner;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(
                        TimeSpan.FromMilliseconds(Math.Max(1, hangoverMs)),
                        restoreToken);
                    if (TryConsumePendingDuckingRestore(restoreCancellation, restoreGeneration, restoreOwner))
                    {
                        _diagnostics.Ignored(context, $"Comfort duck restored. DuckingOwner: {restoreOwner ?? "(none)"}. RestoreGeneration: {restoreGeneration}. RestoreReason: speech_hangover_elapsed.");
                        _speakerDuckingService.Restore(context, "speech_hangover_elapsed");
                    }
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(_duckingRestoreCancellation, restoreCancellation))
                        {
                            _duckingRestoreCancellation = null;
                        }
                    }

                    restoreCancellation.Dispose();
                }
            },
            CancellationToken.None);
    }

    private void CancelPendingDuckingRestore()
    {
        CancellationTokenSource? cancellation;
        lock (_syncRoot)
        {
            _duckingRestoreGeneration++;
            cancellation = _duckingRestoreCancellation;
            _duckingRestoreCancellation = null;
        }

        cancellation?.Cancel();
    }

    private bool TryConsumePendingDuckingRestore(
        CancellationTokenSource restoreCancellation,
        long restoreGeneration,
        string? restoreOwner)
    {
        lock (_syncRoot)
        {
            if (!ReferenceEquals(_duckingRestoreCancellation, restoreCancellation))
            {
                return false;
            }

            if (_duckingRestoreGeneration != restoreGeneration)
            {
                return false;
            }

            if (IsInterruptionCaptureActiveLocked())
            {
                return false;
            }

            if (!string.Equals(_duckingOwner, restoreOwner, StringComparison.Ordinal))
            {
                return false;
            }

            _duckingRestoreCancellation = null;
            _duckingOwner = null;
            _duckingRestoreGeneration++;
            return true;
        }
    }

    private bool IsInterruptionCaptureActiveLocked()
    {
        return _handlingTrigger || _activeCaptureSession is not null;
    }

    private bool IsInterruptionCaptureActive()
    {
        lock (_syncRoot)
        {
            return IsInterruptionCaptureActiveLocked();
        }
    }

    private void StartOwnedDucking(BargeInSpeechContext context, string owner)
    {
        lock (_syncRoot)
        {
            _duckingOwner = owner;
        }

        _speakerDuckingService.StartDucking(context, owner);
    }

    private void RestoreOwnedDucking(BargeInSpeechContext context, string reason)
    {
        lock (_syncRoot)
        {
            _duckingOwner = null;
        }

        _speakerDuckingService.Restore(context, reason);
    }

    private SelfSpeechGateResult EvaluateSelfSpeechGate(
        BargeInAudioFrame frame,
        VadFrameResult? vad,
        AecProcessResult? aecResult,
        SelfSpeechCorrelationResult correlation,
        string reason)
    {
        var now = frame.Timestamp;
        var playbackStartedAt = _assistantPlaybackMonitor.PlaybackStartedAt;
        var currentPlaybackEnergy = _assistantPlaybackMonitor.CurrentPlaybackEnergy;
        var recentPlaybackEnergy = _assistantPlaybackMonitor.RecentPlaybackEnergy;
        var input = new SelfSpeechGateInput
        {
            AssistantPlaybackActive = _assistantPlaybackMonitor.IsPlaybackActive,
            MicEnergy = vad?.Energy ?? CalculateRms(frame.Samples.Span),
            PlaybackEnergy = Math.Max(currentPlaybackEnergy, recentPlaybackEnergy),
            CurrentPlaybackEnergy = currentPlaybackEnergy,
            RecentPlaybackEnergy = recentPlaybackEnergy,
            AecVerified = aecResult?.IsEchoCancellationActive == true,
            VadSaysSpeech = vad?.IsSpeech ?? IsSpeechFrame(frame, _options.CurrentValue),
            VadConfidence = vad?.Confidence,
            Timestamp = now,
            PlaybackAge = playbackStartedAt is null ? null : now - playbackStartedAt.Value,
            Reason = reason,
            CorrelationId = _activeContext?.CorrelationId,
            CorrelationScore = correlation.CorrelationScore,
            BestDelayMs = correlation.BestDelayMs,
            CorrelationDecision = correlation.Decision,
            CorrelationAvailable = correlation.IsAvailable,
            CorrelationReason = correlation.Reason,
            ReferenceWindowAvailable = correlation.ReferenceWindowAvailable,
            ReferenceWindowEnergy = correlation.ReferenceWindowEnergy,
            ReferenceWindowSampleCount = correlation.ReferenceWindowSampleCount,
            RequestedMicSampleCount = correlation.RequestedMicSampleCount,
            RequestedDelayMinMs = correlation.RequestedDelayMinMs,
            RequestedDelayMaxMs = correlation.RequestedDelayMaxMs,
            RequestedDelayStepMs = correlation.RequestedDelayStepMs,
            PlaybackRingBufferedSamples = correlation.PlaybackRingBufferedSamples,
            PlaybackRingCapacitySamples = correlation.PlaybackRingCapacitySamples,
            PlaybackRingBufferedMs = correlation.PlaybackRingBufferedMs,
            PlaybackTapSampleRate = correlation.PlaybackTapSampleRate,
            MicSampleRate = correlation.MicSampleRate,
            SampleRateMatches = correlation.SampleRateMatches,
            PlaybackWritePosition = correlation.PlaybackWritePosition,
            NumberOfDelayWindowsChecked = correlation.NumberOfDelayWindowsChecked,
            NumberOfDelayWindowsAvailable = correlation.NumberOfDelayWindowsAvailable,
            NumberOfDelayWindowsSkippedLowEnergy = correlation.NumberOfDelayWindowsSkippedLowEnergy,
            MaxReferenceEnergySeen = correlation.MaxReferenceEnergySeen,
            CorrelationUnavailableReason = correlation.CorrelationUnavailableReason,
            PlaybackReferenceSource = correlation.PlaybackReferenceSource,
            PlaybackReferenceIsConsumptionAligned = correlation.PlaybackReferenceIsConsumptionAligned,
            PlaybackConsumedSamplesTotal = correlation.PlaybackConsumedSamplesTotal,
            ReferenceBufferedMs = correlation.ReferenceBufferedMs,
            ReferenceNewestAgeMs = correlation.ReferenceNewestAgeMs,
            ReferenceOldestAgeMs = correlation.ReferenceOldestAgeMs,
            OutputReadSamples = correlation.OutputReadSamples,
            OutputReadDurationMs = correlation.OutputReadDurationMs,
            LastOutputReadAtUtc = correlation.LastOutputReadAtUtc
        };

        return _selfSpeechGate.Evaluate(input, _options.CurrentValue);
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

    private BargeInDecision DecideFastHardStop(
        BargeInSpeechContext context,
        InterruptionClassificationResult classification,
        BargeInOptions options)
    {
        if (classification.Type is not InterruptionType.HardStop)
        {
            return Rejected(
                classification,
                BargeInAction.Ignore,
                $"Fast hard-stop rejected because transcript classified as {classification.Type}.");
        }

        if (classification.Confidence < Math.Max(options.MinHardStopConfidence, options.FastHardStopMinConfidence))
        {
            return Rejected(
                classification,
                BargeInAction.Ignore,
                $"Fast hard-stop confidence below threshold {Math.Max(options.MinHardStopConfidence, options.FastHardStopMinConfidence):N2}.");
        }

        return Accepted(classification, BargeInAction.HardCancel, "Fast hard-stop accepted.");
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
        RestoreOwnedDucking(context, reason);
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
        var shouldResetTriggerBuffer = false;
        lock (_syncRoot)
        {
            shouldResetTriggerBuffer = _activeContext?.AssistantTurnId != context.AssistantTurnId;
            _activeContext = context;
            _handlingTrigger = false;
            _activeCaptureSession = null;
            _duckingOwner = null;
            _suppressedFastHardStopAttemptSaved = false;
            _suppressedVadTriggeredAttemptSaved = false;
            ResetFastHardStopCandidate();
            _selfSpeechGate.Reset();
            CancelPendingDuckingRestore();
            _bargeInsThisTurn = 0;
        }

        _vadService.Reset();
        if (shouldResetTriggerBuffer)
        {
            _triggerBuffer.Reset("speech_started", context.AssistantTurnId);
        }

        _diagnostics.MonitorStarted(context, _aecMode);
        if (!string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            _liveTurnService.UpdateTurnState(context.CorrelationId, LiveAssistantTurnState.Speaking);
        }
    }

    private void OnSpeechStopped(object? sender, BargeInSpeechContext context)
    {
        var stopped = false;
        lock (_syncRoot)
        {
            if (_activeContext?.AssistantTurnId == context.AssistantTurnId)
            {
                _activeContext = _liveMonitorContext;
                _handlingTrigger = false;
                _activeCaptureSession = null;
                _duckingOwner = null;
                _suppressedFastHardStopAttemptSaved = false;
                _suppressedVadTriggeredAttemptSaved = false;
                _burstCaptureCandidate.Reset();
                ResetFastHardStopCandidate();
                _selfSpeechGate.Reset();
                CancelPendingDuckingRestore();
                stopped = true;
            }
        }

        if (!stopped)
        {
            return;
        }

        _vadService.Reset();
        _triggerBuffer.Reset("speech_stopped", context.AssistantTurnId);
        RestoreOwnedDucking(context, "speech_stopped");
        _diagnostics.MonitorStopped(context);
    }

    private BargeInSpeechContext CreateLiveContext(LiveAssistantTurn turn)
    {
        return new BargeInSpeechContext
        {
            AssistantTurnId = turn.AssistantTurnId,
            CorrelationId = turn.CorrelationId,
            SpeechType = SpeechPlaybackItemType.FinalAnswer,
            SpokenText = turn.PendingCommandDescription ?? string.Empty
        };
    }

    private UserUtterance CreateUserUtterance(
        BargeInSpeechContext context,
        string transcript,
        double confidence)
    {
        LiveAssistantTurnState state = _assistantPlaybackMonitor.IsPlaybackActive
            ? LiveAssistantTurnState.Speaking
            : LiveAssistantTurnState.ProcessingTurn;
        string? activeTurnId = context.AssistantTurnId;
        string? correlationId = context.CorrelationId;
        var playbackActive = _assistantPlaybackMonitor.IsPlaybackActive;
        if (!string.IsNullOrWhiteSpace(correlationId)
            && _liveTurnService.TryGetActiveTurn(correlationId, out var turn))
        {
            state = playbackActive ? LiveAssistantTurnState.Speaking : turn.State;
            activeTurnId = turn.AssistantTurnId;
        }
        else if (_liveTurnService.TryGetCurrentActiveTurn(out var currentTurn))
        {
            state = playbackActive ? LiveAssistantTurnState.Speaking : currentTurn.State;
            activeTurnId = currentTurn.AssistantTurnId;
            correlationId = currentTurn.CorrelationId;
        }

        return new UserUtterance
        {
            Text = transcript.Trim(),
            TimestampUtc = DateTimeOffset.UtcNow,
            ActiveTurnId = activeTurnId,
            CorrelationId = correlationId,
            StateWhenCaptured = state,
            AssistantWasSpeaking = playbackActive || state is LiveAssistantTurnState.Speaking,
            Source = "live_utterance_monitor",
            Confidence = confidence
        };
    }

    private static UtteranceRouteDecision RouteUtterance(UserUtterance utterance)
    {
        var normalized = InterruptionClassifier.Normalize(utterance.Text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Decision(UtteranceRouteKind.BackgroundOrNoOp, 0.2, "Empty transcript.", "Ignore");
        }

        if (IsExplicitCancel(normalized))
        {
            return Decision(UtteranceRouteKind.CancelActiveTurn, 0.95, "Explicit cancellation phrase.", "CancelActiveTurn");
        }

        if (IsPausePhrase(normalized))
        {
            var action = utterance.StateWhenCaptured switch
            {
                LiveAssistantTurnState.Speaking => "StopSpeechOnlyNoConfirmation",
                LiveAssistantTurnState.AwaitingToolCommit or LiveAssistantTurnState.PlanningTool => "PauseAndConfirmCancel",
                LiveAssistantTurnState.ExecutingTool => "TryCancelThenClarifyIfNeeded",
                LiveAssistantTurnState.ProcessingTurn or LiveAssistantTurnState.Interpreting => "CancelPendingResponseQuietly",
                _ => "PauseActiveTurn"
            };
            return Decision(UtteranceRouteKind.PauseAndClarify, 0.9, "Pause/stop phrase.", action);
        }

        if (TryExtractReplacement(normalized, utterance.Text, out var replacement))
        {
            return Decision(UtteranceRouteKind.ReplaceActiveTurn, 0.9, "Correction/replacement phrase.", "CancelPendingCommandAndStartReplacement", replacement);
        }

        if (StartsWithAny(normalized, "after that ", "then ", "when you are done ", "when you're done "))
        {
            return Decision(UtteranceRouteKind.QueueAfterActiveTurn, 0.8, "Queue-after cue.", "NotImplementedQueue");
        }

        if (StartsWithAny(normalized, "also ", "and ") || normalized.Contains(" too", StringComparison.Ordinal))
        {
            return Decision(UtteranceRouteKind.AddToActiveTurn, 0.7, "Additive cue.", "LogOnly");
        }

        if (normalized.Contains("what are you doing", StringComparison.Ordinal)
            || normalized.Contains("what are you working on", StringComparison.Ordinal)
            || normalized.Contains("did you hear me", StringComparison.Ordinal)
            || normalized.Contains("are you still", StringComparison.Ordinal))
        {
            return Decision(UtteranceRouteKind.StatusQuestion, 0.8, "Status question cue.", "AnswerStatus");
        }

        if (normalized is "thanks" or "thank you" or "okay" or "ok")
        {
            return Decision(UtteranceRouteKind.BackgroundOrNoOp, 0.65, "Backchannel/no-op phrase.", "Ignore");
        }

        return Decision(UtteranceRouteKind.Unknown, 0.45, "No live routing rule matched.", "LogOnly");
    }

    private async Task<bool> TryHandleLiveUtteranceRouteAsync(
        BargeInSpeechContext context,
        UserUtterance utterance,
        UtteranceRouteDecision routeDecision,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(utterance.CorrelationId))
        {
            return false;
        }

        switch (routeDecision.Kind)
        {
            case UtteranceRouteKind.PauseAndClarify when utterance.StateWhenCaptured is LiveAssistantTurnState.Speaking:
                _logger.LogInformation(
                    "TtsStoppedByUser. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}.",
                    utterance.ActiveTurnId,
                    utterance.CorrelationId);
                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return false;

            case UtteranceRouteKind.PauseAndClarify
                when utterance.StateWhenCaptured is LiveAssistantTurnState.AwaitingToolCommit or LiveAssistantTurnState.PlanningTool:
                if (!string.IsNullOrWhiteSpace(utterance.CorrelationId))
                {
                    _liveTurnService.UpdateTurnState(utterance.CorrelationId, LiveAssistantTurnState.PausedByUser);
                    await _liveTurnService.CancelTurnAsync(
                        utterance.CorrelationId,
                        LiveAssistantTurnCancelReason.UserHardStop,
                        cancellationToken: cancellationToken);
                    _logger.LogInformation(
                        "ActiveTurnPaused. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}.",
                        utterance.ActiveTurnId,
                        utterance.CorrelationId,
                        utterance.StateWhenCaptured);
                }

                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return true;

            case UtteranceRouteKind.CancelActiveTurn:
                await CancelByUtteranceAsync(utterance, LiveAssistantTurnCancelReason.UserHardStop, null, cancellationToken);
                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return true;

            case UtteranceRouteKind.ReplaceActiveTurn:
                if (utterance.AssistantWasSpeaking)
                {
                    await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                    return false;
                }

                await CancelByUtteranceAsync(utterance, LiveAssistantTurnCancelReason.UserCorrection, routeDecision.ReplacementText, cancellationToken);
                if (!string.IsNullOrWhiteSpace(routeDecision.ReplacementText))
                {
                    await RaiseCorrectionRegenerationRequestedAsync(context, routeDecision.ReplacementText, cancellationToken);
                }

                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return true;

            case UtteranceRouteKind.QueueAfterActiveTurn:
            case UtteranceRouteKind.StatusQuestion:
            case UtteranceRouteKind.AddToActiveTurn:
                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return true;

            default:
                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return false;
        }
    }

    private async Task CancelByUtteranceAsync(
        UserUtterance utterance,
        LiveAssistantTurnCancelReason reason,
        string? correctionText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(utterance.CorrelationId))
        {
            return;
        }

        await _playbackService.ClearQueueAsync(cancellationToken);
        await _liveTurnService.CancelTurnAsync(utterance.CorrelationId, reason, correctionText, cancellationToken);
        _logger.LogInformation(
            reason is LiveAssistantTurnCancelReason.UserCorrection ? "ActiveTurnSuperseded. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. CorrectionText: {CorrectionText}." : "ActiveTurnCancelled. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. CorrectionText: {CorrectionText}.",
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            correctionText);
    }

    private static UtteranceRouteDecision Decision(
        UtteranceRouteKind kind,
        double confidence,
        string reason,
        string action,
        string? replacementText = null)
    {
        return new UtteranceRouteDecision
        {
            Kind = kind,
            Confidence = confidence,
            Reason = reason,
            Action = action,
            ReplacementText = replacementText
        };
    }

    private static bool IsExplicitCancel(string normalized)
    {
        return normalized is "cancel that" or "never mind" or "nevermind" or "forget it" or "dont do that" or "don't do that" or "abort" or "stop doing that";
    }

    private static bool IsPausePhrase(string normalized)
    {
        return normalized is "stop" or "wait" or "hold on" or "pause" or "hang on" or "one second";
    }

    private static bool TryExtractReplacement(string normalized, string originalText, out string replacement)
    {
        replacement = string.Empty;
        var cues = new[]
        {
            "sorry i meant ",
            "i meant ",
            "i mean ",
            "actually ",
            "no open ",
            "not facebook ",
            "open google instead",
            "google instead"
        };

        foreach (var cue in cues)
        {
            var index = normalized.IndexOf(cue, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var candidate = normalized[(index + cue.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(candidate)
                && (cue is "open google instead" or "google instead"))
            {
                candidate = "google";
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            replacement = candidate.StartsWith("open ", StringComparison.OrdinalIgnoreCase)
                ? candidate
                : $"open {candidate}";
            return true;
        }

        if (normalized.Contains("not facebook google", StringComparison.Ordinal))
        {
            replacement = "open google";
            return true;
        }

        return false;
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool IsLikelyPlaybackLeakage(
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult? vad,
        BargeInOptions options,
        out string reason)
    {
        reason = string.Empty;
        if (!options.EnablePlaybackLeakageDuckingGuard)
        {
            return false;
        }

        var referenceEnergy = CalculateRms(playbackReference.Span);
        if (referenceEnergy < Math.Max(0.0, options.PlaybackLeakageReferenceEnergyThreshold))
        {
            return false;
        }

        var nearEndEnergy = CalculateRms(echoReducedFrame.Samples.Span);
        var confidence = vad is not null && vad.IsSpeech
            ? vad.Confidence
            : CalculateSpeechConfidence(nearEndEnergy, options);
        var requiredEnergy = Math.Max(
            options.VadEnergyThreshold * Math.Max(1.0, options.PlaybackLeakageMinEchoReducedEnergyMultiplier),
            referenceEnergy * Math.Max(0.0, options.PlaybackLeakageMinNearEndToReferenceRatio));
        var requiredConfidence = Math.Clamp(options.PlaybackLeakageMinVadConfidence, 0.0, 1.0);

        if (nearEndEnergy >= requiredEnergy && confidence >= requiredConfidence)
        {
            return false;
        }

        reason = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "Likely playback leakage rejected for ducking/barge-in. NearEndEnergy: {0:N4}. ReferenceEnergy: {1:N4}. VadConfidence: {2:N2}. RequiredEnergy: {3:N4}. RequiredConfidence: {4:N2}.",
            nearEndEnergy,
            referenceEnergy,
            confidence,
            requiredEnergy,
            requiredConfidence);
        return true;
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

    private static bool IsSpeechFrame(BargeInAudioFrame frame, BargeInOptions options)
    {
        var threshold = Math.Max(0.0001, options.VadEnergyThreshold);
        return CalculateRms(frame.Samples.Span) >= threshold;
    }

    private static double CalculateRms(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return 0.0;
        }

        double sumSquares = 0.0;
        foreach (var sample in samples)
        {
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / samples.Length);
    }

    private static double CalculateSpeechConfidence(double energy, BargeInOptions options)
    {
        var threshold = Math.Max(0.0001, options.VadEnergyThreshold);
        return Math.Clamp((energy - threshold) / threshold, 0.0, 1.0);
    }

    private CaptureFrameObservation CreateCaptureFrameObservation(
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        VadFrameResult vad,
        SelfSpeechGateResult? gateResult,
        BargeInOptions options)
    {
        var rawEnergy = CalculateRms(rawFrame.Samples.Span);
        var aecEnergy = CalculateRms(echoReducedFrame.Samples.Span);
        var comfortWouldAllow = IsFastNearEndSpeechCandidate(vad, options.FastNearEndDucking);
        var captureIsSpeech =
            (options.CaptureContinuationUseVad && vad.IsSpeech)
            || comfortWouldAllow
            || rawEnergy >= Math.Max(0.0, options.CaptureContinuationRawEnergyThreshold)
            || aecEnergy >= Math.Max(0.0, options.CaptureContinuationAecEnergyThreshold);

        return new CaptureFrameObservation
        {
            Timestamp = echoReducedFrame.Timestamp,
            RawEnergy = rawEnergy,
            AecEnergy = aecEnergy,
            VadConfidence = vad.Confidence,
            VadSaysSpeech = vad.IsSpeech,
            ComfortDuckingActive = _speakerDuckingService.IsDucked,
            ComfortDuckingWouldAllow = comfortWouldAllow,
            SelfSpeechDecision = gateResult?.Decision.ToString() ?? "Unavailable",
            SelfSpeechReason = gateResult?.Reason ?? "Self-speech gate was not evaluated for this capture frame.",
            CaptureIsSpeechFrame = captureIsSpeech,
            AppendedToCapture = true,
            AppendedToContinuousRecorder = rawFrame.SequenceNumber > 0,
            ProcessedByAnalyzer = true,
            SequenceNumber = rawFrame.SequenceNumber,
            DurationMs = rawFrame.DurationMs > 0
                ? rawFrame.DurationMs
                : (rawFrame.SampleRate <= 0 ? 0 : Math.Max(1, (int)Math.Round(rawFrame.Samples.Length * 1000.0 / rawFrame.SampleRate))),
            QueueDepth = rawFrame.AnalysisQueueDepth,
            AnalysisFramesDropped = rawFrame.AnalysisFramesDropped,
            ProcessedAtUtc = DateTimeOffset.UtcNow,
            RawSpeechEnergyThreshold = Math.Max(0.0, options.CaptureContinuationRawEnergyThreshold),
            AecSpeechEnergyThreshold = Math.Max(0.0, options.CaptureContinuationAecEnergyThreshold)
        };
    }

    private sealed class BargeInCaptureSession
    {
        private readonly object _sessionSync = new();
        private readonly DateTimeOffset _triggerTimestamp;
        private readonly DateTimeOffset _maxCaptureUntil;
        private readonly TaskCompletionSource<DateTimeOffset> _endpoint =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<InterruptionCaptureFrameDiagnostic> _frameDiagnostics = new();
        private readonly int _endSilenceMs;
        private DateTimeOffset _lastSpeechAt;
        private DateTimeOffset _lastFrameAt;
        private DateTimeOffset? _firstSpeechAt;
        private string _endReason = "pending";
        private int _rawSpeechFrames;
        private int _aecSpeechFrames;
        private int _vadSpeechFrames;
        private int _captureSpeechFrames;
        private int _falseSilenceFramesWhileVadSpeech;
        private double _totalProcessingLagMs;
        private double _maxProcessingLagMs;
        private long _analysisFramesDropped;

        private BargeInCaptureSession(DateTimeOffset triggerTimestamp, int maxCaptureMs, int endSilenceMs)
        {
            _triggerTimestamp = triggerTimestamp;
            _lastSpeechAt = triggerTimestamp;
            _lastFrameAt = triggerTimestamp;
            _endSilenceMs = Math.Max(0, endSilenceMs);
            _maxCaptureUntil = triggerTimestamp + TimeSpan.FromMilliseconds(maxCaptureMs);
            if (maxCaptureMs <= 0)
            {
                _endReason = "max_duration";
                _endpoint.TrySetResult(triggerTimestamp);
            }
        }

        public static BargeInCaptureSession CreateNormal(DateTimeOffset triggerTimestamp, BargeInOptions options)
        {
            return new BargeInCaptureSession(
                triggerTimestamp,
                BargeInCaptureTiming.GetMaxCaptureMs(options),
                options.VadEndSilenceMs);
        }

        public static BargeInCaptureSession CreateFastHardStop(DateTimeOffset triggerTimestamp, BargeInOptions options)
        {
            return new BargeInCaptureSession(
                triggerTimestamp,
                Math.Max(1, options.FastHardStopCaptureWindowMs),
                options.FastHardStopPostSpeechPaddingMs);
        }

        public string EndReason
        {
            get
            {
                lock (_sessionSync)
                {
                    return _endReason;
                }
            }
        }

        public int? FirstSpeechFrameRelativeMs
        {
            get
            {
                lock (_sessionSync)
                {
                    return _firstSpeechAt is null
                        ? null
                        : ToRelativeMs(_firstSpeechAt.Value);
                }
            }
        }

        public int? LastSpeechFrameRelativeMs
        {
            get
            {
                lock (_sessionSync)
                {
                    return ToRelativeMs(_lastSpeechAt);
                }
            }
        }

        public int CapturedSpeechMs
        {
            get
            {
                lock (_sessionSync)
                {
                    return _firstSpeechAt is null
                        ? 0
                        : Math.Max(0, (int)Math.Round((_lastSpeechAt - _firstSpeechAt.Value).TotalMilliseconds));
                }
            }
        }

        public int RawSpeechFrames
        {
            get
            {
                lock (_sessionSync)
                {
                    return _rawSpeechFrames;
                }
            }
        }

        public int AecSpeechFrames
        {
            get
            {
                lock (_sessionSync)
                {
                    return _aecSpeechFrames;
                }
            }
        }

        public int VadSpeechFrames
        {
            get
            {
                lock (_sessionSync)
                {
                    return _vadSpeechFrames;
                }
            }
        }

        public int CaptureSpeechFrames
        {
            get
            {
                lock (_sessionSync)
                {
                    return _captureSpeechFrames;
                }
            }
        }

        public int FalseSilenceFramesWhileVadSpeech
        {
            get
            {
                lock (_sessionSync)
                {
                    return _falseSilenceFramesWhileVadSpeech;
                }
            }
        }

        public long AnalysisFramesDropped
        {
            get
            {
                lock (_sessionSync)
                {
                    return _analysisFramesDropped;
                }
            }
        }

        public double MaxProcessingLagMs
        {
            get
            {
                lock (_sessionSync)
                {
                    return _maxProcessingLagMs;
                }
            }
        }

        public double AverageProcessingLagMs
        {
            get
            {
                lock (_sessionSync)
                {
                    return _frameDiagnostics.Count == 0
                        ? 0.0
                        : _totalProcessingLagMs / _frameDiagnostics.Count;
                }
            }
        }

        public void Observe(CaptureFrameObservation observation)
        {
            if (_endpoint.Task.IsCompleted)
            {
                return;
            }

            DateTimeOffset? endpoint = null;
            lock (_sessionSync)
            {
                _lastFrameAt = observation.Timestamp;
                if (observation.RawEnergy >= observation.RawSpeechEnergyThreshold)
                {
                    _rawSpeechFrames++;
                }

                if (observation.AecEnergy >= observation.AecSpeechEnergyThreshold)
                {
                    _aecSpeechFrames++;
                }

                if (observation.VadSaysSpeech)
                {
                    _vadSpeechFrames++;
                }

                if (observation.CaptureIsSpeechFrame)
                {
                    _firstSpeechAt ??= observation.Timestamp;
                    _lastSpeechAt = observation.Timestamp;
                    _captureSpeechFrames++;
                }
                else if (observation.VadSaysSpeech)
                {
                    _falseSilenceFramesWhileVadSpeech++;
                }

                var silenceMs = Math.Max(0, (int)Math.Round((observation.Timestamp - _lastSpeechAt).TotalMilliseconds));
                var processingLagMs = Math.Max(0, (int)Math.Round((observation.ProcessedAtUtc - observation.Timestamp).TotalMilliseconds));
                _totalProcessingLagMs += processingLagMs;
                _maxProcessingLagMs = Math.Max(_maxProcessingLagMs, processingLagMs);
                _analysisFramesDropped = Math.Max(_analysisFramesDropped, observation.AnalysisFramesDropped);
                _frameDiagnostics.Add(new InterruptionCaptureFrameDiagnostic
                {
                    FrameIndex = _frameDiagnostics.Count,
                    SequenceNumber = observation.SequenceNumber,
                    CapturedAtUtc = observation.Timestamp,
                    ProcessedAtUtc = observation.ProcessedAtUtc,
                    TimestampUtc = observation.Timestamp,
                    RelativeMs = ToRelativeMs(observation.Timestamp),
                    CaptureRelativeMs = ToRelativeMs(observation.Timestamp),
                    ProcessingLagMs = processingLagMs,
                    DurationMs = observation.DurationMs,
                    QueueDepth = observation.QueueDepth,
                    RawEnergy = observation.RawEnergy,
                    AecEnergy = observation.AecEnergy,
                    VadConfidence = observation.VadConfidence,
                    VadSaysSpeech = observation.VadSaysSpeech,
                    ComfortDuckingActive = observation.ComfortDuckingActive,
                    ComfortDuckingWouldAllow = observation.ComfortDuckingWouldAllow,
                    SelfSpeechDecision = observation.SelfSpeechDecision,
                    SelfSpeechReason = observation.SelfSpeechReason,
                    CaptureIsSpeechFrame = observation.CaptureIsSpeechFrame,
                    LastSpeechAgeMs = silenceMs,
                    AppendedToCapture = observation.AppendedToCapture,
                    AppendedToContinuousRecorder = observation.AppendedToContinuousRecorder,
                    ProcessedByAnalyzer = observation.ProcessedByAnalyzer,
                    EndpointSilenceMs = silenceMs
                });

                if (observation.Timestamp >= _maxCaptureUntil)
                {
                    endpoint = _maxCaptureUntil;
                    _endReason = "max_duration";
                }
                else if (observation.Timestamp - _lastSpeechAt >= TimeSpan.FromMilliseconds(_endSilenceMs))
                {
                    endpoint = _lastFrameAt;
                    _endReason = "sustained_silence";
                }
            }

            if (endpoint is not null)
            {
                _endpoint.TrySetResult(endpoint.Value);
            }
        }

        public async Task<DateTimeOffset> WaitForEndpointAsync(CancellationToken cancellationToken)
        {
            var timeout = _maxCaptureUntil - DateTimeOffset.UtcNow;
            if (timeout < TimeSpan.Zero)
            {
                timeout = TimeSpan.Zero;
            }

            var delayTask = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(_endpoint.Task, delayTask);
            if (completed == _endpoint.Task)
            {
                return await _endpoint.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
            lock (_sessionSync)
            {
                _endReason = "max_duration";
            }

            return _maxCaptureUntil;
        }

        public IReadOnlyList<InterruptionCaptureFrameDiagnostic> GetFrameDiagnostics()
        {
            lock (_sessionSync)
            {
                return _frameDiagnostics.ToArray();
            }
        }

        private int ToRelativeMs(DateTimeOffset timestamp)
        {
            return (int)Math.Round((timestamp - _triggerTimestamp).TotalMilliseconds);
        }
    }

    private sealed class BurstCaptureCandidateState
    {
        private readonly List<BurstCaptureObservation> _observations = [];

        public bool HasFrames => _observations.Count > 0;

        public BurstCaptureSnapshot Add(
            BurstCaptureObservation observation,
            BurstCapturePromotionOptions options)
        {
            _observations.Add(observation);
            var windowStart = observation.TimestampUtc - TimeSpan.FromMilliseconds(Math.Max(1, options.MaxWindowMs));
            _observations.RemoveAll(candidate => candidate.TimestampUtc < windowStart);
            return Snapshot();
        }

        public BurstCaptureSnapshot Snapshot()
        {
            if (_observations.Count == 0)
            {
                return BurstCaptureSnapshot.Empty;
            }

            var first = _observations[0];
            var last = _observations[^1];
            var burstMs = Math.Max(
                last.DurationMs,
                (int)Math.Round((last.TimestampUtc - first.TimestampUtc).TotalMilliseconds) + last.DurationMs);
            var rawMin = _observations.Min(observation => observation.RawEnergy);
            var rawMax = _observations.Max(observation => observation.RawEnergy);
            var rawAverage = _observations.Average(observation => observation.RawEnergy);
            var aecMin = _observations.Min(observation => observation.AecEnergy);
            var aecMax = _observations.Max(observation => observation.AecEnergy);
            var aecAverage = _observations.Average(observation => observation.AecEnergy);
            var strongSelfEchoFrames = _observations.Count(observation => observation.StrongSelfEcho);
            var totalFrames = _observations.Count;
            var vadSpeechFrames = _observations.Count(observation => observation.VadSaysSpeech);
            return new BurstCaptureSnapshot
            {
                CandidateBurstMs = burstMs,
                WindowMs = burstMs,
                TotalFrames = totalFrames,
                VadSpeechFrames = vadSpeechFrames,
                VadSpeechFrameRatio = totalFrames == 0 ? 0.0 : vadSpeechFrames / (double)totalFrames,
                ComfortDuckingFrames = _observations.Count(observation => observation.ComfortDuckingActive),
                AllowFrames = _observations.Count(observation => observation.GateDecision is SelfSpeechDecision.Allow),
                UncertainFrames = _observations.Count(observation => observation.GateDecision is SelfSpeechDecision.Uncertain),
                SuppressAsSelfEchoFrames = _observations.Count(observation => observation.GateDecision is SelfSpeechDecision.SuppressAsSelfEcho),
                StrongSelfEchoFrames = strongSelfEchoFrames,
                StrongSelfEchoRatio = totalFrames == 0 ? 0.0 : strongSelfEchoFrames / (double)totalFrames,
                RawEnergyMin = rawMin,
                RawEnergyMax = rawMax,
                RawEnergyAverage = rawAverage,
                AecEnergyMin = aecMin,
                AecEnergyMax = aecMax,
                AecEnergyAverage = aecAverage,
                CorrelationScoreMax = _observations
                    .Where(observation => observation.CorrelationScore is not null)
                    .Select(observation => observation.CorrelationScore!.Value)
                    .DefaultIfEmpty(0.0)
                    .Max()
            };
        }

        public void Reset()
        {
            _observations.Clear();
        }
    }

    private sealed record BurstCaptureObservation
    {
        public required DateTimeOffset TimestampUtc { get; init; }

        public required int DurationMs { get; init; }

        public required bool VadSaysSpeech { get; init; }

        public required bool ComfortDuckingActive { get; init; }

        public required SelfSpeechDecision? GateDecision { get; init; }

        public required bool StrongSelfEcho { get; init; }

        public required double RawEnergy { get; init; }

        public required double AecEnergy { get; init; }

        public required double? CorrelationScore { get; init; }
    }

    private sealed record BurstCaptureSnapshot
    {
        public static BurstCaptureSnapshot Empty { get; } = new()
        {
            CandidateBurstMs = 0,
            WindowMs = 0,
            TotalFrames = 0,
            VadSpeechFrames = 0,
            VadSpeechFrameRatio = 0.0,
            ComfortDuckingFrames = 0,
            AllowFrames = 0,
            UncertainFrames = 0,
            SuppressAsSelfEchoFrames = 0,
            StrongSelfEchoFrames = 0,
            StrongSelfEchoRatio = 0.0,
            RawEnergyMin = 0.0,
            RawEnergyMax = 0.0,
            RawEnergyAverage = 0.0,
            AecEnergyMin = 0.0,
            AecEnergyMax = 0.0,
            AecEnergyAverage = 0.0,
            CorrelationScoreMax = 0.0
        };

        public required int CandidateBurstMs { get; init; }

        public required int WindowMs { get; init; }

        public required int TotalFrames { get; init; }

        public required int VadSpeechFrames { get; init; }

        public required double VadSpeechFrameRatio { get; init; }

        public required int ComfortDuckingFrames { get; init; }

        public required int AllowFrames { get; init; }

        public required int UncertainFrames { get; init; }

        public required int SuppressAsSelfEchoFrames { get; init; }

        public required int StrongSelfEchoFrames { get; init; }

        public required double StrongSelfEchoRatio { get; init; }

        public required double RawEnergyMin { get; init; }

        public required double RawEnergyMax { get; init; }

        public required double RawEnergyAverage { get; init; }

        public required double AecEnergyMin { get; init; }

        public required double AecEnergyMax { get; init; }

        public required double AecEnergyAverage { get; init; }

        public required double CorrelationScoreMax { get; init; }
    }

    private sealed record BurstCapturePromotionSummary
    {
        public required string CaptureStartReason { get; init; }

        public required int CandidateBurstMsAtPromotion { get; init; }

        public required int BurstTotalFrames { get; init; }

        public required int BurstVadSpeechFrames { get; init; }

        public required int BurstComfortDuckingFrames { get; init; }

        public required int BurstAllowFrames { get; init; }

        public required int BurstUncertainFrames { get; init; }

        public required int BurstSuppressAsSelfEchoFrames { get; init; }

        public required int BurstStrongSelfEchoFrames { get; init; }

        public required double BurstStrongSelfEchoRatio { get; init; }

        public required string BurstPromotionReason { get; init; }
    }

    private sealed record CaptureFrameObservation
    {
        public required DateTimeOffset Timestamp { get; init; }

        public required double RawEnergy { get; init; }

        public required double AecEnergy { get; init; }

        public required double VadConfidence { get; init; }

        public required bool VadSaysSpeech { get; init; }

        public required bool ComfortDuckingActive { get; init; }

        public required bool ComfortDuckingWouldAllow { get; init; }

        public required string SelfSpeechDecision { get; init; }

        public required string SelfSpeechReason { get; init; }

        public required bool CaptureIsSpeechFrame { get; init; }

        public required bool AppendedToCapture { get; init; }

        public required bool AppendedToContinuousRecorder { get; init; }

        public required bool ProcessedByAnalyzer { get; init; }

        public required long SequenceNumber { get; init; }

        public required int DurationMs { get; init; }

        public required int QueueDepth { get; init; }

        public required long AnalysisFramesDropped { get; init; }

        public required DateTimeOffset ProcessedAtUtc { get; init; }

        public required double RawSpeechEnergyThreshold { get; init; }

        public required double AecSpeechEnergyThreshold { get; init; }
    }

    public async ValueTask DisposeAsync()
    {
        _playbackReferenceTap.SpeechStarted -= OnSpeechStarted;
        _playbackReferenceTap.SpeechStopped -= OnSpeechStopped;
        await _aec.DisposeAsync();
    }
}
