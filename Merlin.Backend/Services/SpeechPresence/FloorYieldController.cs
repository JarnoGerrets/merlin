using Merlin.Backend.Models;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.SpeechPresence;

public sealed class FloorYieldController : IFloorYieldController
{
    public const string PlaybackYieldMode = "provisional_audio_hold";
    public const string HoldUnavailableYieldMode = "provisional_audio_hold_unavailable";

    private readonly IAssistantPlaybackMonitor _assistantPlaybackMonitor;
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly IRecentlyYieldedSpokenTurnStore? _recentlyYieldedTurns;
    private readonly ILogger<FloorYieldController> _logger;
    private readonly IOptionsMonitor<SpeechPresenceOptions> _options;
    private readonly object _syncRoot = new();
    private bool _yieldedCurrentPlayback;
    private long? _candidateStartFrameId;
    private DateTimeOffset? _candidateStartTimestampUtc;
    private double? _candidatePeakVadConfidence;
    private FloorYieldDebugState _debugState = new();

    public FloorYieldController(
        IAssistantPlaybackMonitor assistantPlaybackMonitor,
        IAssistantSpeechPlaybackService playbackService,
        IOptionsMonitor<SpeechPresenceOptions> options,
        ILogger<FloorYieldController> logger,
        IRecentlyYieldedSpokenTurnStore? recentlyYieldedTurns = null)
    {
        _assistantPlaybackMonitor = assistantPlaybackMonitor;
        _playbackService = playbackService;
        _options = options;
        _logger = logger;
        _recentlyYieldedTurns = recentlyYieldedTurns;
    }

    public async Task HandleOfficialDecisionAsync(
        SpeechPresenceOfficialDecision? decision,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var defaultRequiredSustainedMs = Math.Max(0, options.FloorYieldMinSustainedMs);
        if (!options.EnableFloorYield)
        {
            ResetStateWhenPlaybackInactive("playback_inactive", decision);
            return;
        }

        if (decision is null)
        {
            ResetStateWhenPlaybackInactive("playback_inactive", decision);
            return;
        }

        if (options.FloorYieldRequiresOfficialDecision
            && (!decision.IsAuthoritative
                || !string.Equals(decision.SourcePath, "official_frame_decision", StringComparison.Ordinal)))
        {
            ResetCandidate("non_authoritative_ignored", decision);
            LogIgnored("not_authoritative", decision);
            return;
        }

        if (!decision.Result.ShouldYieldPlayback
            || !decision.Result.IsUserSpeaking
            || decision.Result.State is not (SpeechPresenceState.Maybe or SpeechPresenceState.Yes))
        {
            ResetCandidate("decision_not_yield_worthy", decision);
            ResetStateWhenPlaybackInactive("playback_inactive", decision);
            return;
        }

        if (!_assistantPlaybackMonitor.IsPlaybackActive)
        {
            ResetStateWhenPlaybackInactive("playback_inactive", decision);
            LogIgnored("playback_not_active", decision);
            return;
        }

        long candidateStartFrameId;
        double candidateDurationMs;
        double candidatePeakVadConfidence;
        int requiredSustainedMs;
        lock (_syncRoot)
        {
            // 2026-07-04: Temporarily disabled for live barge-in testing. A missed or
            // misheard first yield should not block the user from interrupting again
            // during the same assistant answer.
            // if (_yieldedCurrentPlayback)
            // {
            //     ResetCandidateCore("already_yielded_current_playback", decision);
            //     return;
            // }

            if (_candidateStartTimestampUtc is null || _candidateStartFrameId is null)
            {
                _candidateStartTimestampUtc = decision.TimestampUtc;
                _candidateStartFrameId = decision.FrameId;
                _candidatePeakVadConfidence = ClampConfidence(decision.Result.Evidence.VadConfidence);
                candidateDurationMs = 0;
                candidateStartFrameId = decision.FrameId;
                candidatePeakVadConfidence = _candidatePeakVadConfidence.Value;
                requiredSustainedMs = RequiredSustainedMs(defaultRequiredSustainedMs, candidatePeakVadConfidence);
                _debugState = _debugState with
                {
                    CandidateActive = true,
                    CandidateStartFrameId = candidateStartFrameId,
                    CandidateDurationMs = candidateDurationMs,
                    RequiredSustainedMs = requiredSustainedMs,
                    LastVadConfidence = ClampConfidence(decision.Result.Evidence.VadConfidence),
                    CandidatePeakVadConfidence = candidatePeakVadConfidence
                };
                LogCandidateStarted(decision, requiredSustainedMs, candidatePeakVadConfidence);
            }
            else
            {
                candidateStartFrameId = _candidateStartFrameId.Value;
                _candidatePeakVadConfidence = Math.Max(
                    _candidatePeakVadConfidence ?? 0.0,
                    ClampConfidence(decision.Result.Evidence.VadConfidence));
                candidatePeakVadConfidence = _candidatePeakVadConfidence.Value;
                requiredSustainedMs = RequiredSustainedMs(defaultRequiredSustainedMs, candidatePeakVadConfidence);
                candidateDurationMs = GetCandidateDurationMs(decision.TimestampUtc, _candidateStartTimestampUtc.Value);
                _debugState = _debugState with
                {
                    CandidateActive = true,
                    CandidateStartFrameId = candidateStartFrameId,
                    CandidateDurationMs = candidateDurationMs,
                    RequiredSustainedMs = requiredSustainedMs,
                    LastVadConfidence = ClampConfidence(decision.Result.Evidence.VadConfidence),
                    CandidatePeakVadConfidence = candidatePeakVadConfidence
                };
            }

            if (candidateDurationMs < requiredSustainedMs)
            {
                LogCandidateProgress(
                    candidateStartFrameId,
                    decision.FrameId,
                    candidateDurationMs,
                    requiredSustainedMs,
                    decision.Result.Evidence.VadConfidence,
                    candidatePeakVadConfidence,
                    defaultRequiredSustainedMs);
                return;
            }

            _yieldedCurrentPlayback = true;
            _debugState = new FloorYieldDebugState
            {
                Triggered = true,
                LastFrameId = decision.FrameId,
                LastReason = decision.Result.Reason,
                LastTimestampUtc = decision.TimestampUtc,
                LastMode = PlaybackYieldMode,
                CandidateActive = false,
                CandidateStartFrameId = candidateStartFrameId,
                CandidateDurationMs = candidateDurationMs,
                RequiredSustainedMs = requiredSustainedMs,
                LastVadConfidence = ClampConfidence(decision.Result.Evidence.VadConfidence),
                CandidatePeakVadConfidence = candidatePeakVadConfidence
            };
            _candidateStartFrameId = null;
            _candidateStartTimestampUtc = null;
            _candidatePeakVadConfidence = null;
        }

        var holdResult = await _playbackService.BeginProvisionalAudioHoldAsync(
            GetActivePlaybackTurnId(),
            decision.Result.Reason,
            cancellationToken);
        var yieldMode = holdResult.Success ? PlaybackYieldMode : HoldUnavailableYieldMode;
        SetLastYieldMode(yieldMode);

        LogTriggered(decision, options, candidateStartFrameId, candidateDurationMs, requiredSustainedMs, candidatePeakVadConfidence, yieldMode);
        if (holdResult.Success)
        {
            LogHoldStarted(decision, holdResult, candidateDurationMs, requiredSustainedMs, candidatePeakVadConfidence, yieldMode);
            RecordRecentlyYieldedTurn(decision, holdResult, yieldMode);
            return;
        }

        LogHoldUnavailable(decision, holdResult, candidateDurationMs, requiredSustainedMs, candidatePeakVadConfidence, yieldMode);
    }

    private string GetActivePlaybackTurnId()
    {
        var snapshot = _playbackService.GetActivePlaybackSnapshot();
        return snapshot?.AssistantTurnId ?? string.Empty;
    }

    private void SetLastYieldMode(string yieldMode)
    {
        lock (_syncRoot)
        {
            _debugState = _debugState with { LastMode = yieldMode };
        }
    }

    private void RecordRecentlyYieldedTurn(
        SpeechPresenceOfficialDecision decision,
        ProvisionalAudioHoldResult holdResult,
        string yieldMode)
    {
        if (_recentlyYieldedTurns is null)
        {
            return;
        }

        var snapshot = _playbackService.GetActivePlaybackSnapshot();
        if (snapshot is not { IsActive: true }
            || !string.Equals(snapshot.SpeechType, SpeechPlaybackItemType.FinalAnswer.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(snapshot.AssistantTurnId))
        {
            return;
        }

        _recentlyYieldedTurns.Record(new RecentlyYieldedSpokenTurnSnapshot
        {
            TurnId = snapshot.AssistantTurnId,
            CorrelationId = string.IsNullOrWhiteSpace(snapshot.CorrelationId)
                ? snapshot.AssistantTurnId
                : snapshot.CorrelationId,
            SpeechType = snapshot.SpeechType,
            ItemType = snapshot.ItemType,
            YieldedAtUtc = DateTimeOffset.UtcNow,
            YieldReason = decision.Result.Reason,
            YieldSource = nameof(FloorYieldController),
            PlaybackWasCancelledByYieldFallback = false,
            PlaybackWasHeldByProvisionalAudioHold = true,
            HoldId = holdResult.HoldId,
            YieldMode = yieldMode
        });

        _logger.LogInformation(
            "recently_yielded_spoken_turn_recorded TurnId: {TurnId}. CorrelationId: {CorrelationId}. SpeechType: {SpeechType}. ItemType: {ItemType}. YieldReason: {YieldReason}. YieldSource: {YieldSource}. PlaybackWasCancelledByYieldFallback: {PlaybackWasCancelledByYieldFallback}. PlaybackWasHeldByProvisionalAudioHold: {PlaybackWasHeldByProvisionalAudioHold}. HoldId: {HoldId}. YieldMode: {YieldMode}.",
            snapshot.AssistantTurnId,
            snapshot.CorrelationId,
            snapshot.SpeechType,
            snapshot.ItemType,
            decision.Result.Reason,
            nameof(FloorYieldController),
            false,
            true,
            holdResult.HoldId,
            yieldMode);
    }

    public FloorYieldDebugState GetDebugState()
    {
        lock (_syncRoot)
        {
            return _debugState;
        }
    }

    private void ResetStateWhenPlaybackInactive(
        string reason,
        SpeechPresenceOfficialDecision? decision)
    {
        if (_assistantPlaybackMonitor.IsPlaybackActive)
        {
            return;
        }

        lock (_syncRoot)
        {
            ResetCandidateCore(reason, decision);
            _yieldedCurrentPlayback = false;
        }
    }

    private void ResetCandidate(
        string reason,
        SpeechPresenceOfficialDecision decision)
    {
        lock (_syncRoot)
        {
            ResetCandidateCore(reason, decision);
        }
    }

    private void ResetCandidateCore(
        string reason,
        SpeechPresenceOfficialDecision? decision)
    {
        if (_candidateStartTimestampUtc is null || _candidateStartFrameId is null)
        {
            return;
        }

        var resetTimestampUtc = decision?.TimestampUtc ?? DateTimeOffset.UtcNow;
        var durationMs = GetCandidateDurationMs(resetTimestampUtc, _candidateStartTimestampUtc.Value);
        var startFrameId = _candidateStartFrameId.Value;
        var resetFrameId = decision?.FrameId;
        _candidateStartTimestampUtc = null;
        _candidateStartFrameId = null;
        _candidatePeakVadConfidence = null;
        _debugState = _debugState with
        {
            CandidateActive = false,
            CandidateStartFrameId = startFrameId,
            CandidateDurationMs = durationMs,
            CandidatePeakVadConfidence = null
        };

        _logger.LogInformation(
            "FloorYieldCandidateReset. StartFrameId: {StartFrameId}. ResetFrameId: {ResetFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. Reason: {Reason}.",
            startFrameId,
            resetFrameId,
            durationMs,
            reason);
    }

    private void LogTriggered(
        SpeechPresenceOfficialDecision decision,
        SpeechPresenceOptions options,
        long candidateStartFrameId,
        double candidateDurationMs,
        int requiredSustainedMs,
        double candidatePeakVadConfidence,
        string playbackYieldMode)
    {
        var result = decision.Result;
        var evidence = result.Evidence;
        if (options.FloorYieldLogEvidence)
        {
            _logger.LogInformation(
                "FloorYieldTriggered. CandidateStartFrameId: {CandidateStartFrameId}. TriggerFrameId: {TriggerFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}. TimestampUtc: {TimestampUtc}. SpeechPresenceState: {SpeechPresenceState}. Confidence: {Confidence:N2}. Reason: {Reason}. RawMicRms: {RawMicRms:N4}. EchoReducedRms: {EchoReducedRms:N4}. PlaybackReferenceRms: {PlaybackReferenceRms:N4}. VadConfidence: {VadConfidence:N2}. CandidatePeakVadConfidence: {CandidatePeakVadConfidence:N2}. PlaybackCorrelationScore: {PlaybackCorrelationScore}. AssistantPlaybackActive: {AssistantPlaybackActive}. PlaybackYieldMode: {PlaybackYieldMode}. Source: {Source}.",
                candidateStartFrameId,
                decision.FrameId,
                candidateDurationMs,
                requiredSustainedMs,
                decision.TimestampUtc,
                result.State,
                result.Confidence,
                result.Reason,
                evidence.RawMicRms,
                evidence.EchoReducedRms,
                evidence.PlaybackReferenceRms,
                evidence.VadConfidence,
                candidatePeakVadConfidence,
                evidence.PlaybackCorrelationScore,
                _assistantPlaybackMonitor.IsPlaybackActive,
                playbackYieldMode,
                "official_speech_presence");
            return;
        }

        _logger.LogInformation(
            "FloorYieldTriggered. CandidateStartFrameId: {CandidateStartFrameId}. TriggerFrameId: {TriggerFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}. TimestampUtc: {TimestampUtc}. SpeechPresenceState: {SpeechPresenceState}. Confidence: {Confidence:N2}. Reason: {Reason}. VadConfidence: {VadConfidence:N2}. CandidatePeakVadConfidence: {CandidatePeakVadConfidence:N2}. AssistantPlaybackActive: {AssistantPlaybackActive}. PlaybackYieldMode: {PlaybackYieldMode}. Source: {Source}.",
            candidateStartFrameId,
            decision.FrameId,
            candidateDurationMs,
            requiredSustainedMs,
            decision.TimestampUtc,
            result.State,
            result.Confidence,
            result.Reason,
            result.Evidence.VadConfidence,
            candidatePeakVadConfidence,
            _assistantPlaybackMonitor.IsPlaybackActive,
            playbackYieldMode,
            "official_speech_presence");
    }

    private void LogHoldStarted(
        SpeechPresenceOfficialDecision decision,
        ProvisionalAudioHoldResult holdResult,
        double candidateDurationMs,
        int requiredSustainedMs,
        double candidatePeakVadConfidence,
        string playbackYieldMode)
    {
        var snapshot = _playbackService.GetActivePlaybackSnapshot();
        LogHoldOutcome(
            "FloorYieldProvisionalAudioHoldStarted",
            decision,
            holdResult,
            snapshot,
            candidateDurationMs,
            requiredSustainedMs,
            candidatePeakVadConfidence,
            playbackYieldMode);
    }

    private void LogHoldUnavailable(
        SpeechPresenceOfficialDecision decision,
        ProvisionalAudioHoldResult holdResult,
        double candidateDurationMs,
        int requiredSustainedMs,
        double candidatePeakVadConfidence,
        string playbackYieldMode)
    {
        var snapshot = _playbackService.GetActivePlaybackSnapshot();
        LogHoldOutcome(
            "FloorYieldProvisionalAudioHoldUnavailable",
            decision,
            holdResult,
            snapshot,
            candidateDurationMs,
            requiredSustainedMs,
            candidatePeakVadConfidence,
            playbackYieldMode);
    }

    private void LogHoldOutcome(
        string eventName,
        SpeechPresenceOfficialDecision decision,
        ProvisionalAudioHoldResult holdResult,
        ActiveSpeechPlaybackSnapshot? snapshot,
        double candidateDurationMs,
        int requiredSustainedMs,
        double candidatePeakVadConfidence,
        string playbackYieldMode)
    {
        var result = decision.Result;
        var evidence = result.Evidence;
        _logger.LogInformation(
            "{EventName}. TurnId: {TurnId}. CorrelationId: {CorrelationId}. SpeechType: {SpeechType}. ItemType: {ItemType}. HoldId: {HoldId}. YieldReason: {YieldReason}. PlaybackYieldMode: {PlaybackYieldMode}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}. VadConfidence: {VadConfidence:N2}. CandidatePeakVadConfidence: {CandidatePeakVadConfidence:N2}. PlaybackCorrelationScore: {PlaybackCorrelationScore}. RawMicRms: {RawMicRms:N4}. EchoReducedRms: {EchoReducedRms:N4}. PlaybackReferenceRms: {PlaybackReferenceRms:N4}. FailureReason: {FailureReason}.",
            eventName,
            holdResult.TurnId ?? snapshot?.AssistantTurnId,
            snapshot?.CorrelationId,
            snapshot?.SpeechType,
            snapshot?.ItemType,
            holdResult.HoldId,
            result.Reason,
            playbackYieldMode,
            candidateDurationMs,
            requiredSustainedMs,
            evidence.VadConfidence,
            candidatePeakVadConfidence,
            evidence.PlaybackCorrelationScore,
            evidence.RawMicRms,
            evidence.EchoReducedRms,
            evidence.PlaybackReferenceRms,
            holdResult.FailureReason);
    }

    private void LogIgnored(string reason, SpeechPresenceOfficialDecision decision)
    {
        _logger.LogDebug(
            "FloorYieldIgnored. Reason: {Reason}. FrameId: {FrameId}. Source: {Source}. ShouldYieldPlayback: {ShouldYieldPlayback}. AssistantPlaybackActive: {AssistantPlaybackActive}.",
            reason,
            decision.FrameId,
            decision.SourcePath,
            decision.Result.ShouldYieldPlayback,
            _assistantPlaybackMonitor.IsPlaybackActive);
    }

    private void LogCandidateStarted(
        SpeechPresenceOfficialDecision decision,
        int requiredSustainedMs,
        double candidatePeakVadConfidence)
    {
        _logger.LogInformation(
            "FloorYieldCandidateStarted. StartFrameId: {StartFrameId}. TimestampUtc: {TimestampUtc}. SpeechPresenceState: {SpeechPresenceState}. Confidence: {Confidence:N2}. Reason: {Reason}. VadConfidence: {VadConfidence:N2}. CandidatePeakVadConfidence: {CandidatePeakVadConfidence:N2}. RequiredSustainedMs: {RequiredSustainedMs}.",
            decision.FrameId,
            decision.TimestampUtc,
            decision.Result.State,
            decision.Result.Confidence,
            decision.Result.Reason,
            decision.Result.Evidence.VadConfidence,
            candidatePeakVadConfidence,
            requiredSustainedMs);
    }

    private void LogCandidateProgress(
        long candidateStartFrameId,
        long currentFrameId,
        double candidateDurationMs,
        int requiredSustainedMs,
        double vadConfidence,
        double candidatePeakVadConfidence,
        int defaultRequiredSustainedMs)
    {
        _logger.LogDebug(
            "FloorYieldCandidateProgress. StartFrameId: {StartFrameId}. CurrentFrameId: {CurrentFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}. VadConfidence: {VadConfidence:N2}. CandidatePeakVadConfidence: {CandidatePeakVadConfidence:N2}. Reason: {Reason}.",
            candidateStartFrameId,
            currentFrameId,
            candidateDurationMs,
            requiredSustainedMs,
            vadConfidence,
            candidatePeakVadConfidence,
            requiredSustainedMs > defaultRequiredSustainedMs ? "low_vad_extended_sustain" : "default_sustain");
    }

    private static int RequiredSustainedMs(int defaultRequiredSustainedMs, double peakVadConfidence)
    {
        if (peakVadConfidence < 0.20)
        {
            return Math.Max(defaultRequiredSustainedMs, 90);
        }

        if (peakVadConfidence < 0.40)
        {
            return Math.Max(defaultRequiredSustainedMs, 60);
        }

        return defaultRequiredSustainedMs;
    }

    private static double ClampConfidence(double value) => Math.Clamp(value, 0.0, 1.0);

    private static double GetCandidateDurationMs(
        DateTimeOffset currentTimestampUtc,
        DateTimeOffset candidateStartTimestampUtc)
    {
        return Math.Max(0, (currentTimestampUtc - candidateStartTimestampUtc).TotalMilliseconds);
    }
}
