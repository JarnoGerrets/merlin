using System.Diagnostics;
using System.Text.RegularExpressions;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Services.LiveUtterance;
using Merlin.Backend.Services.SpeechPresence;
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
    private readonly ILiveInterruptionIntegrationService? _liveInterruptionIntegrationService;
    private readonly IActiveSpokenTurnResolver? _activeSpokenTurnResolver;
    private readonly ILiveUtteranceGate? _liveUtteranceGate;
    private readonly ILiveAssistantTurnService _liveTurnService;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly AssistantUiStateBroadcaster? _assistantUiStateBroadcaster;
    private readonly IInterruptionCaptureDiagnosticsWriter _captureDiagnosticsWriter;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly ILogger<BargeInCoordinator> _logger;
    private readonly IAssistantPlaybackMonitor _assistantPlaybackMonitor;
    private readonly ISpeakerDuckingService _speakerDuckingService;
    private readonly ISelfSpeechSuppressionGate _selfSpeechGate;
    private readonly IContinuousMicAudioBuffer _continuousMicAudioBuffer;
    private readonly IOptionsMonitor<VoiceInputOptions> _voiceInputOptions;
    private readonly IBargeInSttService _sttService;
    private readonly IBargeInTriggerBuffer _triggerBuffer;
    private readonly IBargeInVadService _vadService;
    private readonly IBargeInDebugSnapshotService? _debugSnapshots;
    private readonly ISpeechPresenceDetector? _speechPresenceDetector;
    private readonly ISpeechPresenceDecisionLogSink? _speechPresenceDecisionLogSink;
    private readonly IFloorYieldController? _floorYieldController;
    private readonly IActiveSurfaceService? _activeSurfaceService;
    private readonly IPendingInterruptionClarificationService? _pendingInterruptionClarifications;
    private readonly MerlinAwakeStateService _merlinAwakeState;
    private readonly object _syncRoot = new();
    private long _analysisFrameSequence;
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
    private int _sustainedUserSpeechScoreMs;
    private bool _playbackYieldedForRollingUserSpeechEvidence;
    private long _interruptionHandlingWatchdogGeneration;
    private DateTimeOffset? _interruptionHandlingStateStartedAtUtc;
    private string? _interruptionHandlingTurnId;
    private string? _interruptionHandlingCorrelationId;
    private string? _interruptionHandlingReason;
    private readonly RollingUserSpeechEvidenceTracker _rollingUserSpeechEvidence = new();
    private readonly BurstCaptureCandidateState _burstCaptureCandidate = new();
    private readonly Dictionary<string, DateTimeOffset> _lastRepeatedDiagnosticAt = new(StringComparer.Ordinal);

    public event Func<CorrectionRegenerationRequested, CancellationToken, Task>? CorrectionRegenerationRequested;

    public event Func<BackendVoiceRequestCaptured, CancellationToken, Task>? BackendVoiceRequestCaptured;

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
        IOptionsMonitor<BargeInOptions> options,
        IOptionsMonitor<VoiceInputOptions> voiceInputOptions,
        MerlinAwakeStateService merlinAwakeState,
        ILiveUtteranceGate? liveUtteranceGate = null,
        IBargeInDebugSnapshotService? debugSnapshots = null,
        ISpeechPresenceDetector? speechPresenceDetector = null,
        ISpeechPresenceDecisionLogSink? speechPresenceDecisionLogSink = null,
        IFloorYieldController? floorYieldController = null,
        ILiveInterruptionIntegrationService? liveInterruptionIntegrationService = null,
        IActiveSpokenTurnResolver? activeSpokenTurnResolver = null,
        AssistantUiStateBroadcaster? assistantUiStateBroadcaster = null,
        IActiveSurfaceService? activeSurfaceService = null,
        IPendingInterruptionClarificationService? pendingInterruptionClarifications = null)
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
        _assistantUiStateBroadcaster = assistantUiStateBroadcaster;
        _captureDiagnosticsWriter = captureDiagnosticsWriter;
        _diagnostics = diagnostics;
        _logger = logger;
        _options = options;
        _voiceInputOptions = voiceInputOptions;
        _merlinAwakeState = merlinAwakeState;
        _liveInterruptionIntegrationService = liveInterruptionIntegrationService;
        _liveUtteranceGate = liveUtteranceGate;
        _debugSnapshots = debugSnapshots;
        _speechPresenceDetector = speechPresenceDetector;
        _speechPresenceDecisionLogSink = speechPresenceDecisionLogSink;
        _floorYieldController = floorYieldController;
        _activeSurfaceService = activeSurfaceService;
        _activeSpokenTurnResolver = activeSpokenTurnResolver;
        _pendingInterruptionClarifications = pendingInterruptionClarifications;

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

        var frameId = Interlocked.Increment(ref _analysisFrameSequence);
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
            var activeSpeechPresenceDecision = EvaluateOfficialSpeechPresence(
                frameId,
                frame,
                echoReducedFrame,
                reference,
                activeVad,
                activeGateResult,
                correlation);
            await HandleFloorYieldAsync(activeSpeechPresenceDecision, cancellationToken);
            var activeSpeechPresence = activeSpeechPresenceDecision?.Result;
            PublishDebugSnapshot(
                context,
                frame,
                echoReducedFrame,
                reference,
                activeVad,
                activeGateResult,
                correlation,
                "active_capture_endpointing",
                BargeInState.CapturingInterruption.ToString(),
                speechPresence: activeSpeechPresence);
            ObserveCaptureFrame(context, activeCaptureSession, CreateCaptureFrameObservation(
                frame,
                echoReducedFrame,
                reference,
                activeVad,
                activeGateResult,
                activeCaptureSession.Mode,
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
        var speechPresenceDecision = EvaluateOfficialSpeechPresence(
            frameId,
            frame,
            echoReducedFrame,
            reference,
            vad,
            comfortGateResult,
            correlation);
        await HandleFloorYieldAsync(speechPresenceDecision, cancellationToken);
        var speechPresence = speechPresenceDecision?.Result;
        PublishDebugSnapshot(
            context,
            frame,
            echoReducedFrame,
            reference,
            vad,
            comfortGateResult,
            correlation,
            "mic_frame",
            IsInterruptionCaptureActive()
                ? BargeInState.CapturingInterruption.ToString()
                : BargeInState.Speaking.ToString(),
            speechPresence: speechPresence);
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
            await UpdateRollingUserSpeechEvidenceAsync(
                context,
                echoReducedFrame,
                comfortGateResult,
                options,
                "burst_promotion",
                cancellationToken);

            if (!EvaluateSustainedUserSpeechScoreGate(
                    context,
                    echoReducedFrame,
                    comfortGateResult,
                    options,
                    "burst_promotion",
                    burstPromotionSummary))
            {
                _burstCaptureCandidate.Reset();
                return;
            }

            _suppressedFastHardStopAttemptSaved = false;
            _suppressedVadTriggeredAttemptSaved = false;
            ResetFastHardStopCandidate();
            ResetFastNearEndDuckingCandidate();
            await StartTriggeredSpeechAsync(
                context,
                frame,
                echoReducedFrame,
                reference,
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
            SelfSpeechGateResult? rollingGateResult = comfortGateResult;
            var rollingSource = comfortGateResult is null ? "mic_frame" : "comfort_ducking";
            if (!vad.IsSpeech)
            {
                _suppressedFastHardStopAttemptSaved = false;
                _suppressedVadTriggeredAttemptSaved = false;
                ResetSustainedUserSpeechScoreGate(context, options, "vad_reported_non_speech", null);
            }

            if (vad.IsSpeech)
            {
                var speechGateResult = EvaluateSelfSpeechGate(
                    echoReducedFrame,
                    vad,
                    aecResult,
                    correlation,
                    "fast_hard_stop_candidate");
                _ = ObserveSpeechPresenceBranch(
                    frameId,
                    frame,
                    echoReducedFrame,
                    reference,
                    vad,
                    speechGateResult,
                    correlation,
                    "fast_hard_stop_candidate");
                if (IsBetterRollingUserSpeechEvidence(speechGateResult, rollingGateResult))
                {
                    rollingGateResult = speechGateResult;
                    rollingSource = "fast_hard_stop_candidate";
                }

                PublishDebugSnapshot(
                    context,
                    frame,
                    echoReducedFrame,
                    reference,
                    vad,
                    speechGateResult,
                    correlation,
                    "fast_hard_stop_candidate",
                    BargeInState.Speaking.ToString(),
                    speechPresence: speechPresence);
                if (speechGateResult.Decision is not SelfSpeechDecision.Allow)
                {
                    LogCandidateDiagnostic(options, context, $"Fast hard-stop candidate suppressed by self-speech gate. Decision: {speechGateResult.Decision}. Reason: {speechGateResult.Reason}");
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
                    await UpdateRollingUserSpeechEvidenceAsync(
                        context,
                        echoReducedFrame,
                        rollingGateResult,
                        options,
                        rollingSource,
                        cancellationToken);
                    return;
                }
            }

            await UpdateRollingUserSpeechEvidenceAsync(
                context,
                echoReducedFrame,
                rollingGateResult,
                options,
                rollingSource,
                cancellationToken);

            if (IsAssistantAudioActuallyPlaying(_playbackService.GetActivePlaybackSnapshot())
                && TryConsumeFastHardStopCandidate(echoReducedFrame, reference, vad, options, out var fastTriggerFrame))
            {
                await StartTriggeredSpeechAsync(
                    context,
                    echoReducedFrame,
                    fastTriggerFrame,
                    reference,
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
        _ = ObserveSpeechPresenceBranch(
            frameId,
            frame,
            echoReducedFrame,
            reference,
            vad,
            gateResult,
            correlation,
            "vad_triggered_capture");
        var rollingVadGateResult = IsBetterRollingUserSpeechEvidence(gateResult, comfortGateResult)
            ? gateResult
            : comfortGateResult;
        await UpdateRollingUserSpeechEvidenceAsync(
            context,
            echoReducedFrame,
            rollingVadGateResult,
            options,
            ReferenceEquals(rollingVadGateResult, gateResult) ? "vad_triggered_capture" : "comfort_ducking",
            cancellationToken);
        PublishDebugSnapshot(
            context,
            frame,
            echoReducedFrame,
            reference,
            vad,
            gateResult,
            correlation,
            "vad_triggered_capture",
            BargeInState.SoftPausedForUserSpeech.ToString(),
            speechPresence: speechPresence);
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
            ResetSustainedUserSpeechScoreGate(context, options, "self_speech_gate_blocked_vad_triggered_capture", gateResult);
            return;
        }

        if (!EvaluateSustainedUserSpeechScoreGate(
                context,
                echoReducedFrame,
                gateResult,
                options,
                "vad_triggered_capture"))
        {
            ResetFastHardStopCandidate();
            return;
        }

        _suppressedFastHardStopAttemptSaved = false;
        _suppressedVadTriggeredAttemptSaved = false;
        ResetFastHardStopCandidate();
        ResetFastNearEndDuckingCandidate();
        await StartTriggeredSpeechAsync(
            context,
            frame,
            echoReducedFrame,
            reference,
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

        var voiceInput = _voiceInputOptions.CurrentValue;
        _logger.LogInformation(
            "VoiceInputOwnerMode. Owner: {Owner}. BackendVoiceInputEnabled: {BackendVoiceInputEnabled}. FrontendVoiceInputEnabled: {FrontendVoiceInputEnabled}. BackendIdleVoiceInteractionSource: {BackendIdleVoiceInteractionSource}.",
            voiceInput.Owner,
            voiceInput.BackendVoiceInputEnabled,
            voiceInput.FrontendVoiceInputEnabled,
            voiceInput.BackendIdleVoiceInteractionSource);
        if (voiceInput.BackendVoiceInputEnabled)
        {
            _logger.LogInformation("BackendVoiceInputEnabled. Owner: {Owner}.", voiceInput.Owner);
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
            _sustainedUserSpeechScoreMs = 0;
            ResetRollingUserSpeechEvidence(context, options, "live_monitor_started", null, "live_monitor_started");
            _selfSpeechGate.Reset();
            CancelPendingDuckingRestore();
        }

        _vadService.Reset();
        _triggerBuffer.Reset("live_monitor_started", context.AssistantTurnId);
        _diagnostics.MonitorStarted(context, _aecMode);
    }

    private async Task StartTriggeredSpeechAsync(
        BargeInSpeechContext context,
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        AecProcessResult aecResult,
        BargeInOptions options,
        CaptureKind captureKind,
        BurstCapturePromotionSummary? burstPromotionSummary,
        CancellationToken cancellationToken)
    {
        _diagnostics.VadPossibleSpeech(context, vad, aecResult.Mode);
        if (IsLiveMonitorContext(context) && !_liveTurnService.TryGetCurrentActiveTurn(out _))
        {
            _logger.LogInformation(
                "BackendIdleVoiceCaptureStarted. AssistantTurnId: {AssistantTurnId}. VadConfidence: {VadConfidence}.",
                context.AssistantTurnId,
                vad.Confidence);
        }

        var isSleepingIdleCapture = IsLiveMonitorContext(context)
            && !_liveTurnService.TryGetCurrentActiveTurn(out _)
            && !_merlinAwakeState.IsAwake;
        if (!isSleepingIdleCapture)
        {
            await EmitAssistantUiStateImmediateAsync(
                AssistantUiStateEvent.Create(
                    "listening",
                    IsLiveMonitorContext(context)
                        ? "backend_idle_voice_capture_started"
                        : "barge_in_capture_started",
                    context.CorrelationId,
                    context.AssistantTurnId,
                    audiblePlaybackActive: false,
                    interruptionState: AssistantUiStateEvent.InterruptionStateCapturing),
                nameof(BargeInCoordinator),
                cancellationToken);
        }
        _diagnostics.StateChanged(
            context,
            BargeInState.SoftPausedForUserSpeech,
            captureKind is CaptureKind.FastHardStop
                ? "Fast hard-stop candidate detected while assistant is speaking; capture starts without legacy playback duck/yield."
                : "VAD detected likely user speech; capture starts without legacy playback duck/yield.");

        BargeInCaptureSession captureSession;
        lock (_syncRoot)
        {
            if (_handlingTrigger)
            {
                return;
            }

            _handlingTrigger = true;
            _sustainedUserSpeechScoreMs = 0;
            ResetRollingUserSpeechEvidence(context, options, "triggered_capture_started", null, "triggered_capture_started");
            var acousticMode = captureKind is CaptureKind.FastHardStop
                ? AcousticCaptureMode.AssistantInterruption
                : SelectAcousticCaptureModeForFrame(playbackReference, options);
            captureSession = captureKind is CaptureKind.FastHardStop
                ? BargeInCaptureSession.CreateFastHardStop(echoReducedFrame.Timestamp, options)
                : BargeInCaptureSession.CreateNormal(echoReducedFrame.Timestamp, options, acousticMode);
            LogVoiceCaptureTimelineStarted(captureSession, context, acousticMode, playbackReference, captureKind);
            ObserveCaptureFrame(context, captureSession, CreateCaptureFrameObservation(
                rawFrame,
                echoReducedFrame,
                playbackReference,
                vad,
                null,
                acousticMode,
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

        if (duckingOptions.RequireAssistantPlayback && !IsAssistantAudioActuallyPlaying(_playbackService.GetActivePlaybackSnapshot()))
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
            LogLegacyDiagnostic(options, context, $"Comfort duck suppressed by confident self echo. DuckingOwner: {_duckingOwner ?? "(none)"}. RestoreGeneration: {_duckingRestoreGeneration}. InputReason: {comfortOptions.InputReason}. SelfSpeechDecision: {gateResult.Decision}. SelfSpeechReason: {gateResult.Reason}.{holdReason} ConsecutiveMs: {_fastNearEndSpeechMs}. VadConfidence: {vad.Confidence:N2}. Energy: {vad.Energy:N4}. NoiseFloor: {vad.NoiseFloor:N4}.");
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

        var duckReason = gateResult.Decision is not SelfSpeechDecision.Allow
            ? "comfort_ducking_uncertain_near_end"
            : "comfort_ducking_likely_user";
        var latencyMs = _fastNearEndFirstSpeechAt is null
            ? 0
            : Math.Max(0, (int)Math.Round((DateTimeOffset.UtcNow - _fastNearEndFirstSpeechAt.Value).TotalMilliseconds));
        LogLegacyDiagnostic(
            options,
            context,
            $"Legacy comfort duck disabled. LegacyOwner: {duckReason}. InputReason: {comfortOptions.InputReason}. SelfSpeechDecision: {gateResult.Decision}. SelfSpeechReason: {gateResult.Reason}. ConsecutiveMs: {_fastNearEndSpeechMs}. MinSpeechMs: {comfortOptions.MinSpeechMs}. VadConfidence: {vad.Confidence:N2}. Energy: {vad.Energy:N4}. NoiseFloor: {vad.NoiseFloor:N4}. WouldDuckLatencyMs: {latencyMs}. CaptureActive: {IsInterruptionCaptureActive()}.");

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

    private bool EvaluateSustainedUserSpeechScoreGate(
        BargeInSpeechContext context,
        BargeInAudioFrame frame,
        SelfSpeechGateResult? gateResult,
        BargeInOptions options,
        string candidateType,
        BurstCapturePromotionSummary? burstPromotionSummary = null)
    {
        if (!options.RequireSustainedUserSpeechScoreDuringPlayback)
        {
            return true;
        }

        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var assistantPlaybackContextActive = IsAssistantPlaybackContextActive(activePlaybackSnapshot);
        var assistantAudioActuallyPlaying = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot);
        var threshold = Math.Clamp(options.SustainedUserSpeechScoreThreshold, 0.0, 1.0);
        var requiredDurationMs = Math.Max(1, options.SustainedUserSpeechScoreDurationMs);
        var userSpeechScore = gateResult?.UserSpeechScore;
        var userSpeechScoreText = FormatUserSpeechScore(userSpeechScore);
        var gateApplies = assistantAudioActuallyPlaying;

        _logger.LogInformation(
            "SustainedUserSpeechScoreGateEvaluated. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. AudiblePlaybackActive: {AudiblePlaybackActive}. ActivePlaybackSnapshotIsActive: {ActivePlaybackSnapshotIsActive}. ActivePlaybackSnapshotIsHeld: {ActivePlaybackSnapshotIsHeld}. HoldId: {HoldId}. RequireSustainedUserSpeechScoreDuringPlaybackApplied: {RequireSustainedUserSpeechScoreDuringPlaybackApplied}. CandidateBurstMs: {CandidateBurstMs}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}.",
            candidateType,
            userSpeechScoreText,
            threshold,
            _sustainedUserSpeechScoreMs,
            requiredDurationMs,
            assistantAudioActuallyPlaying,
            assistantPlaybackContextActive,
            assistantAudioActuallyPlaying,
            assistantAudioActuallyPlaying,
            activePlaybackSnapshot?.IsActive,
            activePlaybackSnapshot?.IsHeld,
            activePlaybackSnapshot?.HoldId,
            gateApplies,
            burstPromotionSummary?.CandidateBurstMsAtPromotion,
            gateResult?.Decision.ToString() ?? "Unavailable",
            gateResult?.Reason ?? "Self-speech gate result unavailable.");

        if (!assistantAudioActuallyPlaying)
        {
            if (IsStrongHeldBurstCandidate(burstPromotionSummary, gateResult, threshold, options, activePlaybackSnapshot))
            {
                var seededDurationMs = Math.Max(
                    _sustainedUserSpeechScoreMs,
                    burstPromotionSummary!.CandidateBurstMsAtPromotion);
                _sustainedUserSpeechScoreMs = Math.Max(_sustainedUserSpeechScoreMs, Math.Min(seededDurationMs, requiredDurationMs));
                _logger.LogInformation(
                    "sustained_user_speech_gate_seeded_from_burst CandidateType: {CandidateType}. SustainedGateSeededFromBurst: {SustainedGateSeededFromBurst}. SeededDurationMs: {SeededDurationMs}. CandidateBurstMs: {CandidateBurstMs}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. ActivePlaybackSnapshotIsActive: {ActivePlaybackSnapshotIsActive}. ActivePlaybackSnapshotIsHeld: {ActivePlaybackSnapshotIsHeld}. HoldId: {HoldId}. SelfSpeechGateDecision: {SelfSpeechGateDecision}.",
                    candidateType,
                    true,
                    _sustainedUserSpeechScoreMs,
                    burstPromotionSummary.CandidateBurstMsAtPromotion,
                    userSpeechScoreText,
                    threshold,
                    _sustainedUserSpeechScoreMs,
                    requiredDurationMs,
                    assistantPlaybackContextActive,
                    assistantAudioActuallyPlaying,
                    activePlaybackSnapshot?.IsActive,
                    activePlaybackSnapshot?.IsHeld,
                    activePlaybackSnapshot?.HoldId,
                    gateResult?.Decision.ToString() ?? "Unavailable");
                _logger.LogInformation(
                    "sustained_user_speech_gate_bypassed_for_held_burst CandidateType: {CandidateType}. CandidateBurstMs: {CandidateBurstMs}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. RequiredDurationMs: {RequiredDurationMs}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. HoldId: {HoldId}.",
                    candidateType,
                    burstPromotionSummary.CandidateBurstMsAtPromotion,
                    userSpeechScoreText,
                    threshold,
                    requiredDurationMs,
                    assistantPlaybackContextActive,
                    assistantAudioActuallyPlaying,
                    activePlaybackSnapshot?.HoldId);
                return true;
            }

            if (burstPromotionSummary is not null && activePlaybackSnapshot is { IsHeld: true })
            {
                _logger.LogInformation(
                    "sustained_user_speech_gate_bypassed_for_held_burst CandidateType: {CandidateType}. CandidateBurstMs: {CandidateBurstMs}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. RequiredDurationMs: {RequiredDurationMs}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. HoldId: {HoldId}.",
                    candidateType,
                    burstPromotionSummary.CandidateBurstMsAtPromotion,
                    userSpeechScoreText,
                    threshold,
                    requiredDurationMs,
                    assistantPlaybackContextActive,
                    assistantAudioActuallyPlaying,
                    activePlaybackSnapshot.HoldId);
            }

            ResetSustainedUserSpeechScoreGate(context, options, "assistant_audio_not_audible", gateResult, candidateType);
            return true;
        }

        if (gateResult?.Decision is SelfSpeechDecision.SuppressAsSelfEcho)
        {
            ResetSustainedUserSpeechScoreGate(context, options, "self_speech_gate_suppressed_as_echo", gateResult, candidateType);
            _logger.LogInformation(
                "SustainedUserSpeechScoreGateBlockedCapture. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}.",
                candidateType,
                userSpeechScoreText,
                threshold,
                _sustainedUserSpeechScoreMs,
                requiredDurationMs,
                assistantAudioActuallyPlaying,
                gateResult.Decision,
                gateResult.Reason,
                "suppressed_as_self_echo");
            return false;
        }

        if (userSpeechScore is null)
        {
            _logger.LogInformation(
                "SustainedUserSpeechScoreGateBlockedCapture. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}.",
                candidateType,
                userSpeechScoreText,
                threshold,
                _sustainedUserSpeechScoreMs,
                requiredDurationMs,
                assistantAudioActuallyPlaying,
                "Unavailable",
                "Self-speech gate result unavailable.",
                "missing_user_speech_score");
            return false;
        }

        if (userSpeechScore.Value < threshold)
        {
            ResetSustainedUserSpeechScoreGate(context, options, "measured_score_below_threshold", gateResult, candidateType);
            _logger.LogInformation(
                "SustainedUserSpeechScoreGateBlockedCapture. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}.",
                candidateType,
                userSpeechScoreText,
                threshold,
                _sustainedUserSpeechScoreMs,
                requiredDurationMs,
                assistantAudioActuallyPlaying,
                gateResult?.Decision.ToString() ?? "Unavailable",
                gateResult?.Reason ?? "Self-speech gate result unavailable.",
                "measured_score_below_threshold");
            return false;
        }

        _sustainedUserSpeechScoreMs += GetFrameDurationMs(frame, options);
        if (ShouldEmitRepeatedDiagnostic(options, $"SustainedUserSpeechScoreGateAccumulated:{context.AssistantTurnId}:{candidateType}"))
        {
            _logger.LogInformation(
                "SustainedUserSpeechScoreGateAccumulated. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}.",
                candidateType,
                userSpeechScoreText,
                threshold,
                _sustainedUserSpeechScoreMs,
                requiredDurationMs,
                assistantAudioActuallyPlaying,
                gateResult?.Decision.ToString() ?? "Unavailable",
                gateResult?.Reason ?? "Self-speech gate result unavailable.");
        }

        if (_sustainedUserSpeechScoreMs < requiredDurationMs)
        {
            _logger.LogInformation(
                "SustainedUserSpeechScoreGateBlockedCapture. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}.",
                candidateType,
                userSpeechScoreText,
                threshold,
                _sustainedUserSpeechScoreMs,
                requiredDurationMs,
                assistantAudioActuallyPlaying,
                gateResult?.Decision.ToString() ?? "Unavailable",
                gateResult?.Reason ?? "Self-speech gate result unavailable.",
                "UserSpeechScore has not been sustained long enough.");
            return false;
        }

        _logger.LogInformation(
            "SustainedUserSpeechScoreGateAllowedCapture. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}.",
            candidateType,
            userSpeechScoreText,
            threshold,
            _sustainedUserSpeechScoreMs,
            requiredDurationMs,
            assistantAudioActuallyPlaying,
            gateResult?.Decision.ToString() ?? "Unavailable",
            gateResult?.Reason ?? "Self-speech gate result unavailable.",
            "UserSpeechScore sustained for required duration.");
        return true;
    }

    private bool IsAssistantAudioActuallyPlaying(ActiveSpeechPlaybackSnapshot? activePlaybackSnapshot)
    {
        if (!_assistantPlaybackMonitor.IsPlaybackActive)
        {
            return false;
        }

        return activePlaybackSnapshot?.IsAudiblePlaybackActive
            ?? activePlaybackSnapshot is not { IsHeld: true };
    }

    private bool IsAssistantPlaybackContextActive(ActiveSpeechPlaybackSnapshot? activePlaybackSnapshot)
    {
        return _assistantPlaybackMonitor.IsPlaybackActive
            || activePlaybackSnapshot is { IsActive: true }
            || activePlaybackSnapshot is { IsHeld: true };
    }

    private static bool IsStrongHeldBurstCandidate(
        BurstCapturePromotionSummary? burstPromotionSummary,
        SelfSpeechGateResult? gateResult,
        double threshold,
        BargeInOptions options,
        ActiveSpeechPlaybackSnapshot? activePlaybackSnapshot)
    {
        if (burstPromotionSummary is null || activePlaybackSnapshot is not { IsHeld: true })
        {
            return false;
        }

        if (gateResult?.Decision is SelfSpeechDecision.SuppressAsSelfEcho)
        {
            return false;
        }

        if (gateResult?.UserSpeechScore is null || gateResult.UserSpeechScore < threshold)
        {
            return false;
        }

        var candidateBurstMs = burstPromotionSummary.CandidateBurstMsAtPromotion;
        if (candidateBurstMs < Math.Max(1, options.BurstCapturePromotion.MinBurstMs))
        {
            return false;
        }

        var totalFrames = Math.Max(1, burstPromotionSummary.BurstTotalFrames);
        var suppressFrames = burstPromotionSummary.BurstSuppressAsSelfEchoFrames;
        var strongSelfEchoRatio = burstPromotionSummary.BurstStrongSelfEchoRatio;
        var suppressDominant = suppressFrames / (double)totalFrames >= 0.5;
        return !suppressDominant
            && strongSelfEchoRatio < Math.Clamp(options.BurstCapturePromotion.StrongSelfEchoVetoRatio, 0.0, 1.0);
    }

    private static bool IsRollingUserSpeechEvidenceEnabled(BargeInOptions options)
    {
        return false;
    }

    private static bool IsBetterRollingUserSpeechEvidence(
        SelfSpeechGateResult? candidate,
        SelfSpeechGateResult? current)
    {
        if (candidate is null)
        {
            return false;
        }

        if (current is null)
        {
            return true;
        }

        return candidate.UserSpeechScore > current.UserSpeechScore;
    }

    private async Task UpdateRollingUserSpeechEvidenceAsync(
        BargeInSpeechContext context,
        BargeInAudioFrame frame,
        SelfSpeechGateResult? gateResult,
        BargeInOptions options,
        string evidenceSource,
        CancellationToken cancellationToken)
    {
        if (!IsRollingUserSpeechEvidenceEnabled(options))
        {
            return;
        }

        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var assistantWasSpeaking = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot);
        if (!assistantWasSpeaking)
        {
            ResetRollingUserSpeechEvidence(context, options, "assistant_playback_inactive", gateResult, evidenceSource);
            return;
        }

        var userSpeechScore = gateResult?.UserSpeechScore;
        var snapshot = _rollingUserSpeechEvidence.Observe(
            frame.Timestamp,
            GetFrameDurationMs(frame, options),
            userSpeechScore,
            options);
        if (userSpeechScore is > 0.0)
        {
            _logger.LogInformation(
                "RollingUserSpeechEvidenceUpdated. EvidenceSource: {EvidenceSource}. UserSpeechScore: {UserSpeechScore:N3}. WindowMs: {WindowMs}. HighScoreThreshold: {HighScoreThreshold:N3}. HighScoreMsInWindow: {HighScoreMsInWindow}. RequiredHighScoreMs: {RequiredHighScoreMs}. AverageScore: {AverageScore:N3}. AverageScoreThreshold: {AverageScoreThreshold:N3}. RecentHighFramePresent: {RecentHighFramePresent}. RecentHighFrameWindowMs: {RecentHighFrameWindowMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. PlaybackAlreadyYielded: {PlaybackAlreadyYielded}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}.",
                evidenceSource,
                userSpeechScore.Value,
                snapshot.WindowMs,
                snapshot.HighScoreThreshold,
                snapshot.HighScoreMsInWindow,
                snapshot.RequiredHighScoreMs,
                snapshot.AverageScore,
                snapshot.AverageScoreThreshold,
                snapshot.RecentHighFramePresent,
                snapshot.RecentHighFrameWindowMs,
                assistantWasSpeaking,
                _playbackYieldedForRollingUserSpeechEvidence,
                gateResult?.Decision.ToString() ?? "Unavailable",
                gateResult?.Reason ?? "Self-speech gate result unavailable.",
                snapshot.DecisionReason);
        }
        else if (gateResult is null)
        {
            _logger.LogInformation(
                "RollingUserSpeechEvidenceSampleIgnored. EvidenceSource: {EvidenceSource}. UserSpeechScore: {UserSpeechScore}. WindowMs: {WindowMs}. ObservedMsInWindow: {ObservedMsInWindow}. AssistantWasSpeaking: {AssistantWasSpeaking}. PlaybackAlreadyYielded: {PlaybackAlreadyYielded}. DecisionReason: {DecisionReason}.",
                evidenceSource,
                "Unavailable",
                snapshot.WindowMs,
                snapshot.ObservedMsInWindow,
                assistantWasSpeaking,
                _playbackYieldedForRollingUserSpeechEvidence,
                "missing_score_ignored");
        }
        else if (snapshot.ObservedMsInWindow <= 0)
        {
            return;
        }

        _logger.LogInformation(
            "RollingUserSpeechEvidenceWindowComputed. EvidenceSource: {EvidenceSource}. WindowMs: {WindowMs}. HighScoreThreshold: {HighScoreThreshold:N3}. HighScoreMsInWindow: {HighScoreMsInWindow}. RequiredHighScoreMs: {RequiredHighScoreMs}. AverageScore: {AverageScore:N3}. AverageScoreThreshold: {AverageScoreThreshold:N3}. RecentHighFramePresent: {RecentHighFramePresent}. RecentHighFrameWindowMs: {RecentHighFrameWindowMs}. ObservedMsInWindow: {ObservedMsInWindow}. AssistantWasSpeaking: {AssistantWasSpeaking}. PlaybackAlreadyYielded: {PlaybackAlreadyYielded}. DecisionReason: {DecisionReason}.",
            evidenceSource,
            snapshot.WindowMs,
            snapshot.HighScoreThreshold,
            snapshot.HighScoreMsInWindow,
            snapshot.RequiredHighScoreMs,
            snapshot.AverageScore,
            snapshot.AverageScoreThreshold,
            snapshot.RecentHighFramePresent,
            snapshot.RecentHighFrameWindowMs,
            snapshot.ObservedMsInWindow,
            assistantWasSpeaking,
            _playbackYieldedForRollingUserSpeechEvidence,
            snapshot.DecisionReason);

        if (_playbackYieldedForRollingUserSpeechEvidence || !snapshot.ShouldYield)
        {
            return;
        }

        _playbackYieldedForRollingUserSpeechEvidence = true;
        _logger.LogInformation(
            "LegacyRollingUserSpeechEvidenceWouldHaveTriggered. EvidenceSource: {EvidenceSource}. UserSpeechScore: {UserSpeechScore}. WindowMs: {WindowMs}. HighScoreThreshold: {HighScoreThreshold:N3}. HighScoreMsInWindow: {HighScoreMsInWindow}. RequiredHighScoreMs: {RequiredHighScoreMs}. AverageScore: {AverageScore:N3}. AverageScoreThreshold: {AverageScoreThreshold:N3}. RecentHighFramePresent: {RecentHighFramePresent}. RecentHighFrameWindowMs: {RecentHighFrameWindowMs}. ObservedMsInWindow: {ObservedMsInWindow}. AssistantWasSpeaking: {AssistantWasSpeaking}. DecisionReason: {DecisionReason}. AssistantTurnId: {AssistantTurnId}. CorrelationId: {CorrelationId}.",
            evidenceSource,
            FormatUserSpeechScore(userSpeechScore),
            snapshot.WindowMs,
            snapshot.HighScoreThreshold,
            snapshot.HighScoreMsInWindow,
            snapshot.RequiredHighScoreMs,
            snapshot.AverageScore,
            snapshot.AverageScoreThreshold,
            snapshot.RecentHighFramePresent,
            snapshot.RecentHighFrameWindowMs,
            snapshot.ObservedMsInWindow,
            assistantWasSpeaking,
            snapshot.DecisionReason,
            context.AssistantTurnId,
            context.CorrelationId);
    }

    private void ResetRollingUserSpeechEvidence(
        BargeInSpeechContext context,
        BargeInOptions options,
        string reason,
        SelfSpeechGateResult? gateResult,
        string evidenceSource = "unknown")
    {
        if (!IsRollingUserSpeechEvidenceEnabled(options))
        {
            return;
        }

        var previousSnapshot = _rollingUserSpeechEvidence.Compute(DateTimeOffset.UtcNow, options);
        if (previousSnapshot.ObservedMsInWindow <= 0 && !_playbackYieldedForRollingUserSpeechEvidence)
        {
            return;
        }

        _playbackYieldedForRollingUserSpeechEvidence = false;
        var snapshot = _rollingUserSpeechEvidence.Reset(DateTimeOffset.UtcNow, options);
        var userSpeechScoreText = FormatUserSpeechScore(gateResult?.UserSpeechScore);
        _logger.LogInformation(
            "RollingUserSpeechEvidenceReset. EvidenceSource: {EvidenceSource}. UserSpeechScore: {UserSpeechScore}. WindowMs: {WindowMs}. HighScoreThreshold: {HighScoreThreshold:N3}. HighScoreMsInWindow: {HighScoreMsInWindow}. RequiredHighScoreMs: {RequiredHighScoreMs}. AverageScore: {AverageScore:N3}. AverageScoreThreshold: {AverageScoreThreshold:N3}. RecentHighFramePresent: {RecentHighFramePresent}. RecentHighFrameWindowMs: {RecentHighFrameWindowMs}. ObservedMsInWindow: {ObservedMsInWindow}. PreviousObservedMsInWindow: {PreviousObservedMsInWindow}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. ResetReason: {ResetReason}. AssistantTurnId: {AssistantTurnId}. CorrelationId: {CorrelationId}.",
            evidenceSource,
            userSpeechScoreText,
            snapshot.WindowMs,
            snapshot.HighScoreThreshold,
            snapshot.HighScoreMsInWindow,
            snapshot.RequiredHighScoreMs,
            snapshot.AverageScore,
            snapshot.AverageScoreThreshold,
            snapshot.RecentHighFramePresent,
            snapshot.RecentHighFrameWindowMs,
            snapshot.ObservedMsInWindow,
            previousSnapshot.ObservedMsInWindow,
            IsAssistantAudioActuallyPlaying(_playbackService.GetActivePlaybackSnapshot()),
            gateResult?.Decision.ToString() ?? "Unavailable",
            gateResult?.Reason ?? "Self-speech gate result unavailable.",
            reason,
            context.AssistantTurnId,
            context.CorrelationId);
    }

    private void ResetSustainedUserSpeechScoreGate(
        BargeInSpeechContext context,
        BargeInOptions options,
        string reason,
        SelfSpeechGateResult? gateResult,
        string candidateType = "unknown")
    {
        if (_sustainedUserSpeechScoreMs <= 0)
        {
            return;
        }

        var previousDurationMs = _sustainedUserSpeechScoreMs;
        _sustainedUserSpeechScoreMs = 0;
        _logger.LogInformation(
            "SustainedUserSpeechScoreGateReset. CandidateType: {CandidateType}. UserSpeechScore: {UserSpeechScore}. Threshold: {Threshold:N3}. AccumulatedDurationMs: {AccumulatedDurationMs}. RequiredDurationMs: {RequiredDurationMs}. AssistantWasSpeaking: {AssistantWasSpeaking}. SelfSpeechGateDecision: {SelfSpeechGateDecision}. SelfSpeechGateReason: {SelfSpeechGateReason}. DecisionReason: {DecisionReason}. AssistantTurnId: {AssistantTurnId}. CorrelationId: {CorrelationId}.",
            candidateType,
            FormatUserSpeechScore(gateResult?.UserSpeechScore),
            Math.Clamp(options.SustainedUserSpeechScoreThreshold, 0.0, 1.0),
            previousDurationMs,
            Math.Max(1, options.SustainedUserSpeechScoreDurationMs),
            IsAssistantAudioActuallyPlaying(_playbackService.GetActivePlaybackSnapshot()),
            gateResult?.Decision.ToString() ?? "Unavailable",
            gateResult?.Reason ?? "Self-speech gate result unavailable.",
            reason,
            context.AssistantTurnId,
            context.CorrelationId);
    }

    private static string FormatUserSpeechScore(double? userSpeechScore)
    {
        return userSpeechScore is null
            ? "Unavailable"
            : userSpeechScore.Value.ToString("N3", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int GetFrameDurationMs(BargeInAudioFrame frame, BargeInOptions options)
    {
        if (frame.DurationMs > 0)
        {
            return frame.DurationMs;
        }

        if (frame.SampleRate > 0 && frame.Samples.Length > 0)
        {
            return Math.Max(1, (int)Math.Round(frame.Samples.Length * 1000.0 / frame.SampleRate));
        }

        return Math.Max(1, options.FrameMs);
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

        if (promotionOptions.RequireAssistantPlayback && !IsAssistantPlaybackContextActive(_playbackService.GetActivePlaybackSnapshot()))
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
            LogCandidateStateDiagnostic(
                options,
                context,
                BargeInState.SoftPausedForUserSpeech,
                FormatBurstDiagnostic("Burst capture candidate started", snapshot, "speech-like activity observed"));
        }
        else
        {
            LogCandidateDiagnostic(options, context, FormatBurstDiagnostic("Burst capture candidate updated", snapshot, "collecting sustained near-end evidence"));
        }

        if (snapshot.StrongSelfEchoFrames >= Math.Max(1, promotionOptions.StrongSelfEchoVetoMinFrames)
            && snapshot.StrongSelfEchoRatio >= Math.Clamp(promotionOptions.StrongSelfEchoVetoRatio, 0.0, 1.0))
        {
            LogCandidateDiagnostic(
                options,
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
        LogCandidateDiagnostic(_options.CurrentValue, context, FormatBurstDiagnostic("Burst capture candidate reset", snapshot, reason));
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

    private CapturedWindowSelfPlaybackCheckResult EvaluateCapturedWindowSelfPlayback(
        IReadOnlyList<BargeInAudioFrame> captured,
        TimeSpan duration,
        string sttAudioSource,
        bool sttAudioIsAecProcessed,
        BargeInOptions options)
    {
        var snapshot = _playbackReferenceTap.GetDebugSnapshot();
        if (!options.EnableCapturedWindowSelfPlaybackCheck)
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Captured-window self-playback check is disabled.",
                snapshot);
        }

        var playbackRecentlyActive = _assistantPlaybackMonitor.IsPlaybackActive
            || snapshot.ReferenceNewestAgeMilliseconds <= Math.Max(0, options.CapturedWindowSelfPlaybackRecentPlaybackMs);
        if (!playbackRecentlyActive)
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Assistant playback is not active or recent.",
                snapshot);
        }

        if (captured.Count == 0)
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Captured audio window is empty.",
                snapshot);
        }

        var sampleRate = captured[0].SampleRate;
        if (sampleRate <= 0 || captured.Any(frame => frame.SampleRate != sampleRate))
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Captured audio window has invalid or mixed sample rates.",
                snapshot);
        }

        if (snapshot.SampleRate != sampleRate)
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Captured audio sample rate does not match playback reference sample rate.",
                snapshot);
        }

        var samples = captured
            .SelectMany(frame => frame.Samples.ToArray())
            .ToArray();
        if (samples.Length == 0)
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Captured audio window has no samples.",
                snapshot);
        }

        var captureEnergy = CalculateRms(samples);
        if (captureEnergy < Math.Max(0.0, options.CapturedWindowSelfPlaybackMinCaptureEnergy))
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "Captured audio energy is below the self-playback check minimum.",
                snapshot) with
            {
                CaptureEnergy = captureEnergy,
                SamplesChecked = samples.Length
            };
        }

        var sliceSampleCount = Math.Clamp(
            sampleRate * Math.Max(10, options.CapturedWindowSelfPlaybackSliceMs) / 1000,
            1,
            samples.Length);
        var sliceOffsets = SelectCapturedWindowSliceOffsets(
            samples,
            sliceSampleCount,
            Math.Max(1, options.CapturedWindowSelfPlaybackMaxSlices));
        var correlationOptions = CreateCapturedWindowCorrelationOptions(options);
        SelfSpeechCorrelationResult? bestCorrelation = null;
        double bestCaptureEnergy = 0.0;
        var bestOffset = 0;
        var availableSlices = 0;
        var samplesChecked = 0;

        foreach (var offset in sliceOffsets)
        {
            var slice = samples.AsSpan(offset, sliceSampleCount);
            var sliceEnergy = CalculateRms(slice);
            samplesChecked += sliceSampleCount;
            var correlation = SelfSpeechCorrelationDetector.Analyze(
                slice,
                sampleRate,
                _playbackReferenceTap,
                correlationOptions);
            if (correlation.IsAvailable)
            {
                availableSlices++;
            }

            _logger.LogDebug(
                "CapturedWindowCorrelationSliceEvaluated. SliceOffsetMs: {SliceOffsetMs}. SliceSamples: {SliceSamples}. SliceEnergy: {SliceEnergy:N4}. CorrelationAvailable: {CorrelationAvailable}. CorrelationDecision: {CorrelationDecision}. CorrelationScore: {CorrelationScore:N3}. BestDelayMs: {BestDelayMs}. ReferenceEnergy: {ReferenceEnergy:N4}. Reason: {Reason}. SttAudioSource: {SttAudioSource}. SttAudioIsAecProcessed: {SttAudioIsAecProcessed}.",
                offset * 1000.0 / sampleRate,
                sliceSampleCount,
                sliceEnergy,
                correlation.IsAvailable,
                correlation.Decision,
                correlation.CorrelationScore,
                correlation.BestDelayMs,
                correlation.ReferenceWindowEnergy,
                correlation.Reason,
                sttAudioSource,
                sttAudioIsAecProcessed);

            if (!correlation.IsAvailable)
            {
                continue;
            }

            if (bestCorrelation is null
                || (correlation.CorrelationScore ?? 0.0) > (bestCorrelation.CorrelationScore ?? 0.0))
            {
                bestCorrelation = correlation;
                bestCaptureEnergy = sliceEnergy;
                bestOffset = offset;
            }
        }

        if (bestCorrelation is null)
        {
            return CapturedWindowSelfPlaybackCheckResult.Unavailable(
                "No captured-window correlation slices were available.",
                snapshot) with
            {
                CaptureEnergy = captureEnergy,
                SliceCount = sliceOffsets.Count,
                SamplesChecked = samplesChecked
            };
        }

        var bestScore = bestCorrelation.CorrelationScore ?? 0.0;
        var bestReferenceEnergy = bestCorrelation.ReferenceWindowEnergy ?? 0.0;
        var threshold = Math.Clamp(options.CapturedWindowSelfPlaybackCorrelationThreshold, 0.0, 1.0);
        var likelyUserThreshold = Math.Clamp(options.CapturedWindowSelfPlaybackLikelyUserThreshold, 0.0, 1.0);
        var minReferenceEnergy = Math.Max(0.0, options.CapturedWindowSelfPlaybackMinReferenceEnergy);
        var strongUserEnergy = bestReferenceEnergy > 0.0
            && bestCaptureEnergy >= bestReferenceEnergy * Math.Max(1.0, options.CapturedWindowSelfPlaybackStrongUserEnergyRatio);

        var baseResult = new CapturedWindowSelfPlaybackCheckResult
        {
            IsAvailable = true,
            ShouldReject = false,
            Reason = "Captured-window correlation did not meet high-confidence self-playback rejection policy.",
            CaptureEnergy = captureEnergy,
            BestCaptureEnergy = bestCaptureEnergy,
            BestReferenceEnergy = bestReferenceEnergy,
            BestCorrelationScore = bestScore,
            BestDelayMs = bestCorrelation.BestDelayMs,
            BestSliceOffsetMs = bestOffset * 1000.0 / sampleRate,
            SliceCount = sliceOffsets.Count,
            AvailableSliceCount = availableSlices,
            SamplesChecked = samplesChecked,
            PlaybackReferenceNewestAgeMs = snapshot.ReferenceNewestAgeMilliseconds,
            PlaybackReferenceOldestAgeMs = snapshot.ReferenceOldestAgeMilliseconds
        };

        if (bestReferenceEnergy < minReferenceEnergy)
        {
            return baseResult with
            {
                IsAvailable = false,
                Reason = "Playback reference energy is below captured-window self-playback minimum."
            };
        }

        if (bestScore <= likelyUserThreshold)
        {
            return baseResult with
            {
                Reason = "Captured-window correlation is low, likely user speech."
            };
        }

        if (strongUserEnergy)
        {
            return baseResult with
            {
                Reason = "Captured-window energy strongly exceeds correlated playback reference; preserving possible near-end user speech."
            };
        }

        if (bestScore >= threshold)
        {
            return baseResult with
            {
                ShouldReject = true,
                Reason = "Captured audio window strongly correlates with recent assistant playback reference."
            };
        }

        return baseResult;
    }

    private static SelfSpeechSuppressionOptions CreateCapturedWindowCorrelationOptions(BargeInOptions options)
    {
        var source = options.SelfSpeechSuppression;
        return new SelfSpeechSuppressionOptions
        {
            Enabled = source.Enabled,
            SuppressDuringPlayback = source.SuppressDuringPlayback,
            PlaybackOnsetGraceMs = source.PlaybackOnsetGraceMs,
            EchoLeakageMultiplier = source.EchoLeakageMultiplier,
            EchoMargin = source.EchoMargin,
            UserSpeechRatio = source.UserSpeechRatio,
            UserSpeechMargin = source.UserSpeechMargin,
            RequireSustainedUserSpeechFrames = source.RequireSustainedUserSpeechFrames,
            AllowFastHardStopOverride = source.AllowFastHardStopOverride,
            LogDecisions = source.LogDecisions,
            DiagnosticsFileEnabled = source.DiagnosticsFileEnabled,
            DiagnosticsFilePath = source.DiagnosticsFilePath,
            DiagnosticsSampleEveryNFrames = source.DiagnosticsSampleEveryNFrames,
            DiagnosticsIncludeSuppressed = source.DiagnosticsIncludeSuppressed,
            DiagnosticsIncludeAllowed = source.DiagnosticsIncludeAllowed,
            DiagnosticsIncludeUncertain = source.DiagnosticsIncludeUncertain,
            PolicyMode = source.PolicyMode,
            AllowSustainedUncertainForDucking = source.AllowSustainedUncertainForDucking,
            AllowSustainedUncertainForCapture = source.AllowSustainedUncertainForCapture,
            AllowSustainedUncertainForFastHardStop = source.AllowSustainedUncertainForFastHardStop,
            FastHardStopUncertainFrames = source.FastHardStopUncertainFrames,
            FastHardStopUncertainExtraMargin = source.FastHardStopUncertainExtraMargin,
            CorrelationDetectionEnabled = true,
            CorrelationMinScore = options.CapturedWindowSelfPlaybackCorrelationThreshold,
            CorrelationSelfEchoThreshold = options.CapturedWindowSelfPlaybackCorrelationThreshold,
            CorrelationLikelyUserThreshold = options.CapturedWindowSelfPlaybackLikelyUserThreshold,
            CorrelationMinDelayMs = options.CapturedWindowSelfPlaybackDelayMsMin,
            CorrelationMaxDelayMs = options.CapturedWindowSelfPlaybackDelayMsMax,
            CorrelationStepMs = options.CapturedWindowSelfPlaybackDelayStepMs,
            CorrelationMinReferenceEnergy = options.CapturedWindowSelfPlaybackMinReferenceEnergy,
            CorrelationMinMicEnergy = options.CapturedWindowSelfPlaybackMinCaptureEnergy
        };
    }

    private static IReadOnlyList<int> SelectCapturedWindowSliceOffsets(
        float[] samples,
        int sliceSampleCount,
        int maxSlices)
    {
        var offsets = new List<int>();
        AddOffset(0);
        AddOffset((samples.Length - sliceSampleCount) / 2);
        AddOffset(samples.Length - sliceSampleCount);
        AddOffset(FindHighestEnergySliceOffset(samples, sliceSampleCount));
        return offsets.Take(maxSlices).ToArray();

        void AddOffset(int offset)
        {
            offset = Math.Clamp(offset, 0, Math.Max(0, samples.Length - sliceSampleCount));
            if (!offsets.Contains(offset))
            {
                offsets.Add(offset);
            }
        }
    }

    private static int FindHighestEnergySliceOffset(float[] samples, int sliceSampleCount)
    {
        if (samples.Length <= sliceSampleCount)
        {
            return 0;
        }

        var step = Math.Max(1, sliceSampleCount / 2);
        var bestOffset = 0;
        var bestEnergy = 0.0;
        for (var offset = 0; offset <= samples.Length - sliceSampleCount; offset += step)
        {
            var energy = CalculateRms(samples.AsSpan(offset, sliceSampleCount));
            if (energy > bestEnergy)
            {
                bestEnergy = energy;
                bestOffset = offset;
            }
        }

        return bestOffset;
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
        var timeline = CreateVoiceCaptureTimeline(context, captureSession, captureKind, burstPromotionSummary);
        var capturedUntil = await captureSession.WaitForEndpointAsync(cancellationToken);
        timeline.CaptureEndedUtc = capturedUntil;
        LogVoiceCaptureEndpointTriggered(timeline, captureSession);
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
        var sttAudioSource = builtFromContinuousRecorder
            ? "ContinuousMicAudioBufferRaw"
            : "TriggerBufferProcessed";
        var sttAudioIsAecProcessed = !builtFromContinuousRecorder;
        _logger.LogInformation(
            "CapturedWindowSelfPlaybackCheckStarted. CaptureKind: {CaptureKind}. CorrelationId: {CorrelationId}. DurationMs: {DurationMs}. Frames: {Frames}. SttAudioSource: {SttAudioSource}. SttAudioIsAecProcessed: {SttAudioIsAecProcessed}. ContinuousFrames: {ContinuousFrames}. TriggerFrames: {TriggerFrames}.",
            captureKind,
            context.CorrelationId,
            duration.TotalMilliseconds,
            captured.Count,
            sttAudioSource,
            sttAudioIsAecProcessed,
            continuousRange.Frames.Count,
            triggeredCapture.Frames.Count);
        var capturedWindowCheck = EvaluateCapturedWindowSelfPlayback(
            captured,
            duration,
            sttAudioSource,
            sttAudioIsAecProcessed,
            options);
        PublishDebugSnapshot(
            context,
            triggerFrame,
            triggerFrame,
            _playbackReferenceTap.GetLatestReferenceFrame(triggerFrame.Samples.Length),
            vad,
            null,
            null,
            captureKind.ToString(),
            BargeInState.CapturingInterruption.ToString(),
            CapturedWindowDecisionText(capturedWindowCheck),
            capturedWindowCheck.IsAvailable ? capturedWindowCheck.BestCorrelationScore : null,
            sttAudioSource,
            sttAudioIsAecProcessed,
            null,
            force: true);
        if (capturedWindowCheck.ShouldReject)
        {
            _logger.LogWarning(
                "CapturedWindowSelfPlaybackRejected. CorrelationId: {CorrelationId}. Reason: {Reason}. DurationMs: {DurationMs}. Frames: {Frames}. SamplesChecked: {SamplesChecked}. SliceCount: {SliceCount}. BestCorrelationScore: {BestCorrelationScore:N3}. BestDelayMs: {BestDelayMs}. BestSliceOffsetMs: {BestSliceOffsetMs}. BestReferenceEnergy: {BestReferenceEnergy:N4}. BestCaptureEnergy: {BestCaptureEnergy:N4}. CaptureEnergy: {CaptureEnergy:N4}. PlaybackReferenceNewestAgeMs: {PlaybackReferenceNewestAgeMs}. PlaybackReferenceOldestAgeMs: {PlaybackReferenceOldestAgeMs}. SttAudioSource: {SttAudioSource}. SttAudioIsAecProcessed: {SttAudioIsAecProcessed}.",
                context.CorrelationId,
                capturedWindowCheck.Reason,
                duration.TotalMilliseconds,
                captured.Count,
                capturedWindowCheck.SamplesChecked,
                capturedWindowCheck.SliceCount,
                capturedWindowCheck.BestCorrelationScore,
                capturedWindowCheck.BestDelayMs,
                capturedWindowCheck.BestSliceOffsetMs,
                capturedWindowCheck.BestReferenceEnergy,
                capturedWindowCheck.BestCaptureEnergy,
                capturedWindowCheck.CaptureEnergy,
                capturedWindowCheck.PlaybackReferenceNewestAgeMs,
                capturedWindowCheck.PlaybackReferenceOldestAgeMs,
                sttAudioSource,
                sttAudioIsAecProcessed);
            _logger.LogWarning(
                "SttSkippedForCapturedWindowSelfPlayback. CorrelationId: {CorrelationId}. CaptureKind: {CaptureKind}. SttAudioSource: {SttAudioSource}. Reason: {Reason}.",
                context.CorrelationId,
                captureKind,
                sttAudioSource,
                capturedWindowCheck.Reason);
            timeline.Suppress(
                "captured_window_self_playback",
                capturedWindowCheck.Reason,
                (int)Math.Round(duration.TotalMilliseconds),
                captureSession);
            LogVoiceCaptureSuppressed(timeline);
            LogVoiceCaptureTimelineCompleted(timeline);
            await ResumePreviousSpeechAsync(context, "captured_window_self_playback", cancellationToken);
            _vadService.Reset();
            return;
        }

        if (capturedWindowCheck.IsAvailable)
        {
            _logger.LogInformation(
                "CapturedWindowSelfPlaybackAllowed. CorrelationId: {CorrelationId}. Reason: {Reason}. DurationMs: {DurationMs}. Frames: {Frames}. SamplesChecked: {SamplesChecked}. SliceCount: {SliceCount}. BestCorrelationScore: {BestCorrelationScore:N3}. BestDelayMs: {BestDelayMs}. BestSliceOffsetMs: {BestSliceOffsetMs}. BestReferenceEnergy: {BestReferenceEnergy:N4}. BestCaptureEnergy: {BestCaptureEnergy:N4}. CaptureEnergy: {CaptureEnergy:N4}. SttAudioSource: {SttAudioSource}. SttAudioIsAecProcessed: {SttAudioIsAecProcessed}.",
                context.CorrelationId,
                capturedWindowCheck.Reason,
                duration.TotalMilliseconds,
                captured.Count,
                capturedWindowCheck.SamplesChecked,
                capturedWindowCheck.SliceCount,
                capturedWindowCheck.BestCorrelationScore,
                capturedWindowCheck.BestDelayMs,
                capturedWindowCheck.BestSliceOffsetMs,
                capturedWindowCheck.BestReferenceEnergy,
                capturedWindowCheck.BestCaptureEnergy,
                capturedWindowCheck.CaptureEnergy,
                sttAudioSource,
                sttAudioIsAecProcessed);
        }
        else
        {
            _logger.LogInformation(
                "CapturedWindowSelfPlaybackUnavailable. CorrelationId: {CorrelationId}. Reason: {Reason}. DurationMs: {DurationMs}. Frames: {Frames}. SamplesChecked: {SamplesChecked}. SliceCount: {SliceCount}. PlaybackReferenceNewestAgeMs: {PlaybackReferenceNewestAgeMs}. PlaybackReferenceOldestAgeMs: {PlaybackReferenceOldestAgeMs}. SttAudioSource: {SttAudioSource}. SttAudioIsAecProcessed: {SttAudioIsAecProcessed}.",
                context.CorrelationId,
                capturedWindowCheck.Reason,
                duration.TotalMilliseconds,
                captured.Count,
                capturedWindowCheck.SamplesChecked,
                capturedWindowCheck.SliceCount,
                capturedWindowCheck.PlaybackReferenceNewestAgeMs,
                capturedWindowCheck.PlaybackReferenceOldestAgeMs,
                sttAudioSource,
                sttAudioIsAecProcessed);
        }

        _diagnostics.GatedSttStarted(context, duration);
        timeline.AudioSentToSttMs = duration.TotalMilliseconds;
        timeline.SttStartedUtc = DateTimeOffset.UtcNow;
        LogVoiceCaptureSttStarted(timeline, captured);
        var sttStopwatch = Stopwatch.StartNew();
        var stt = await _sttService.TranscribeTriggerAsync(captured, options, cancellationToken);
        sttStopwatch.Stop();
        timeline.SttCompletedUtc = DateTimeOffset.UtcNow;
        timeline.SttLatencyMs = sttStopwatch.Elapsed.TotalMilliseconds;
        timeline.Transcript = stt.Transcript;
        timeline.TranscriptChars = stt.Transcript.Length;
        ApplyTranscriptHeuristics(timeline, stt.Transcript);
        LogVoiceCaptureSttCompleted(timeline, stt, captured);
        _diagnostics.GatedSttResult(context, stt);
        if (IsLiveMonitorContext(context) && !_liveTurnService.TryGetCurrentActiveTurn(out _))
        {
            _logger.LogInformation(
                "BackendIdleVoiceTranscript. Transcript: {Transcript}. AudioMs: {AudioMs}.",
                stt.Transcript,
                stt.AudioDuration.TotalMilliseconds);
        }
        _diagnostics.StateChanged(context, BargeInState.ClassifyingInterruption, $"Interruption transcript captured: {stt.Transcript}");

        var normalized = InterruptionClassifier.Normalize(stt.Transcript);
        var utterance = CreateUserUtterance(context, stt.Transcript, vad.Confidence, timeline.CaptureId);
        if (await TryConsumePendingInterruptionClarificationAsync(context, utterance, cancellationToken))
        {
            timeline.ToolResult = "pending_interruption_clarification_response";
            LogVoiceCaptureTimelineCompleted(timeline);
            _vadService.Reset();
            return;
        }

        if (IsLiveMonitorContext(context) && !utterance.AssistantWasSpeaking)
        {
            var awakeResult = _merlinAwakeState.EvaluateVoiceActivity(
                utterance.Text,
                utterance.CorrelationId,
                utterance.ActiveTurnId);
            if (awakeResult.IsWakePhrase || awakeResult.IsSleepPhrase || !awakeResult.ShouldAllow)
            {
                var awakeRouteDecision = new UtteranceRouteDecision
                {
                    Kind = UtteranceRouteKind.BackgroundOrNoOp,
                    Confidence = 1.0,
                    Reason = awakeResult.Reason,
                    Action = awakeResult.IsWakePhrase
                        ? "wake_phrase_accepted"
                        : awakeResult.IsSleepPhrase && awakeResult.ShouldAllow
                            ? "sleep_phrase_accepted"
                            : "merlin_sleeping"
                };
                timeline.MarkRouted(
                    utterance,
                    null,
                    awakeRouteDecision);
                LogVoiceCaptureRouted(
                    timeline,
                    utterance,
                    null,
                    awakeRouteDecision);
                _logger.LogInformation(
                    "BackendIdleVoiceAwakeGateHandled. CaptureId: {CaptureId}. Text: {Text}. Decision: {Decision}. Reason: {Reason}.",
                    utterance.CaptureId,
                    utterance.Text,
                    awakeResult.Decision,
                    awakeResult.Reason);
                if (awakeResult.IsWakePhrase || (awakeResult.IsSleepPhrase && awakeResult.ShouldAllow))
                {
                    await RaiseBackendVoiceRequestCapturedAsync(context, utterance, cancellationToken);
                }

                timeline.ToolResult = awakeResult.IsWakePhrase
                    ? "wake_phrase_accepted"
                    : awakeResult.IsSleepPhrase && awakeResult.ShouldAllow
                        ? "sleep_phrase_accepted"
                        : "ignored_while_sleeping";
                LogVoiceCaptureTimelineCompleted(timeline);
                _vadService.Reset();
                return;
            }
        }
        _diagnostics.StateChanged(
            context,
            BargeInState.ClassifyingInterruption,
            $"UserUtteranceCaptured. activeTurnId={utterance.ActiveTurnId ?? "(none)"} stateWhenCaptured={utterance.StateWhenCaptured} assistantWasSpeaking={utterance.AssistantWasSpeaking} text={utterance.Text}");
        _logger.LogInformation(
            "UserUtteranceCaptured. CaptureId: {CaptureId}. Text: {Text}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. AssistantWasSpeaking: {AssistantWasSpeaking}. Confidence: {Confidence}.",
            utterance.CaptureId,
            utterance.Text,
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            utterance.StateWhenCaptured,
            utterance.AssistantWasSpeaking,
            utterance.Confidence);
        await EmitAssistantUiStateCoalescedAsync(
            AssistantUiStateEvent.Create(
                "thinking",
                utterance.AssistantWasSpeaking
                    ? "interruption_handling_started"
                    : "backend_idle_voice_request_accepted",
                utterance.CorrelationId,
                utterance.ActiveTurnId,
                interruptionState: utterance.AssistantWasSpeaking
                    ? AssistantUiStateEvent.InterruptionStateHandling
                    : AssistantUiStateEvent.InterruptionStateNone),
            nameof(BargeInCoordinator),
            cancellationToken);
        var gateResult = _liveUtteranceGate?.Evaluate(CreateLiveUtteranceGateInput(utterance, vad.Confidence));
        var routeDecision = gateResult is not null && _liveUtteranceGate is not null
            ? _liveUtteranceGate.ToRouteDecision(utterance, gateResult)
            : RouteUtterance(utterance);
        if (IsLiveMonitorContext(context)
            && routeDecision.Kind is not UtteranceRouteKind.BackgroundOrNoOp
            && routeDecision.Kind is not UtteranceRouteKind.Unknown)
        {
            _merlinAwakeState?.TouchActivity();
        }
        timeline.MarkRouted(utterance, gateResult, routeDecision);
        LogVoiceCaptureRouted(timeline, utterance, gateResult, routeDecision);
        _logger.LogInformation(
            "UserUtteranceRouted. CaptureId: {CaptureId}. Text: {Text}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. Route: {Route}. Confidence: {Confidence}. Action: {Action}. Reason: {Reason}.",
            utterance.CaptureId,
            utterance.Text,
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            utterance.StateWhenCaptured,
            routeDecision.Kind,
            routeDecision.Confidence,
            routeDecision.Action,
            routeDecision.Reason);
        if (!await TryHandleConversationalInterruptionLiveSeamAsync(
            context,
            utterance,
            routeDecision,
            gateResult,
            vad,
            captureKind,
            captured,
            capturedUntil,
            cancellationToken))
        {
            timeline.ToolResult = "conversational_interruption_handled";
            LogVoiceCaptureTimelineCompleted(timeline);
            _vadService.Reset();
            return;
        }
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
        if (IsHardStopGatePhrase(normalized))
        {
            if (ShouldRejectStopCommandMissingWakePrefix(utterance.AssistantWasSpeaking, normalized, options, out var rejectionReason))
            {
                LogStopCommandWakePrefixCheck(utterance.AssistantWasSpeaking, normalized, options, "rejected_missing_wake_prefix", rejectionReason);
                _logger.LogInformation(
                    "StopCommandRejectedMissingWakePrefix. NormalizedTranscript: {NormalizedTranscript}. WakePrefix: {WakePrefix}. AssistantWasSpeaking: {AssistantWasSpeaking}. DecisionReason: {DecisionReason}.",
                    normalized,
                    InterruptionClassifier.Normalize(options.StopWakePrefix),
                    utterance.AssistantWasSpeaking,
                    rejectionReason);
            }
            else if (options.RequireWakePrefixForStopDuringPlayback && utterance.AssistantWasSpeaking && HasStopWakePrefix(normalized, options))
            {
                LogStopCommandWakePrefixCheck(utterance.AssistantWasSpeaking, normalized, options, "accepted_with_wake_prefix", "Stop command met wake-prefix policy.");
                _logger.LogInformation(
                    "StopCommandAcceptedWithWakePrefix. NormalizedTranscript: {NormalizedTranscript}. WakePrefix: {WakePrefix}. AssistantWasSpeaking: {AssistantWasSpeaking}. DecisionReason: {DecisionReason}.",
                    normalized,
                    InterruptionClassifier.Normalize(options.StopWakePrefix),
                    utterance.AssistantWasSpeaking,
                    "Stop command accepted as playback hard stop.");
            }
        }

        _diagnostics.ClassificationResult(context, classification);
        var decision = captureKind is CaptureKind.FastHardStop
            ? DecideFastHardStop(context, classification, options)
            : Decide(context, classification, options);
        PublishDebugSnapshot(
            context,
            triggerFrame,
            triggerFrame,
            _playbackReferenceTap.GetLatestReferenceFrame(triggerFrame.Samples.Length),
            vad,
            null,
            null,
            captureKind.ToString(),
            BargeInState.ClassifyingInterruption.ToString(),
            CapturedWindowDecisionText(capturedWindowCheck),
            capturedWindowCheck.IsAvailable ? capturedWindowCheck.BestCorrelationScore : null,
            sttAudioSource,
            sttAudioIsAecProcessed,
            $"{(decision.Accepted ? "accepted" : "rejected")}:{decision.Action}",
            force: true);
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
        timeline.EndpointReason = captureSession.EndReason;
        timeline.CaptureWindowMs = Math.Max(0, (capturedUntil - triggerFrame.Timestamp).TotalMilliseconds);
        timeline.AudioSentToSttMs = duration.TotalMilliseconds;
        timeline.ToolResult = decision.Accepted ? decision.Action.ToString() : "ignored";
        if (gateResult is not null && IsDecisiveGateDecision(gateResult, utterance))
        {
            _logger.LogInformation(
                "LiveUtteranceGateDecisionApplied. CaptureId: {CaptureId}. Text: {Text}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. GateDecision: {GateDecision}. Route: {Route}. Action: {Action}. LegacyClassificationType: {LegacyClassificationType}. LegacyAction: {LegacyAction}.",
                utterance.CaptureId,
                utterance.Text,
                utterance.ActiveTurnId,
                utterance.CorrelationId,
                utterance.StateWhenCaptured,
                gateResult.Decision,
                routeDecision.Kind,
                routeDecision.Action,
                classification.Type,
                decision.Action);
            if (decision.Action is BargeInAction.Resume or BargeInAction.SideComment or BargeInAction.Clarification)
            {
                _logger.LogInformation(
                    "LegacyInterruptionClassifierSuppressed. Text: {Text}. GateDecision: {GateDecision}. LegacyClassificationType: {LegacyClassificationType}. LegacyAction: {LegacyAction}. Reason: Decisive LiveUtteranceGate decision controls routing.",
                    utterance.Text,
                    gateResult.Decision,
                    classification.Type,
                    decision.Action);
                _logger.LogInformation(
                    "PlaybackResumeSuppressedByGate. Text: {Text}. GateDecision: {GateDecision}. LegacyAction: {LegacyAction}.",
                    utterance.Text,
                    gateResult.Decision,
                    decision.Action);
            }

            await ApplyDecisiveGateDecisionAsync(context, utterance, routeDecision, gateResult, cancellationToken);
            LogVoiceCaptureTimelineCompleted(timeline);
            _vadService.Reset();
            return;
        }

        if (gateResult is not null)
        {
            _logger.LogInformation(
                "GateDecisionAllowedLegacyFallback. CaptureId: {CaptureId}. Text: {Text}. GateDecision: {GateDecision}. Route: {Route}. Action: {Action}. LegacyClassificationType: {LegacyClassificationType}. LegacyAction: {LegacyAction}.",
                utterance.CaptureId,
                utterance.Text,
                gateResult.Decision,
                routeDecision.Kind,
                routeDecision.Action,
                classification.Type,
                decision.Action);
        }

        if (await TryHandleLiveUtteranceRouteAsync(context, utterance, routeDecision, cancellationToken))
        {
            LogVoiceCaptureTimelineCompleted(timeline);
            _vadService.Reset();
            return;
        }

        if (!decision.Accepted)
        {
            _diagnostics.Ignored(context, decision.Reason);
            await ResumePreviousSpeechAsync(context, decision.Reason, cancellationToken);
            LogVoiceCaptureTimelineCompleted(timeline);
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
            LogVoiceCaptureTimelineCompleted(timeline);
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
            await RaiseCorrectionRegenerationRequestedAsync(context, correctionText, timeline.CaptureId, cancellationToken);
        }

        LogVoiceCaptureTimelineCompleted(timeline);
    }

    private void LogCandidateDiagnostic(BargeInOptions options, BargeInSpeechContext context, string reason)
    {
        if (!options.EnableBargeInCandidateDiagnostics)
        {
            return;
        }

        if (!ShouldEmitRepeatedDiagnostic(options, $"candidate:{context.AssistantTurnId}:{reason}"))
        {
            return;
        }

        _diagnostics.Ignored(context, reason);
    }

    private void LogCandidateStateDiagnostic(
        BargeInOptions options,
        BargeInSpeechContext context,
        BargeInState state,
        string reason)
    {
        if (!options.EnableBargeInCandidateDiagnostics)
        {
            return;
        }

        if (!ShouldEmitRepeatedDiagnostic(options, $"candidate_state:{context.AssistantTurnId}:{state}:{reason}"))
        {
            return;
        }

        _diagnostics.StateChanged(context, state, reason);
    }

    private void LogLegacyDiagnostic(BargeInOptions options, BargeInSpeechContext context, string reason)
    {
        if (!options.EnableBargeInLegacyDiagnostics)
        {
            return;
        }

        if (!ShouldEmitRepeatedDiagnostic(options, $"legacy:{context.AssistantTurnId}:{reason}"))
        {
            return;
        }

        _diagnostics.Ignored(context, reason);
    }

    private bool ShouldEmitRepeatedDiagnostic(BargeInOptions options, string key)
    {
        var throttleMs = Math.Max(0, options.BargeInRepeatedDiagnosticThrottleMs);
        if (throttleMs == 0)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            if (_lastRepeatedDiagnosticAt.TryGetValue(key, out var last)
                && (now - last).TotalMilliseconds < throttleMs)
            {
                return false;
            }

            _lastRepeatedDiagnosticAt[key] = now;
            return true;
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
        if (!options.SaveDebugAudio || !options.EnableSuppressedCaptureDiagnostics)
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

    private VoiceCaptureTimeline CreateVoiceCaptureTimeline(
        BargeInSpeechContext context,
        BargeInCaptureSession captureSession,
        CaptureKind captureKind,
        BurstCapturePromotionSummary? burstPromotionSummary)
    {
        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var playbackReferenceSnapshot = _playbackReferenceTap.GetDebugSnapshot();
        return new VoiceCaptureTimeline
        {
            CaptureId = captureSession.CaptureId,
            TurnId = context.AssistantTurnId,
            CorrelationId = context.CorrelationId,
            Source = IsLiveMonitorContext(context) ? "backend_idle_voice" : "barge_in",
            AcousticCaptureMode = captureSession.Mode.ToString(),
            SpeechType = context.SpeechType.ToString(),
            ItemType = context.SpeechType.ToString(),
            HoldId = activePlaybackSnapshot?.HoldId,
            AssistantPlaybackContextActive = IsAssistantPlaybackContextActive(activePlaybackSnapshot),
            AssistantAudioActuallyPlaying = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot),
            PlaybackReferenceAgeMs = playbackReferenceSnapshot.ReferenceNewestAgeMilliseconds,
            CaptureStartedUtc = DateTimeOffset.UtcNow,
            CaptureKind = GetCaptureKindName(captureKind),
            RequiredEndpointSilenceMs = captureSession.RequiredEndpointSilenceMs,
            BurstCandidateMs = burstPromotionSummary?.CandidateBurstMsAtPromotion,
            BurstAllowFrames = burstPromotionSummary?.BurstAllowFrames,
            BurstUncertainFrames = burstPromotionSummary?.BurstUncertainFrames,
            BurstSuppressFrames = burstPromotionSummary?.BurstSuppressAsSelfEchoFrames
        };
    }

    private void LogVoiceCaptureTimelineStarted(
        BargeInCaptureSession captureSession,
        BargeInSpeechContext context,
        AcousticCaptureMode acousticMode,
        ReadOnlyMemory<float> playbackReference,
        CaptureKind captureKind)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var playbackReferenceSnapshot = _playbackReferenceTap.GetDebugSnapshot();
        var playbackReferenceRms = playbackReference.IsEmpty ? (double?)null : CalculateRms(playbackReference.Span);
        _logger.LogInformation(
            "voice_capture_timeline_started CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Source: {Source}. AcousticCaptureMode: {AcousticCaptureMode}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. SpeechType: {SpeechType}. ItemType: {ItemType}. HoldId: {HoldId}. PlaybackReferenceRms: {PlaybackReferenceRms}. PlaybackReferenceAgeMs: {PlaybackReferenceAgeMs}. CaptureKind: {CaptureKind}. RequiredEndpointSilenceMs: {RequiredEndpointSilenceMs}.",
            captureSession.CaptureId,
            context.AssistantTurnId,
            context.CorrelationId,
            IsLiveMonitorContext(context) ? "backend_idle_voice" : "barge_in",
            acousticMode,
            IsAssistantPlaybackContextActive(activePlaybackSnapshot),
            IsAssistantAudioActuallyPlaying(activePlaybackSnapshot),
            context.SpeechType,
            context.SpeechType,
            activePlaybackSnapshot?.HoldId,
            playbackReferenceRms,
            playbackReferenceSnapshot.ReferenceNewestAgeMilliseconds,
            GetCaptureKindName(captureKind),
            captureSession.RequiredEndpointSilenceMs);
    }

    private void LogVoiceCaptureEndpointTriggered(VoiceCaptureTimeline timeline, BargeInCaptureSession captureSession)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        var lastFrame = captureSession.GetFrameDiagnostics().LastOrDefault();
        timeline.EndpointReason = captureSession.EndReason;
        timeline.EndpointSilenceMs = lastFrame?.EndpointSilenceMs;
        timeline.RequiredEndpointSilenceMs = lastFrame?.RequiredEndpointSilenceMs ?? captureSession.RequiredEndpointSilenceMs;
        timeline.CaptureWindowMs = captureSession.CaptureStartedToLastFrameMs;
        timeline.PostRawSpeechTailMs = lastFrame?.EndpointSilenceMs;
        timeline.PlaybackReferenceRms = lastFrame?.PlaybackReferenceRms;
        timeline.PlaybackReferenceAgeMs = lastFrame?.PlaybackReferenceAgeMs;
        _logger.LogInformation(
            "voice_capture_endpoint_triggered CaptureId: {CaptureId}. EndpointReason: {EndpointReason}. EndpointSilenceMs: {EndpointSilenceMs}. RequiredEndpointSilenceMs: {RequiredEndpointSilenceMs}. AudioMsSoFar: {AudioMsSoFar}. LastSpeechFrameRelativeMs: {LastSpeechFrameRelativeMs}. RawSpeechActive: {RawSpeechActive}. AecSpeechActive: {AecSpeechActive}. VadSaysSpeech: {VadSaysSpeech}. CaptureIsSpeechFrame: {CaptureIsSpeechFrame}.",
            timeline.CaptureId,
            timeline.EndpointReason,
            timeline.EndpointSilenceMs,
            timeline.RequiredEndpointSilenceMs,
            timeline.CaptureWindowMs,
            captureSession.LastSpeechFrameRelativeMs,
            lastFrame?.RawSpeechActive,
            lastFrame?.AecSpeechActive,
            lastFrame?.VadSaysSpeech,
            lastFrame?.CaptureIsSpeechFrame);
    }

    private void LogVoiceCaptureSttStarted(VoiceCaptureTimeline timeline, IReadOnlyList<BargeInAudioFrame> captured)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        _logger.LogInformation(
            "voice_capture_stt_started CaptureId: {CaptureId}. AudioSentToSttMs: {AudioSentToSttMs}. Samples: {Samples}. Model: {Model}. Device: {Device}.",
            timeline.CaptureId,
            timeline.AudioSentToSttMs,
            captured.Sum(frame => frame.Samples.Length),
            "configured_barge_in_stt",
            "configured");
    }

    private void LogVoiceCaptureSttCompleted(
        VoiceCaptureTimeline timeline,
        BargeInSttResult stt,
        IReadOnlyList<BargeInAudioFrame> captured)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        _logger.LogInformation(
            "voice_capture_stt_completed CaptureId: {CaptureId}. AudioSentToSttMs: {AudioSentToSttMs}. Samples: {Samples}. Model: {Model}. Device: {Device}. LatencyMs: {LatencyMs}. Transcript: {Transcript}. TranscriptChars: {TranscriptChars}. TranscriptLooksIncomplete: {TranscriptLooksIncomplete}. EndsWithCorrectionPrefix: {EndsWithCorrectionPrefix}.",
            timeline.CaptureId,
            timeline.AudioSentToSttMs,
            captured.Sum(frame => frame.Samples.Length),
            "configured_barge_in_stt",
            "configured",
            timeline.SttLatencyMs,
            stt.Transcript,
            stt.Transcript.Length,
            timeline.TranscriptLooksIncomplete,
            timeline.EndsWithCorrectionPrefix);
    }

    private void LogVoiceCaptureRouted(
        VoiceCaptureTimeline timeline,
        UserUtterance utterance,
        LiveUtteranceGateResult? gateResult,
        UtteranceRouteDecision routeDecision)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        _logger.LogInformation(
            "voice_capture_routed CaptureId: {CaptureId}. Transcript: {Transcript}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. AssistantWasSpeaking: {AssistantWasSpeaking}. LiveGateDecision: {LiveGateDecision}. Route: {Route}. RouteAction: {RouteAction}. ReplacementText: {ReplacementText}.",
            timeline.CaptureId,
            utterance.Text,
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            utterance.StateWhenCaptured,
            utterance.AssistantWasSpeaking,
            gateResult?.Decision.ToString(),
            routeDecision.Kind,
            routeDecision.Action,
            routeDecision.ReplacementText);
    }

    private void LogVoiceCaptureSuppressed(VoiceCaptureTimeline timeline)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        _logger.LogInformation(
            "voice_capture_suppressed CaptureId: {CaptureId}. CaptureKind: {CaptureKind}. SuppressionReason: {SuppressionReason}. EndpointReason: {EndpointReason}. AudioSentToSttMs: {AudioSentToSttMs}.",
            timeline.CaptureId,
            timeline.CaptureKind,
            timeline.SuppressionReason,
            timeline.EndpointReason,
            timeline.AudioSentToSttMs);
    }

    private void LogVoiceCaptureTimelineCompleted(VoiceCaptureTimeline timeline)
    {
        if (!_options.CurrentValue.EnableVoiceCaptureTimelineDiagnostics)
        {
            return;
        }

        timeline.RoutedUtc ??= DateTimeOffset.UtcNow;
        if (timeline.CaptureStartedUtc is not null)
        {
            timeline.TotalCaptureToRouteMs = Math.Max(0, (timeline.RoutedUtc.Value - timeline.CaptureStartedUtc.Value).TotalMilliseconds);
        }

        _logger.LogInformation(
            "voice_capture_timeline_completed CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Source: {Source}. AcousticCaptureMode: {AcousticCaptureMode}. SpeechType: {SpeechType}. ItemType: {ItemType}. HoldId: {HoldId}. AssistantPlaybackContextActive: {AssistantPlaybackContextActive}. AssistantAudioActuallyPlaying: {AssistantAudioActuallyPlaying}. PlaybackReferenceRms: {PlaybackReferenceRms}. PlaybackReferenceAgeMs: {PlaybackReferenceAgeMs}. CaptureStartedUtc: {CaptureStartedUtc}. CaptureEndedUtc: {CaptureEndedUtc}. SttStartedUtc: {SttStartedUtc}. SttCompletedUtc: {SttCompletedUtc}. RoutedUtc: {RoutedUtc}. CaptureKind: {CaptureKind}. EndpointReason: {EndpointReason}. SuppressionReason: {SuppressionReason}. AudioSentToSttMs: {AudioSentToSttMs}. EndpointSilenceMs: {EndpointSilenceMs}. RequiredEndpointSilenceMs: {RequiredEndpointSilenceMs}. SttLatencyMs: {SttLatencyMs}. TotalCaptureToRouteMs: {TotalCaptureToRouteMs}. Transcript: {Transcript}. TranscriptChars: {TranscriptChars}. TranscriptLooksIncomplete: {TranscriptLooksIncomplete}. EndsWithCorrectionPrefix: {EndsWithCorrectionPrefix}. LiveGateDecision: {LiveGateDecision}. LiveGateRoute: {LiveGateRoute}. RouteAction: {RouteAction}. ReplacementText: {ReplacementText}. CommandRouterNormalizedText: {CommandRouterNormalizedText}. Intent: {Intent}. ToolName: {ToolName}. ToolResult: {ToolResult}. SpokenResponse: {SpokenResponse}.",
            timeline.CaptureId,
            timeline.TurnId,
            timeline.CorrelationId,
            timeline.Source,
            timeline.AcousticCaptureMode,
            timeline.SpeechType,
            timeline.ItemType,
            timeline.HoldId,
            timeline.AssistantPlaybackContextActive,
            timeline.AssistantAudioActuallyPlaying,
            timeline.PlaybackReferenceRms,
            timeline.PlaybackReferenceAgeMs,
            timeline.CaptureStartedUtc,
            timeline.CaptureEndedUtc,
            timeline.SttStartedUtc,
            timeline.SttCompletedUtc,
            timeline.RoutedUtc,
            timeline.CaptureKind,
            timeline.EndpointReason,
            timeline.SuppressionReason,
            timeline.AudioSentToSttMs,
            timeline.EndpointSilenceMs,
            timeline.RequiredEndpointSilenceMs,
            timeline.SttLatencyMs,
            timeline.TotalCaptureToRouteMs,
            timeline.Transcript,
            timeline.TranscriptChars,
            timeline.TranscriptLooksIncomplete,
            timeline.EndsWithCorrectionPrefix,
            timeline.LiveGateDecision,
            timeline.LiveGateRoute,
            timeline.RouteAction,
            timeline.ReplacementText,
            timeline.CommandRouterNormalizedText,
            timeline.Intent,
            timeline.ToolName,
            timeline.ToolResult,
            timeline.SpokenResponse);
    }

    private static void ApplyTranscriptHeuristics(VoiceCaptureTimeline timeline, string transcript)
    {
        var normalized = InterruptionClassifier.Normalize(transcript);
        var endsWithCorrectionPrefix = IsIncompleteCorrectionPrefixTranscript(normalized);
        timeline.TranscriptLooksIncomplete = endsWithCorrectionPrefix
            || normalized is "yeah but" or "but" or "sorry i meant" or "i meant" or "i mean";
        timeline.EndsWithCorrectionPrefix = endsWithCorrectionPrefix;
    }

    private static bool IsIncompleteCorrectionPrefixTranscript(string normalized)
    {
        var prefixes = new[]
        {
            "no what i meant is",
            "what i meant is",
            "i meant",
            "i mean",
            "no i meant",
            "no i mean",
            "actually i meant",
            "wait i meant",
            "sorry i meant",
            "no what i mean is",
            "what i mean is"
        };

        return prefixes.Any(prefix => string.Equals(normalized, prefix, StringComparison.Ordinal));
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
        var captureId = $"capture:{Guid.NewGuid():N}"[..40];
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
        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var suppressedTimeline = new VoiceCaptureTimeline
        {
            CaptureId = captureId,
            TurnId = context.AssistantTurnId,
            CorrelationId = context.CorrelationId,
            Source = IsLiveMonitorContext(context) ? "backend_idle_voice" : "barge_in",
            AcousticCaptureMode = SelectAcousticCaptureModeForFrame(_playbackReferenceTap.GetLatestReferenceFrame(triggerFrame.Samples.Length), options).ToString(),
            SpeechType = context.SpeechType.ToString(),
            ItemType = context.SpeechType.ToString(),
            HoldId = activePlaybackSnapshot?.HoldId,
            AssistantPlaybackContextActive = IsAssistantPlaybackContextActive(activePlaybackSnapshot),
            AssistantAudioActuallyPlaying = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot),
            CaptureStartedUtc = captured[0].Timestamp,
            CaptureEndedUtc = captured[^1].Timestamp,
            CaptureKind = captureKind,
            EndpointReason = "suppressed_before_stt",
            SuppressionReason = reason,
            AudioSentToSttMs = duration.TotalMilliseconds,
            Transcript = string.Empty,
            TranscriptChars = 0,
            ToolResult = "suppressed"
        };
        LogVoiceCaptureSuppressed(suppressedTimeline);
        LogVoiceCaptureTimelineCompleted(suppressedTimeline);
        var diagnostic = new InterruptionCaptureDiagnostic
        {
            CaptureId = captureId,
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
            CaptureId = captureSession.CaptureId,
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
        string? captureId,
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
            CaptureId = captureId,
            OriginalCorrelationId = correlationId,
            CorrectionText = correctionText,
            SpeechContext = context
        };

        foreach (Func<CorrectionRegenerationRequested, CancellationToken, Task> handler in handlers.GetInvocationList())
        {
            await handler(request, cancellationToken);
        }
    }

    private async Task RaiseBackendVoiceRequestCapturedAsync(
        BargeInSpeechContext context,
        UserUtterance utterance,
        CancellationToken cancellationToken)
    {
        var text = utterance.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation(
                "BackendIdleVoiceRequestIgnored. Reason: Empty transcript. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}.",
                utterance.ActiveTurnId,
                utterance.CorrelationId);
            return;
        }

        var options = _voiceInputOptions.CurrentValue;
        if (!options.BackendVoiceInputEnabled)
        {
            _logger.LogInformation(
                "BackendIdleVoiceRequestIgnored. Text: {Text}. Reason: Backend voice input disabled. Owner: {Owner}.",
                text,
                options.Owner);
            return;
        }

        var handlers = BackendVoiceRequestCaptured;
        if (handlers is null)
        {
            _logger.LogInformation(
                "BackendIdleVoiceRequestIgnored. Text: {Text}. Reason: No backend voice request handler attached.",
                text);
            return;
        }

        var correlationId = $"backend_voice:{Guid.NewGuid():N}";
        var source = string.IsNullOrWhiteSpace(options.BackendIdleVoiceInteractionSource)
            ? "backend_idle_voice"
            : options.BackendIdleVoiceInteractionSource.Trim();
        var request = new BackendVoiceRequestCaptured
        {
            CaptureId = utterance.CaptureId,
            CorrelationId = correlationId,
            Text = text,
            InteractionSource = source,
            Utterance = utterance,
            SpeechContext = context
        };

        _logger.LogInformation(
            "BackendIdleVoiceRequestAccepted. CaptureId: {CaptureId}. Text: {Text}. CorrelationId: {CorrelationId}. Source: {Source}.",
            utterance.CaptureId,
            text,
            correlationId,
            source);

        foreach (Func<BackendVoiceRequestCaptured, CancellationToken, Task> handler in handlers.GetInvocationList())
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
                LogLegacyDiagnostic(options, context, $"Legacy active-capture ducking disabled. Gate did not allow active capture frame. Decision: {gateResult.Decision}. Reason: {gateResult.Reason}.");
                return;
            }

            LogLegacyDiagnostic(options, context, $"Legacy active-capture ducking disabled. Gate allowed active capture frame. Decision: {gateResult.Decision}. Reason: {gateResult.Reason}.");
            return;
        }
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
        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var assistantAudioActuallyPlaying = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot);
        var input = new SelfSpeechGateInput
        {
            AssistantPlaybackActive = assistantAudioActuallyPlaying,
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
            _sustainedUserSpeechScoreMs = 0;
            ResetRollingUserSpeechEvidence(context, options, "speech_started", null, "speech_started");
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
        var options = _options.CurrentValue;
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
                _sustainedUserSpeechScoreMs = 0;
                ResetRollingUserSpeechEvidence(context, options, "speech_stopped", null, "speech_stopped");
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
        double confidence,
        string? captureId)
    {
        LiveAssistantTurnState state = _assistantPlaybackMonitor.IsPlaybackActive
            ? LiveAssistantTurnState.Speaking
            : IsLiveMonitorContext(context)
                ? LiveAssistantTurnState.IdleListening
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

        var originalAssistantWasSpeaking = playbackActive || state is LiveAssistantTurnState.Speaking;
        var provisionalUtterance = new UserUtterance
        {
            CaptureId = captureId,
            Text = transcript.Trim(),
            TimestampUtc = DateTimeOffset.UtcNow,
            ActiveTurnId = activeTurnId,
            CorrelationId = correlationId,
            StateWhenCaptured = state,
            AssistantWasSpeaking = originalAssistantWasSpeaking,
            Source = "live_utterance_monitor",
            Confidence = confidence
        };
        var resolution = _activeSpokenTurnResolver?.Resolve(context, provisionalUtterance);
        if (resolution is { IsActiveAnswerTurn: true })
        {
            activeTurnId = resolution.ActiveTurnId;
            correlationId = resolution.CorrelationId;
            state = LiveAssistantTurnState.Speaking;
            playbackActive = true;
            _logger.LogInformation(
                "conversational_interruption_turn_binding_resolved yieldedObservedTurnId: {YieldedObservedTurnId}. resolvedActiveTurnId: {ResolvedActiveTurnId}. turnBindingSource: {TurnBindingSource}. activePlaybackCorrelationId: {ActivePlaybackCorrelationId}. activePlaybackSpeechType: {ActivePlaybackSpeechType}. provisionalAudioHoldId: {ProvisionalAudioHoldId}. wasHeldByProvisionalAudioHold: {WasHeldByProvisionalAudioHold}. recentlyYieldedSnapshotFound: {RecentlyYieldedSnapshotFound}. recentlyYieldedSnapshotAgeMs: {RecentlyYieldedSnapshotAgeMs}. assistantWasSpeakingOriginal: {AssistantWasSpeakingOriginal}. assistantWasSpeakingResolved: {AssistantWasSpeakingResolved}. Reason: {Reason}.",
                resolution.OriginalObservedTurnId,
                resolution.ActiveTurnId,
                resolution.Source,
                resolution.ActivePlaybackCorrelationId,
                resolution.ActivePlaybackSpeechType,
                resolution.ProvisionalAudioHoldId,
                resolution.WasHeldByProvisionalAudioHold,
                resolution.RecentlyYieldedSnapshotFound,
                resolution.RecentlyYieldedSnapshotAgeMs,
                originalAssistantWasSpeaking,
                true,
                resolution.Reason);
        }

        return new UserUtterance
        {
            CaptureId = captureId,
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

    private static bool IsLiveMonitorContext(BargeInSpeechContext context)
    {
        return string.Equals(context.AssistantTurnId, "live-utterance-monitor", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(context.CorrelationId);
    }

    private LiveUtteranceGateInput CreateLiveUtteranceGateInput(UserUtterance utterance, double vadConfidence)
    {
        LiveAssistantTurn? activeTurn = null;
        if (!string.IsNullOrWhiteSpace(utterance.CorrelationId)
            && _liveTurnService.TryGetActiveTurn(utterance.CorrelationId, out var turn))
        {
            activeTurn = turn;
        }
        else if (_liveTurnService.TryGetCurrentActiveTurn(out var currentTurn))
        {
            activeTurn = currentTurn;
        }

        return new LiveUtteranceGateInput
        {
            Utterance = utterance,
            ActiveTurn = activeTurn,
            CurrentSystemState = activeTurn?.State.ToString() ?? utterance.StateWhenCaptured.ToString(),
            AssistantWasSpeaking = utterance.AssistantWasSpeaking,
            IsIdleListening = activeTurn is null && utterance.StateWhenCaptured is LiveAssistantTurnState.IdleListening,
            PendingCommandDescription = activeTurn?.PendingCommandDescription,
            SttConfidence = utterance.Confidence,
            AudioSpeechConfidence = vadConfidence,
            ActiveSurface = _activeSurfaceService?.Current
        };
    }

    private async Task<bool> TryConsumePendingInterruptionClarificationAsync(
        BargeInSpeechContext context,
        UserUtterance utterance,
        CancellationToken cancellationToken)
    {
        if (_pendingInterruptionClarifications is null)
        {
            return false;
        }

        var response = _pendingInterruptionClarifications.TryConsumeResponse(
            utterance.Text,
            utterance.CaptureId,
            utterance.CorrelationId);
        if (response is null)
        {
            return false;
        }

        await EmitAssistantUiStateImmediateAsync(
            AssistantUiStateEvent.Create(
                "thinking",
                "pending_interruption_clarification_response_captured",
                utterance.CorrelationId,
                response.Pending.ActiveTurnId,
                interruptionState: AssistantUiStateEvent.InterruptionStateHandling),
            nameof(BargeInCoordinator),
            cancellationToken);

        var routeDecision = Decision(
            UtteranceRouteKind.AddToActiveTurn,
            0.97,
            "Matched pending interruption clarification response.",
            "PendingInterruptionClarificationResponse");
        _logger.LogInformation(
            "PendingInterruptionClarificationResponseCaptured ClarificationId: {ClarificationId}. OriginalTurnId: {OriginalTurnId}. OriginalCorrelationId: {OriginalCorrelationId}. ResponseCaptureId: {ResponseCaptureId}. ResponseCorrelationId: {ResponseCorrelationId}. ResponseLength: {ResponseLength}.",
            response.Pending.ClarificationId,
            response.Pending.ActiveTurnId,
            response.Pending.CorrelationId,
            response.CaptureId,
            response.CorrelationId,
            response.ResponseText.Length);

        if (_liveInterruptionIntegrationService is not null)
        {
            var outcome = await _liveInterruptionIntegrationService.TryHandlePendingClarificationResponseAsync(
                response,
                cancellationToken);
            if (outcome is not null)
            {
                _logger.LogInformation(
                    "PendingInterruptionClarificationResponseOwnerResult ClarificationId: {ClarificationId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. WasHandled: {WasHandled}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. ShouldCancelPlayback: {ShouldCancelPlayback}. ResultType: {ResultType}. Reason: {Reason}.",
                    response.Pending.ClarificationId,
                    response.Pending.ActiveTurnId,
                    response.Pending.CorrelationId,
                    outcome.WasHandledByConversationalInterruption,
                    outcome.AllowLegacyCleanup,
                    outcome.AllowLegacySemanticRouting,
                    outcome.ShouldCancelPlayback,
                    outcome.Result?.Type,
                    outcome.Reason);
                if (outcome.WasHandledByConversationalInterruption || !outcome.AllowLegacySemanticRouting)
                {
                    return true;
                }
            }
        }

        await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
        return true;
    }

    private async Task<bool> TryHandleConversationalInterruptionLiveSeamAsync(
        BargeInSpeechContext context,
        UserUtterance utterance,
        UtteranceRouteDecision routeDecision,
        LiveUtteranceGateResult? gateResult,
        VadFrameResult vad,
        CaptureKind captureKind,
        IReadOnlyList<BargeInAudioFrame> capturedFrames,
        DateTimeOffset capturedUntil,
        CancellationToken cancellationToken)
    {
        if (_liveInterruptionIntegrationService is null)
        {
            return true;
        }

        try
        {
            var startedAtUtc = capturedFrames.Count > 0
                ? capturedFrames[0].Timestamp
                : utterance.TimestampUtc;
            var endedAtUtc = capturedUntil == default
                ? utterance.TimestampUtc
                : capturedUntil;
            var turnResolution = _activeSpokenTurnResolver?.Resolve(context, utterance);
            var resolvedActiveTurnId = turnResolution?.ActiveTurnId;
            var resolvedCorrelationId = turnResolution?.CorrelationId;
            var originalObservedTurnId = turnResolution?.OriginalObservedTurnId
                ?? utterance.ActiveTurnId
                ?? context.AssistantTurnId;
            var activeTurnId = !string.IsNullOrWhiteSpace(resolvedActiveTurnId)
                ? resolvedActiveTurnId
                : utterance.ActiveTurnId ?? context.AssistantTurnId;
            var correlationId = !string.IsNullOrWhiteSpace(resolvedCorrelationId)
                ? resolvedCorrelationId
                : utterance.CorrelationId ?? context.CorrelationId ?? string.Empty;
            // ConversationalInterruption starts after the existing yield/capture pipeline.
            // The acoustic/Jarno decision remains owned by BargeIn/SpeechPresence.
            // This hook only shadows conversational meaning of the yielded utterance.
            var yieldedUtterance = new YieldedInterruptionUtterance
            {
                CaptureId = utterance.CaptureId,
                Transcript = utterance.Text,
                YieldedByLayer1 = true,
                YieldReason = routeDecision.Reason,
                CaptureKind = captureKind.ToString(),
                RouteKind = routeDecision.Kind.ToString(),
                RouteAction = routeDecision.Action,
                CorrelationId = correlationId,
                ActiveTurnId = activeTurnId,
                OriginalObservedTurnId = originalObservedTurnId,
                TurnBindingSource = turnResolution?.Source ?? "observed_context",
                ActivePlaybackCorrelationId = turnResolution?.ActivePlaybackCorrelationId,
                ActivePlaybackSpeechType = turnResolution?.ActivePlaybackSpeechType,
                ProvisionalAudioHoldId = turnResolution?.ProvisionalAudioHoldId,
                WasHeldByProvisionalAudioHold = turnResolution?.WasHeldByProvisionalAudioHold == true,
                AssistantWasSpeakingOriginal = utterance.AssistantWasSpeaking,
                AssistantWasSpeakingResolved = turnResolution?.IsActiveAnswerTurn == true || utterance.AssistantWasSpeaking,
                RecentlyYieldedSnapshotFound = turnResolution?.RecentlyYieldedSnapshotFound == true,
                RecentlyYieldedSnapshotAgeMs = turnResolution?.RecentlyYieldedSnapshotAgeMs,
                Layer1Confidence = utterance.Confidence ?? vad.Confidence,
                Layer1Decision = gateResult?.Decision.ToString() ?? routeDecision.Action,
                CurrentAssistantSentence = null,
                LastCompletedAssistantSentence = null,
                OriginalUserQuestion = null,
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc
            };

            var outcome = await _liveInterruptionIntegrationService.TryHandleYieldedInterruptionAsync(yieldedUtterance, cancellationToken);
            if (outcome is not null)
            {
                _logger.LogInformation(
                    "conversational_interruption_live_hook_result CaptureId: {CaptureId}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. WasEvaluated: {WasEvaluated}. WasHandled: {WasHandled}. AllowLegacyCleanup: {AllowLegacyCleanup}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}. ShouldCancelPlayback: {ShouldCancelPlayback}. ShouldCancelCurrentTurn: {ShouldCancelCurrentTurn}. ShouldRouteReplacementRequest: {ShouldRouteReplacementRequest}. ResultType: {ResultType}. DecisionType: {DecisionType}. Strategy: {Strategy}. Reason: {Reason}.",
                    utterance.CaptureId,
                    yieldedUtterance.ActiveTurnId,
                    yieldedUtterance.CorrelationId,
                    outcome.WasEvaluatedByConversationalInterruption,
                    outcome.WasHandledByConversationalInterruption,
                    outcome.AllowLegacyCleanup,
                    outcome.AllowLegacySemanticRouting,
                    outcome.ShouldCancelPlayback,
                    outcome.ShouldCancelCurrentTurn,
                    outcome.ShouldRouteReplacementRequest,
                    outcome.Result?.Type,
                    outcome.InterruptionType,
                    outcome.Strategy,
                    outcome.Reason);

                if (outcome.ShouldRouteReplacementRequest && !string.IsNullOrWhiteSpace(outcome.RewrittenRequest))
                {
                    await CancelByUtteranceAsync(utterance, LiveAssistantTurnCancelReason.UserCorrection, outcome.RewrittenRequest, cancellationToken);
                    await RaiseCorrectionRegenerationRequestedAsync(context, outcome.RewrittenRequest, utterance.CaptureId, cancellationToken);
                    _logger.LogInformation(
                        "conversational_interruption_replacement_routing_requested TurnId: {TurnId}. CorrelationId: {CorrelationId}. AllowLegacySemanticRouting: {AllowLegacySemanticRouting}.",
                        yieldedUtterance.ActiveTurnId,
                        yieldedUtterance.CorrelationId,
                        outcome.AllowLegacySemanticRouting);
                    return false;
                }

                if (outcome.ShouldCancelCurrentTurn)
                {
                    await CancelByUtteranceAsync(
                        utterance,
                        LiveAssistantTurnCancelReason.UserHardStop,
                        null,
                        cancellationToken,
                        clearPlaybackQueue: outcome.AllowLegacyCleanup);
                    return false;
                }

                if (outcome.ShouldCancelPlayback)
                {
                    return false;
                }

                if (!outcome.AllowLegacySemanticRouting)
                {
                    _logger.LogInformation(
                        "conversational_interruption_legacy_semantic_routing_suppressed TurnId: {TurnId}. CorrelationId: {CorrelationId}. AllowLegacyCleanup: {AllowLegacyCleanup}. ShouldResumeOrContinuePlaybackIfPossible: {ShouldResumeOrContinuePlaybackIfPossible}. Reason: {Reason}.",
                        yieldedUtterance.ActiveTurnId,
                        yieldedUtterance.CorrelationId,
                        outcome.AllowLegacyCleanup,
                        outcome.ShouldResumeOrContinuePlaybackIfPossible,
                        outcome.Reason);
                    if (outcome.AllowLegacyCleanup && outcome.ShouldResumeOrContinuePlaybackIfPossible)
                    {
                        await ResumePreviousSpeechAsync(context, outcome.Reason, cancellationToken);
                    }

                    return false;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "conversational_interruption_live_hook_failed");
        }

        return true;
    }

    private static bool IsDecisiveGateDecision(LiveUtteranceGateResult gateResult, UserUtterance utterance)
    {
        return gateResult.Decision switch
        {
            LiveUtteranceGateDecisionKind.AskClarification
                or LiveUtteranceGateDecisionKind.HoldForMoreSpeech
                or LiveUtteranceGateDecisionKind.AcceptPlaybackControl
                or LiveUtteranceGateDecisionKind.AcceptCancellation
                or LiveUtteranceGateDecisionKind.AcceptReplacement
                or LiveUtteranceGateDecisionKind.AcceptCorrection
                or LiveUtteranceGateDecisionKind.AcceptContinuation
                or LiveUtteranceGateDecisionKind.AcceptStatusQuestion => true,
            LiveUtteranceGateDecisionKind.AcceptNewRequest => utterance.AssistantWasSpeaking
                || !string.IsNullOrWhiteSpace(utterance.ActiveTurnId)
                || utterance.StateWhenCaptured is not LiveAssistantTurnState.IdleListening,
            _ => false
        };
    }

    private async Task ApplyDecisiveGateDecisionAsync(
        BargeInSpeechContext context,
        UserUtterance utterance,
        UtteranceRouteDecision routeDecision,
        LiveUtteranceGateResult gateResult,
        CancellationToken cancellationToken)
    {
        switch (gateResult.Decision)
        {
            case LiveUtteranceGateDecisionKind.AcceptPlaybackControl:
                if (IsHardStopGatePhrase(gateResult.NormalizedText))
                {
                    var stopWakeOptions = _options.CurrentValue;
                    if (ShouldRejectStopCommandMissingWakePrefix(utterance.AssistantWasSpeaking, gateResult.NormalizedText, stopWakeOptions, out var rejectionReason))
                    {
                        LogStopCommandWakePrefixCheck(utterance.AssistantWasSpeaking, gateResult.NormalizedText, stopWakeOptions, "rejected_missing_wake_prefix", rejectionReason);
                        _logger.LogInformation(
                            "StopCommandRejectedMissingWakePrefix. NormalizedTranscript: {NormalizedTranscript}. WakePrefix: {WakePrefix}. AssistantWasSpeaking: {AssistantWasSpeaking}. DecisionReason: {DecisionReason}.",
                            gateResult.NormalizedText,
                            InterruptionClassifier.Normalize(stopWakeOptions.StopWakePrefix),
                            utterance.AssistantWasSpeaking,
                            rejectionReason);
                        _diagnostics.Ignored(context, rejectionReason);
                        await ResumePreviousSpeechAsync(context, rejectionReason, cancellationToken);
                        return;
                    }

                    if (stopWakeOptions.RequireWakePrefixForStopDuringPlayback && utterance.AssistantWasSpeaking)
                    {
                        LogStopCommandWakePrefixCheck(utterance.AssistantWasSpeaking, gateResult.NormalizedText, stopWakeOptions, "accepted_with_wake_prefix", "Stop command met wake-prefix policy.");
                        _logger.LogInformation(
                            "StopCommandAcceptedWithWakePrefix. NormalizedTranscript: {NormalizedTranscript}. WakePrefix: {WakePrefix}. AssistantWasSpeaking: {AssistantWasSpeaking}. DecisionReason: {DecisionReason}.",
                            gateResult.NormalizedText,
                            InterruptionClassifier.Normalize(stopWakeOptions.StopWakePrefix),
                            utterance.AssistantWasSpeaking,
                            "Stop command accepted as playback hard stop.");
                    }

                    if (string.IsNullOrWhiteSpace(utterance.CorrelationId))
                    {
                        await _playbackService.ClearQueueAsync(cancellationToken);
                    }
                    else
                    {
                        await CancelByUtteranceAsync(utterance, LiveAssistantTurnCancelReason.UserHardStop, null, cancellationToken);
                    }

                    await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(utterance.CorrelationId))
                {
                    _liveTurnService.UpdateTurnState(utterance.CorrelationId, LiveAssistantTurnState.PausedByUser);
                }

                if (utterance.AssistantWasSpeaking)
                {
                    await _playbackService.ClearQueueAsync(cancellationToken);
                }

                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return;

            case LiveUtteranceGateDecisionKind.AskClarification:
            case LiveUtteranceGateDecisionKind.HoldForMoreSpeech:
                if (!string.IsNullOrWhiteSpace(utterance.CorrelationId))
                {
                    _liveTurnService.UpdateTurnState(utterance.CorrelationId, LiveAssistantTurnState.PausedByUser);
                }

                if (utterance.AssistantWasSpeaking)
                {
                    await _playbackService.ClearQueueAsync(cancellationToken);
                }

                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return;

            case LiveUtteranceGateDecisionKind.AcceptCancellation:
                if (string.IsNullOrWhiteSpace(utterance.CorrelationId))
                {
                    await _playbackService.ClearQueueAsync(cancellationToken);
                }
                else
                {
                    await CancelByUtteranceAsync(utterance, LiveAssistantTurnCancelReason.UserHardStop, null, cancellationToken);
                }

                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return;

            case LiveUtteranceGateDecisionKind.AcceptReplacement:
            case LiveUtteranceGateDecisionKind.AcceptCorrection:
                await CancelByUtteranceAsync(utterance, LiveAssistantTurnCancelReason.UserCorrection, routeDecision.ReplacementText, cancellationToken);
                if (!string.IsNullOrWhiteSpace(routeDecision.ReplacementText))
                {
                    await RaiseCorrectionRegenerationRequestedAsync(context, routeDecision.ReplacementText, utterance.CaptureId, cancellationToken);
                }

                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return;

            case LiveUtteranceGateDecisionKind.AcceptNewRequest:
                await RaiseBackendVoiceRequestCapturedAsync(context, utterance, cancellationToken);
                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return;

            default:
                await RaiseLiveUserUtteranceRoutedAsync(utterance, routeDecision, cancellationToken);
                return;
        }
    }

    private static bool IsHardStopGatePhrase(string? normalizedText)
    {
        return !string.IsNullOrWhiteSpace(normalizedText)
            && (ContainsWholePhrase(normalizedText, "stop")
                || ContainsWholePhrase(normalizedText, "shut up")
                || ContainsWholePhrase(normalizedText, "be quiet")
                || ContainsWholePhrase(normalizedText, "quiet"));
    }

    private static bool ShouldRejectStopCommandMissingWakePrefix(
        bool assistantWasSpeaking,
        string? normalizedTranscript,
        BargeInOptions options,
        out string reason)
    {
        reason = string.Empty;
        if (!assistantWasSpeaking || !options.RequireWakePrefixForStopDuringPlayback)
        {
            return false;
        }

        if (!IsHardStopGatePhrase(normalizedTranscript))
        {
            return false;
        }

        if (HasStopWakePrefix(normalizedTranscript, options))
        {
            return false;
        }

        reason = $"Stop command rejected during assistant playback because wake prefix '{InterruptionClassifier.Normalize(options.StopWakePrefix)}' was missing.";
        return true;
    }

    private void LogStopCommandWakePrefixCheck(
        bool assistantWasSpeaking,
        string? normalizedTranscript,
        BargeInOptions options,
        string decision,
        string reason)
    {
        if (!options.RequireWakePrefixForStopDuringPlayback || !assistantWasSpeaking || !IsHardStopGatePhrase(normalizedTranscript))
        {
            return;
        }

        _logger.LogInformation(
            "StopCommandWakePrefixCheck. NormalizedTranscript: {NormalizedTranscript}. WakePrefix: {WakePrefix}. AssistantWasSpeaking: {AssistantWasSpeaking}. Decision: {Decision}. DecisionReason: {DecisionReason}.",
            normalizedTranscript ?? string.Empty,
            InterruptionClassifier.Normalize(options.StopWakePrefix),
            assistantWasSpeaking,
            decision,
            reason);
    }

    private static bool HasStopWakePrefix(string? normalizedTranscript, BargeInOptions options)
    {
        if (string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            return false;
        }

        var wakePrefix = InterruptionClassifier.Normalize(options.StopWakePrefix);
        if (string.IsNullOrWhiteSpace(wakePrefix))
        {
            return false;
        }

        return string.Equals(normalizedTranscript, wakePrefix, StringComparison.OrdinalIgnoreCase)
            || normalizedTranscript.StartsWith($"{wakePrefix} ", StringComparison.OrdinalIgnoreCase)
            || normalizedTranscript.StartsWith($"hey {wakePrefix} ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsWholePhrase(string text, string phrase)
    {
        return Regex.IsMatch(text, $@"(^|\s){Regex.Escape(phrase)}(\s|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

        if (IsIncompleteCorrectionPrefix(normalized))
        {
            return Decision(UtteranceRouteKind.BackgroundOrNoOp, 0.83, "Incomplete correction prefix; hold for more speech.", "HoldForMoreSpeech");
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
                    await RaiseCorrectionRegenerationRequestedAsync(context, routeDecision.ReplacementText, utterance.CaptureId, cancellationToken);
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
        CancellationToken cancellationToken,
        bool clearPlaybackQueue = true)
    {
        if (string.IsNullOrWhiteSpace(utterance.CorrelationId))
        {
            return;
        }

        if (clearPlaybackQueue)
        {
            await _playbackService.ClearQueueAsync(cancellationToken);
        }

        await _liveTurnService.CancelTurnAsync(utterance.CorrelationId, reason, correctionText, cancellationToken);
        _logger.LogInformation(
            reason is LiveAssistantTurnCancelReason.UserCorrection ? "ActiveTurnSuperseded. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. CorrectionText: {CorrectionText}. ClearPlaybackQueue: {ClearPlaybackQueue}." : "ActiveTurnCancelled. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. CorrectionText: {CorrectionText}. ClearPlaybackQueue: {ClearPlaybackQueue}.",
            utterance.ActiveTurnId,
            utterance.CorrelationId,
            correctionText,
            clearPlaybackQueue);
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
        if (IsIncompleteCorrectionPrefix(normalized))
        {
            return false;
        }

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

    private static bool IsIncompleteCorrectionPrefix(string normalized)
    {
        var prefixes = new[]
        {
            "what i meant was",
            "what i meant is",
            "what i meant",
            "what i mean is",
            "what i mean",
            "i meant",
            "i mean"
        };
        var intros = new[] { string.Empty, "no ", "actually ", "wait ", "sorry " };

        return intros.Any(intro => prefixes.Any(prefix =>
            string.Equals(normalized, $"{intro}{prefix}".Trim(), StringComparison.Ordinal)));
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

    private SpeechPresenceOfficialDecision? EvaluateOfficialSpeechPresence(
        long frameId,
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult? correlation)
    {
        if (_speechPresenceDetector is null)
        {
            return null;
        }

        var result = _speechPresenceDetector.Evaluate(CreateSpeechPresenceEvidence(
            frameId,
            rawFrame,
            echoReducedFrame,
            playbackReference,
            vad,
            gateResult,
            correlation,
            "official_frame_decision"));
        var decision = new SpeechPresenceOfficialDecision
        {
            FrameId = frameId,
            TimestampUtc = rawFrame.Timestamp,
            Result = result
        };
        _speechPresenceDecisionLogSink?.TryLogOfficialDecision(decision);
        return decision;
    }

    private Task HandleFloorYieldAsync(
        SpeechPresenceOfficialDecision? decision,
        CancellationToken cancellationToken)
    {
        return _floorYieldController is null
            ? Task.CompletedTask
            : _floorYieldController.HandleOfficialDecisionAsync(decision, cancellationToken);
    }

    private SpeechPresenceBranchObservation? ObserveSpeechPresenceBranch(
        long frameId,
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult? correlation,
        string sourcePath)
    {
        if (_speechPresenceDetector is null)
        {
            return null;
        }

        var result = _speechPresenceDetector.Evaluate(CreateSpeechPresenceEvidence(
            frameId,
            rawFrame,
            echoReducedFrame,
            playbackReference,
            vad,
            gateResult,
            correlation,
            sourcePath));
        var observation = new SpeechPresenceBranchObservation
        {
            FrameId = frameId,
            TimestampUtc = rawFrame.Timestamp,
            SourcePath = sourcePath,
            Result = result
        };
        _speechPresenceDecisionLogSink?.TryLogBranchObservation(observation);
        return observation;
    }

    private SpeechPresenceEvidence CreateSpeechPresenceEvidence(
        long frameId,
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult? correlation,
        string sourcePath)
    {
        var correlationScore = gateResult?.CorrelationScore ?? correlation?.CorrelationScore;
        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        return new SpeechPresenceEvidence
        {
            FrameId = frameId,
            TimestampUtc = rawFrame.Timestamp,
            AssistantPlaybackActive = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot),
            RawMicRms = CalculateRms(rawFrame.Samples.Span),
            RawMicPeak = CalculatePeak(rawFrame.Samples.Span),
            EchoReducedRms = CalculateRms(echoReducedFrame.Samples.Span),
            EchoReducedPeak = CalculatePeak(echoReducedFrame.Samples.Span),
            PlaybackReferenceRms = playbackReference.IsEmpty ? 0.0 : CalculateRms(playbackReference.Span),
            PlaybackReferencePeak = playbackReference.IsEmpty ? 0.0 : CalculatePeak(playbackReference.Span),
            VadConfidence = vad.Confidence,
            VadSpeechDetected = vad.IsSpeech,
            PlaybackCorrelationScore = correlationScore,
            StrongSelfEchoEvidence = IsStrongSelfEchoEvidence(gateResult, correlation),
            UserSpeechScoreLegacy = gateResult?.UserSpeechScore,
            SourcePath = sourcePath
        };
    }

    private static bool IsStrongSelfEchoEvidence(
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult? correlation)
    {
        if (gateResult is not null && IsStrongSelfEchoSuppression(gateResult))
        {
            return true;
        }

        return string.Equals(
            correlation?.Decision,
            SelfSpeechCorrelationDecision.SelfEcho,
            StringComparison.Ordinal);
    }

    private void PublishDebugSnapshot(
        BargeInSpeechContext context,
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult? vad,
        SelfSpeechGateResult? gateResult,
        SelfSpeechCorrelationResult? correlation,
        string captureSource,
        string bargeInState,
        string? capturedWindowSelfPlaybackDecision = null,
        double? capturedWindowBestCorrelation = null,
        string? sttAudioSource = null,
        bool? sttAudioIsAecProcessed = null,
        string? finalBargeInDecision = null,
        bool force = false,
        SpeechPresenceResult? speechPresence = null)
    {
        if (_debugSnapshots?.IsEnabled != true)
        {
            return;
        }

        var micRms = CalculateRms(rawFrame.Samples.Span);
        var micPeak = CalculatePeak(rawFrame.Samples.Span);
        var playbackReferenceRms = playbackReference.IsEmpty
            ? (double?)null
            : CalculateRms(playbackReference.Span);
        var playbackEnergy = Math.Max(_assistantPlaybackMonitor.CurrentPlaybackEnergy, _assistantPlaybackMonitor.RecentPlaybackEnergy);
        var estimatedEchoRms = gateResult?.EstimatedEchoEnergy;
        var micToExpectedEchoRatio = estimatedEchoRms is > 0.000001
            ? micRms / estimatedEchoRms.Value
            : (double?)null;
        var userDominanceScore = gateResult?.UserSpeechScore;
        var correlationScore = gateResult?.CorrelationScore ?? correlation?.CorrelationScore;
        var bestDelayMs = gateResult?.BestDelayMs ?? correlation?.BestDelayMs;
        var options = _options.CurrentValue;
        var acousticMode = SelectAcousticCaptureModeForFrame(playbackReference, options);
        BargeInCaptureSession? activeCaptureSession;
        lock (_syncRoot)
        {
            activeCaptureSession = _activeCaptureSession;
        }

        if (activeCaptureSession is not null)
        {
            acousticMode = activeCaptureSession.Mode;
        }

        var rawSpeechThreshold = Math.Max(0.0, options.CaptureContinuationRawEnergyThreshold)
            * Math.Max(0.0, options.IdleRawMicSpeechEnergyMultiplier);
        var aecSpeechThreshold = Math.Max(0.0, options.CaptureContinuationAecEnergyThreshold);
        var aecRms = CalculateRms(echoReducedFrame.Samples.Span);
        var rawSpeechActive = micRms >= rawSpeechThreshold;
        var aecSpeechActive = vad?.IsSpeech == true || aecRms >= aecSpeechThreshold;
        var idleRawMicPrimary = options.EnableIdleRawMicPrimaryEndpointing
            && acousticMode is AcousticCaptureMode.IdleUserRequest;
        var aecOnlyEnergyIgnored = idleRawMicPrimary
            && !rawSpeechActive
            && aecSpeechActive
            && micRms <= rawSpeechThreshold * Math.Max(1.0, options.IdleAecOnlyEnergyIgnoreRawMaxMultiplier);
        var playbackReferenceSnapshot = _playbackReferenceTap.GetDebugSnapshot();
        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var assistantPlaybackContextActive = IsAssistantPlaybackContextActive(activePlaybackSnapshot);
        var assistantAudioActuallyPlaying = IsAssistantAudioActuallyPlaying(activePlaybackSnapshot);
        var floorYield = _floorYieldController?.GetDebugState();

        _debugSnapshots.Publish(new BargeInDebugSnapshot
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            AssistantWasSpeaking = assistantAudioActuallyPlaying,
            AssistantPlaybackContextActive = assistantPlaybackContextActive,
            AssistantAudioActuallyPlaying = assistantAudioActuallyPlaying,
            AudiblePlaybackActive = assistantAudioActuallyPlaying,
            ActivePlaybackSnapshotIsActive = activePlaybackSnapshot?.IsActive,
            ActivePlaybackSnapshotIsHeld = activePlaybackSnapshot?.IsHeld,
            HoldId = activePlaybackSnapshot?.HoldId,
            CaptureSource = captureSource,
            BargeInState = bargeInState,
            AcousticCaptureMode = acousticMode.ToString(),
            IdleRawMicPrimary = idleRawMicPrimary,
            RawSpeechActive = rawSpeechActive,
            AecSpeechActive = aecSpeechActive,
            AecOnlyEnergyIgnored = aecOnlyEnergyIgnored,
            RawNoiseFloor = rawSpeechThreshold / 3.0,
            AecNoiseFloor = vad?.NoiseFloor,
            PlaybackReferenceAgeMs = playbackReferenceSnapshot.ReferenceNewestAgeMilliseconds,
            EndpointSilenceMs = activeCaptureSession?.GetCurrentSilenceMs(rawFrame.Timestamp),
            RequiredEndpointSilenceMs = activeCaptureSession?.RequiredEndpointSilenceMs
                ?? (acousticMode is AcousticCaptureMode.IdleUserRequest && options.EnableIdleRawMicPrimaryEndpointing
                    ? Math.Max(0, options.IdleRawMicEndpointSilenceMs)
                    : Math.Max(0, options.VadEndSilenceMs)),
            MicRms = micRms,
            MicPeak = micPeak,
            PlaybackReferenceRms = playbackReferenceRms,
            PlaybackEnergy = playbackEnergy,
            EstimatedEchoRms = estimatedEchoRms,
            MicToExpectedEchoRatio = micToExpectedEchoRatio,
            UserDominanceScore = userDominanceScore,
            SpeechPresenceState = speechPresence?.State.ToString(),
            SpeechPresenceFrameId = speechPresence?.Evidence.FrameId,
            SpeechPresenceConfidence = speechPresence?.Confidence,
            SpeechPresenceReason = speechPresence?.Reason,
            SpeechPresenceShouldYieldPlayback = speechPresence?.ShouldYieldPlayback,
            SpeechPresenceRawMicRms = speechPresence?.Evidence.RawMicRms,
            SpeechPresenceEchoReducedRms = speechPresence?.Evidence.EchoReducedRms,
            SpeechPresencePlaybackReferenceRms = speechPresence?.Evidence.PlaybackReferenceRms,
            SpeechPresenceVadConfidence = speechPresence?.Evidence.VadConfidence,
            SpeechPresenceCorrelation = speechPresence?.Evidence.PlaybackCorrelationScore,
            FloorYieldTriggered = floorYield?.Triggered,
            LastFloorYieldFrameId = floorYield?.LastFrameId,
            LastFloorYieldReason = floorYield?.LastReason,
            LastFloorYieldTimestampUtc = floorYield?.LastTimestampUtc,
            LastFloorYieldMode = floorYield?.LastMode,
            FloorYieldCandidateActive = floorYield?.CandidateActive,
            FloorYieldCandidateStartFrameId = floorYield?.CandidateStartFrameId,
            FloorYieldCandidateDurationMs = floorYield?.CandidateDurationMs,
            FloorYieldRequiredSustainedMs = floorYield?.RequiredSustainedMs,
            VadConfidence = vad?.Confidence,
            VadIsSpeech = vad?.IsSpeech,
            CorrelationScore = correlationScore,
            BestCorrelationDelayMs = bestDelayMs,
            SelfSpeechGateDecision = gateResult?.Decision.ToString(),
            SelfSpeechGateReason = gateResult?.Reason,
            CapturedWindowSelfPlaybackDecision = capturedWindowSelfPlaybackDecision,
            CapturedWindowBestCorrelation = capturedWindowBestCorrelation,
            SttAudioSource = sttAudioSource,
            SttAudioIsAecProcessed = sttAudioIsAecProcessed,
            FinalBargeInDecision = finalBargeInDecision,
            MicRmsPercent = ScaleEnergyPercent(micRms),
            MicPeakPercent = ScaleEnergyPercent(micPeak),
            PlaybackReferencePercent = playbackReferenceRms is null ? null : ScaleEnergyPercent(playbackReferenceRms.Value),
            ExpectedEchoPercent = estimatedEchoRms is null ? null : ScaleEnergyPercent(estimatedEchoRms.Value),
            VadPercent = vad?.Confidence * 100.0,
            CorrelationPercent = correlationScore * 100.0,
            UserDominancePercent = userDominanceScore is null ? null : Math.Clamp(userDominanceScore.Value, 0.0, 1.0) * 100.0
        }, force);
    }

    private static double ScaleEnergyPercent(double value)
    {
        return Math.Clamp(value / 0.10, 0.0, 1.0) * 100.0;
    }

    private static string CapturedWindowDecisionText(CapturedWindowSelfPlaybackCheckResult result)
    {
        if (result.ShouldReject)
        {
            return $"rejected_self_playback: {result.Reason}";
        }

        return result.IsAvailable
            ? $"allowed: {result.Reason}"
            : $"unavailable: {result.Reason}";
    }

    private static double CalculatePeak(ReadOnlySpan<float> samples)
    {
        var peak = 0.0;
        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        return peak;
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

    internal static AcousticCaptureMode SelectAcousticCaptureMode(
        bool assistantAudioActuallyPlaying,
        double playbackReferenceRms,
        double? playbackReferenceAgeMs,
        ActiveSpeechPlaybackSnapshot? activePlaybackSnapshot,
        BargeInOptions options)
    {
        if (assistantAudioActuallyPlaying)
        {
            return AcousticCaptureMode.AssistantInterruption;
        }

        if (activePlaybackSnapshot is { IsAudiblePlaybackActive: true })
        {
            return AcousticCaptureMode.AssistantInterruption;
        }

        if (activePlaybackSnapshot is { IsHeld: true, IsAudiblePlaybackActive: false })
        {
            return AcousticCaptureMode.IdleUserRequest;
        }

        var recentWindowMs = Math.Max(0, options.CapturedWindowSelfPlaybackRecentPlaybackMs);
        var referenceIsRecent = playbackReferenceAgeMs is not null
            && playbackReferenceAgeMs.Value >= 0
            && playbackReferenceAgeMs.Value <= recentWindowMs;
        var referenceIsNonZero = playbackReferenceRms > 0.0001;
        if (referenceIsRecent || referenceIsNonZero)
        {
            return AcousticCaptureMode.AssistantInterruption;
        }

        return AcousticCaptureMode.IdleUserRequest;
    }

    private AcousticCaptureMode SelectAcousticCaptureModeForFrame(
        ReadOnlyMemory<float> playbackReference,
        BargeInOptions options)
    {
        var playbackReferenceRms = playbackReference.IsEmpty ? 0.0 : CalculateRms(playbackReference.Span);
        var snapshot = _playbackReferenceTap.GetDebugSnapshot();
        var activePlaybackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        return SelectAcousticCaptureMode(
            IsAssistantAudioActuallyPlaying(activePlaybackSnapshot),
            playbackReferenceRms,
            snapshot.ReferenceNewestAgeMilliseconds,
            activePlaybackSnapshot,
            options);
    }

    private void ObserveCaptureFrame(
        BargeInSpeechContext context,
        BargeInCaptureSession captureSession,
        CaptureFrameObservation observation)
    {
        if (observation.AecOnlyEnergyIgnored)
        {
            _logger.LogInformation(
                "idle_aec_only_energy_ignored AcousticCaptureMode: {AcousticCaptureMode}. IdleRawMicPrimary: {IdleRawMicPrimary}. RawSpeechActive: {RawSpeechActive}. AecSpeechActive: {AecSpeechActive}. RawMicRms: {RawMicRms:N4}. EchoReducedRms: {EchoReducedRms:N4}. RawNoiseFloor: {RawNoiseFloor:N4}. AecNoiseFloor: {AecNoiseFloor:N4}. AssistantPlaybackActive: {AssistantPlaybackActive}. PlaybackReferenceRms: {PlaybackReferenceRms:N4}. PlaybackReferenceAgeMs: {PlaybackReferenceAgeMs}. EndpointSilenceMs: {EndpointSilenceMs}. RequiredEndpointSilenceMs: {RequiredEndpointSilenceMs}.",
                observation.AcousticCaptureMode,
                observation.IdleRawMicPrimary,
                observation.RawSpeechActive,
                observation.AecSpeechActive,
                observation.RawEnergy,
                observation.AecEnergy,
                observation.RawNoiseFloor,
                observation.AecNoiseFloor,
                observation.AssistantPlaybackActive,
                observation.PlaybackReferenceRms,
                observation.PlaybackReferenceAgeMs,
                captureSession.GetCurrentSilenceMs(observation.Timestamp),
                captureSession.RequiredEndpointSilenceMs);
        }

        var endpointTriggered = captureSession.Observe(observation);
        if (endpointTriggered && observation.AcousticCaptureMode is AcousticCaptureMode.IdleUserRequest)
        {
            _logger.LogInformation(
                "idle_raw_mic_endpoint_triggered AcousticCaptureMode: {AcousticCaptureMode}. IdleRawMicPrimary: {IdleRawMicPrimary}. RawSpeechActive: {RawSpeechActive}. AecSpeechActive: {AecSpeechActive}. AecOnlyEnergyIgnored: {AecOnlyEnergyIgnored}. RawMicRms: {RawMicRms:N4}. EchoReducedRms: {EchoReducedRms:N4}. RawNoiseFloor: {RawNoiseFloor:N4}. AecNoiseFloor: {AecNoiseFloor:N4}. AssistantPlaybackActive: {AssistantPlaybackActive}. PlaybackReferenceRms: {PlaybackReferenceRms:N4}. PlaybackReferenceAgeMs: {PlaybackReferenceAgeMs}. EndpointSilenceMs: {EndpointSilenceMs}. RequiredEndpointSilenceMs: {RequiredEndpointSilenceMs}.",
                observation.AcousticCaptureMode,
                observation.IdleRawMicPrimary,
                observation.RawSpeechActive,
                observation.AecSpeechActive,
                observation.AecOnlyEnergyIgnored,
                observation.RawEnergy,
                observation.AecEnergy,
                observation.RawNoiseFloor,
                observation.AecNoiseFloor,
                observation.AssistantPlaybackActive,
                observation.PlaybackReferenceRms,
                observation.PlaybackReferenceAgeMs,
                captureSession.GetCurrentSilenceMs(observation.Timestamp),
                captureSession.RequiredEndpointSilenceMs);
        }
    }

    private CaptureFrameObservation CreateCaptureFrameObservation(
        BargeInAudioFrame rawFrame,
        BargeInAudioFrame echoReducedFrame,
        ReadOnlyMemory<float> playbackReference,
        VadFrameResult vad,
        SelfSpeechGateResult? gateResult,
        AcousticCaptureMode acousticMode,
        BargeInOptions options)
    {
        var rawEnergy = CalculateRms(rawFrame.Samples.Span);
        var aecEnergy = CalculateRms(echoReducedFrame.Samples.Span);
        var comfortWouldAllow = IsFastNearEndSpeechCandidate(vad, options.FastNearEndDucking);
        var rawSpeechThreshold = Math.Max(0.0, options.CaptureContinuationRawEnergyThreshold)
            * Math.Max(0.0, options.IdleRawMicSpeechEnergyMultiplier);
        var aecSpeechThreshold = Math.Max(0.0, options.CaptureContinuationAecEnergyThreshold);
        var rawSpeechActive = rawEnergy >= rawSpeechThreshold;
        var aecSpeechActive = (options.CaptureContinuationUseVad && vad.IsSpeech)
            || comfortWouldAllow
            || aecEnergy >= aecSpeechThreshold;
        var idleRawMicPrimary = options.EnableIdleRawMicPrimaryEndpointing
            && acousticMode is AcousticCaptureMode.IdleUserRequest;
        var aecOnlyEnergyIgnored = idleRawMicPrimary
            && !rawSpeechActive
            && aecSpeechActive
            && rawEnergy <= rawSpeechThreshold * Math.Max(1.0, options.IdleAecOnlyEnergyIgnoreRawMaxMultiplier);
        var legacyCaptureIsSpeech =
            (options.CaptureContinuationUseVad && vad.IsSpeech)
            || comfortWouldAllow
            || rawSpeechActive
            || aecEnergy >= aecSpeechThreshold;
        var captureIsSpeech = idleRawMicPrimary
            ? rawSpeechActive
            : legacyCaptureIsSpeech;
        var playbackReferenceRms = playbackReference.IsEmpty ? 0.0 : CalculateRms(playbackReference.Span);
        var playbackReferenceSnapshot = _playbackReferenceTap.GetDebugSnapshot();

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
            AcousticCaptureMode = acousticMode,
            IdleRawMicPrimary = idleRawMicPrimary,
            RawSpeechActive = rawSpeechActive,
            AecSpeechActive = aecSpeechActive,
            AecOnlyEnergyIgnored = aecOnlyEnergyIgnored,
            RawNoiseFloor = rawSpeechThreshold / 3.0,
            AecNoiseFloor = vad.NoiseFloor,
            AssistantPlaybackActive = IsAssistantAudioActuallyPlaying(_playbackService.GetActivePlaybackSnapshot()),
            PlaybackReferenceRms = playbackReferenceRms,
            PlaybackReferenceAgeMs = playbackReferenceSnapshot.ReferenceNewestAgeMilliseconds,
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
            RawSpeechEnergyThreshold = rawSpeechThreshold,
            AecSpeechEnergyThreshold = aecSpeechThreshold
        };
    }

    private sealed class VoiceCaptureTimeline
    {
        public required string CaptureId { get; init; }
        public string? TurnId { get; set; }
        public string? CorrelationId { get; set; }
        public string? Source { get; set; }
        public string? AcousticCaptureMode { get; set; }
        public string? SpeechType { get; set; }
        public string? ItemType { get; set; }
        public string? HoldId { get; set; }
        public bool AssistantPlaybackContextActive { get; set; }
        public bool AssistantAudioActuallyPlaying { get; set; }
        public double? PlaybackReferenceRms { get; set; }
        public double? PlaybackReferenceAgeMs { get; set; }
        public DateTimeOffset? CaptureStartedUtc { get; set; }
        public DateTimeOffset? CaptureEndedUtc { get; set; }
        public DateTimeOffset? SttStartedUtc { get; set; }
        public DateTimeOffset? SttCompletedUtc { get; set; }
        public DateTimeOffset? RoutedUtc { get; set; }
        public double? CaptureWindowMs { get; set; }
        public double? AudioSentToSttMs { get; set; }
        public double? PostRawSpeechTailMs { get; set; }
        public double? EndpointSilenceMs { get; set; }
        public double? RequiredEndpointSilenceMs { get; set; }
        public double? SttLatencyMs { get; set; }
        public double? TotalCaptureToRouteMs { get; set; }
        public string? CaptureKind { get; set; }
        public string? EndpointReason { get; set; }
        public string? SuppressionReason { get; set; }
        public double? BurstCandidateMs { get; set; }
        public int? BurstAllowFrames { get; set; }
        public int? BurstUncertainFrames { get; set; }
        public int? BurstSuppressFrames { get; set; }
        public string? Transcript { get; set; }
        public int? TranscriptChars { get; set; }
        public bool TranscriptLooksIncomplete { get; set; }
        public bool EndsWithCorrectionPrefix { get; set; }
        public string? LiveGateDecision { get; set; }
        public string? LiveGateRoute { get; set; }
        public string? RouteAction { get; set; }
        public string? ReplacementText { get; set; }
        public string? CommandRouterNormalizedText { get; set; }
        public string? Intent { get; set; }
        public string? ToolName { get; set; }
        public string? ToolResult { get; set; }
        public string? SpokenResponse { get; set; }

        public void MarkRouted(
            UserUtterance utterance,
            LiveUtteranceGateResult? gateResult,
            UtteranceRouteDecision routeDecision)
        {
            TurnId = utterance.ActiveTurnId ?? TurnId;
            CorrelationId = utterance.CorrelationId ?? CorrelationId;
            RoutedUtc = DateTimeOffset.UtcNow;
            LiveGateDecision = gateResult?.Decision.ToString();
            LiveGateRoute = routeDecision.Kind.ToString();
            RouteAction = routeDecision.Action;
            ReplacementText = routeDecision.ReplacementText;
            CommandRouterNormalizedText = routeDecision.Kind is UtteranceRouteKind.Unknown
                ? utterance.Text
                : null;
        }

        public void Suppress(
            string suppressionReason,
            string detail,
            int audioMs,
            BargeInCaptureSession captureSession)
        {
            SuppressionReason = $"{suppressionReason}: {detail}";
            EndpointReason = captureSession.EndReason;
            AudioSentToSttMs = audioMs;
            ToolResult = "suppressed";
        }
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
        private readonly AcousticCaptureMode _mode;
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

        private BargeInCaptureSession(
            DateTimeOffset triggerTimestamp,
            int maxCaptureMs,
            int endSilenceMs,
            AcousticCaptureMode mode)
        {
            CaptureId = CreateCaptureId();
            _triggerTimestamp = triggerTimestamp;
            _lastSpeechAt = triggerTimestamp;
            _lastFrameAt = triggerTimestamp;
            _endSilenceMs = Math.Max(0, endSilenceMs);
            _mode = mode;
            _maxCaptureUntil = triggerTimestamp + TimeSpan.FromMilliseconds(maxCaptureMs);
            if (maxCaptureMs <= 0)
            {
                _endReason = "max_duration";
                _endpoint.TrySetResult(triggerTimestamp);
            }
        }

        public string CaptureId { get; }

        public static BargeInCaptureSession CreateNormal(
            DateTimeOffset triggerTimestamp,
            BargeInOptions options,
            AcousticCaptureMode mode)
        {
            var endSilenceMs = mode is AcousticCaptureMode.IdleUserRequest
                && options.EnableIdleRawMicPrimaryEndpointing
                    ? options.IdleRawMicEndpointSilenceMs
                    : options.VadEndSilenceMs;
            return new BargeInCaptureSession(
                triggerTimestamp,
                BargeInCaptureTiming.GetMaxCaptureMs(options),
                endSilenceMs,
                mode);
        }

        public static BargeInCaptureSession CreateFastHardStop(DateTimeOffset triggerTimestamp, BargeInOptions options)
        {
            return new BargeInCaptureSession(
                triggerTimestamp,
                Math.Max(1, options.FastHardStopCaptureWindowMs),
                options.FastHardStopPostSpeechPaddingMs,
                AcousticCaptureMode.AssistantInterruption);
        }

        public AcousticCaptureMode Mode => _mode;

        public int RequiredEndpointSilenceMs => _endSilenceMs;

        public int GetCurrentSilenceMs(DateTimeOffset timestamp)
        {
            lock (_sessionSync)
            {
                return Math.Max(0, (int)Math.Round((timestamp - _lastSpeechAt).TotalMilliseconds));
            }
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

        public double CaptureStartedToLastFrameMs
        {
            get
            {
                lock (_sessionSync)
                {
                    return Math.Max(0, (_lastFrameAt - _triggerTimestamp).TotalMilliseconds);
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

        public bool Observe(CaptureFrameObservation observation)
        {
            if (_endpoint.Task.IsCompleted)
            {
                return false;
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
                    EndpointSilenceMs = silenceMs,
                    AcousticCaptureMode = observation.AcousticCaptureMode.ToString(),
                    IdleRawMicPrimary = observation.IdleRawMicPrimary,
                    RawSpeechActive = observation.RawSpeechActive,
                    AecSpeechActive = observation.AecSpeechActive,
                    AecOnlyEnergyIgnored = observation.AecOnlyEnergyIgnored,
                    RawNoiseFloor = observation.RawNoiseFloor,
                    AecNoiseFloor = observation.AecNoiseFloor,
                    AssistantPlaybackActive = observation.AssistantPlaybackActive,
                    PlaybackReferenceRms = observation.PlaybackReferenceRms,
                    PlaybackReferenceAgeMs = observation.PlaybackReferenceAgeMs,
                    RequiredEndpointSilenceMs = _endSilenceMs
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
                return true;
            }

            return false;
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

        private static string CreateCaptureId()
        {
            return $"capture:{Guid.NewGuid():N}"[..40];
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

    private sealed record CapturedWindowSelfPlaybackCheckResult
    {
        public required bool IsAvailable { get; init; }

        public required bool ShouldReject { get; init; }

        public required string Reason { get; init; }

        public double CaptureEnergy { get; init; }

        public double BestCaptureEnergy { get; init; }

        public double BestReferenceEnergy { get; init; }

        public double BestCorrelationScore { get; init; }

        public double? BestDelayMs { get; init; }

        public double? BestSliceOffsetMs { get; init; }

        public int SliceCount { get; init; }

        public int AvailableSliceCount { get; init; }

        public int SamplesChecked { get; init; }

        public double? PlaybackReferenceNewestAgeMs { get; init; }

        public double? PlaybackReferenceOldestAgeMs { get; init; }

        public static CapturedWindowSelfPlaybackCheckResult Unavailable(
            string reason,
            PlaybackReferenceDebugSnapshot snapshot)
        {
            return new CapturedWindowSelfPlaybackCheckResult
            {
                IsAvailable = false,
                ShouldReject = false,
                Reason = reason,
                PlaybackReferenceNewestAgeMs = snapshot.ReferenceNewestAgeMilliseconds,
                PlaybackReferenceOldestAgeMs = snapshot.ReferenceOldestAgeMilliseconds
            };
        }
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

        public required AcousticCaptureMode AcousticCaptureMode { get; init; }

        public required bool IdleRawMicPrimary { get; init; }

        public required bool RawSpeechActive { get; init; }

        public required bool AecSpeechActive { get; init; }

        public required bool AecOnlyEnergyIgnored { get; init; }

        public required double RawNoiseFloor { get; init; }

        public required double AecNoiseFloor { get; init; }

        public required bool AssistantPlaybackActive { get; init; }

        public required double PlaybackReferenceRms { get; init; }

        public required double? PlaybackReferenceAgeMs { get; init; }

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

    private Task EmitAssistantUiStateImmediateAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        ObserveInterruptionHandlingState(uiState, source);

        return _assistantUiStateBroadcaster?.EmitImmediateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    private Task EmitAssistantUiStateCoalescedAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        ObserveInterruptionHandlingState(uiState, source);

        return _assistantUiStateBroadcaster?.RequestCoalescedStateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    internal Task EmitAssistantUiStateForDiagnosticsAsync(
        AssistantUiStateEvent uiState,
        CancellationToken cancellationToken = default)
    {
        return EmitAssistantUiStateImmediateAsync(uiState, nameof(BargeInCoordinator), cancellationToken);
    }

    private void ObserveInterruptionHandlingState(AssistantUiStateEvent uiState, string source)
    {
        if (_assistantUiStateBroadcaster is null)
        {
            return;
        }

        if (string.Equals(uiState.InterruptionState, AssistantUiStateEvent.InterruptionStateHandling, StringComparison.Ordinal))
        {
            StartInterruptionHandlingWatchdog(uiState, source);
            return;
        }

        if (string.Equals(uiState.InterruptionState, AssistantUiStateEvent.InterruptionStateNone, StringComparison.Ordinal)
            || string.Equals(uiState.InterruptionState, AssistantUiStateEvent.InterruptionStateAwaitingClarification, StringComparison.Ordinal)
            || string.Equals(uiState.InterruptionState, AssistantUiStateEvent.InterruptionStateHeldForUserSpeech, StringComparison.Ordinal))
        {
            ClearInterruptionHandlingWatchdog();
        }
    }

    private void StartInterruptionHandlingWatchdog(AssistantUiStateEvent uiState, string source)
    {
        var options = _options.CurrentValue;
        if (!options.EnableInterruptionHandlingWatchdog || options.InterruptionHandlingWatchdogTimeoutMs <= 0)
        {
            return;
        }

        long generation;
        lock (_syncRoot)
        {
            generation = ++_interruptionHandlingWatchdogGeneration;
            _interruptionHandlingStateStartedAtUtc = uiState.TimestampUtc;
            _interruptionHandlingTurnId = uiState.TurnId;
            _interruptionHandlingCorrelationId = uiState.CorrelationId;
            _interruptionHandlingReason = uiState.Reason;
        }

        _logger.LogInformation(
            "InterruptionHandlingWatchdogStarted Generation: {Generation}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}. Source: {Source}. TimeoutMs: {TimeoutMs}.",
            generation,
            uiState.TurnId,
            uiState.CorrelationId,
            uiState.Reason,
            source,
            options.InterruptionHandlingWatchdogTimeoutMs);
        ScheduleInterruptionHandlingWatchdogCheck(generation, options.InterruptionHandlingWatchdogTimeoutMs);
    }

    private void ClearInterruptionHandlingWatchdog()
    {
        lock (_syncRoot)
        {
            _interruptionHandlingWatchdogGeneration++;
            _interruptionHandlingStateStartedAtUtc = null;
            _interruptionHandlingTurnId = null;
            _interruptionHandlingCorrelationId = null;
            _interruptionHandlingReason = null;
        }
    }

    private void ScheduleInterruptionHandlingWatchdogCheck(long generation, int timeoutMs)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs)), CancellationToken.None);
                    await TryRecoverStaleInterruptionHandlingAsync(generation, timeoutMs);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "InterruptionHandlingWatchdogFailed Generation: {Generation}.", generation);
                }
            },
            CancellationToken.None);
    }

    private async Task TryRecoverStaleInterruptionHandlingAsync(long generation, int timeoutMs)
    {
        DateTimeOffset? startedAtUtc;
        string? turnId;
        string? correlationId;
        string? reason;
        bool activeCapture;
        lock (_syncRoot)
        {
            if (generation != _interruptionHandlingWatchdogGeneration
                || _interruptionHandlingStateStartedAtUtc is null)
            {
                return;
            }

            startedAtUtc = _interruptionHandlingStateStartedAtUtc;
            turnId = _interruptionHandlingTurnId;
            correlationId = _interruptionHandlingCorrelationId;
            reason = _interruptionHandlingReason;
            activeCapture = IsInterruptionCaptureActiveLocked();
        }

        var pendingClarification = !string.IsNullOrWhiteSpace(turnId)
            && _pendingInterruptionClarifications?.HasActivePendingForTurn(turnId) == true;
        var playbackSnapshot = _playbackService.GetActivePlaybackSnapshot();
        var playbackOwner = IsInterruptionHandlingPlaybackOwner(playbackSnapshot, turnId);
        if (activeCapture || pendingClarification || playbackOwner)
        {
            _logger.LogInformation(
                "InterruptionHandlingWatchdogDeferred Generation: {Generation}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}. ActiveCapture: {ActiveCapture}. PendingClarification: {PendingClarification}. PlaybackOwner: {PlaybackOwner}.",
                generation,
                turnId,
                correlationId,
                reason,
                activeCapture,
                pendingClarification,
                playbackOwner);
            ScheduleInterruptionHandlingWatchdogCheck(generation, timeoutMs);
            return;
        }

        var ageMs = startedAtUtc is null
            ? null
            : (double?)Math.Max(0, (DateTimeOffset.UtcNow - startedAtUtc.Value).TotalMilliseconds);
        lock (_syncRoot)
        {
            if (generation != _interruptionHandlingWatchdogGeneration
                || _interruptionHandlingStateStartedAtUtc != startedAtUtc)
            {
                return;
            }

            _interruptionHandlingWatchdogGeneration++;
            _interruptionHandlingStateStartedAtUtc = null;
            _interruptionHandlingTurnId = null;
            _interruptionHandlingCorrelationId = null;
            _interruptionHandlingReason = null;
        }

        _logger.LogWarning(
            "InterruptionHandlingWatchdogRecovered Generation: {Generation}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. Reason: {Reason}. AgeMs: {AgeMs}. TimeoutMs: {TimeoutMs}.",
            generation,
            turnId,
            correlationId,
            reason,
            ageMs,
            timeoutMs);
        await EmitAssistantUiStateImmediateAsync(
            AssistantUiStateEvent.Create(
                "idle",
                "stale_interruption_handling_watchdog_recovered",
                correlationId,
                turnId,
                interruptionState: AssistantUiStateEvent.InterruptionStateNone),
            nameof(BargeInCoordinator),
            CancellationToken.None);
    }

    private static bool IsInterruptionHandlingPlaybackOwner(
        ActiveSpeechPlaybackSnapshot? snapshot,
        string? turnId)
    {
        if (snapshot is null)
        {
            return false;
        }

        if (snapshot.IsHeld
            && (string.IsNullOrWhiteSpace(turnId)
                || string.Equals(snapshot.AssistantTurnId, turnId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (!snapshot.IsActive)
        {
            return false;
        }

        return string.Equals(snapshot.ItemType, SpeechPlaybackItemType.InterruptionClarification.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(snapshot.ItemType, SpeechPlaybackItemType.InterruptionContinuation.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
