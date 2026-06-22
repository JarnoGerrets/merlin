using Merlin.Backend.Services.BargeIn;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.SpeechPresence;

public sealed class FloorYieldController : IFloorYieldController
{
    public const string PlaybackYieldMode = "existing_pause_method_cancel_fallback";

    private readonly IAssistantPlaybackMonitor _assistantPlaybackMonitor;
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly ILogger<FloorYieldController> _logger;
    private readonly IOptionsMonitor<SpeechPresenceOptions> _options;
    private readonly object _syncRoot = new();
    private bool _yieldedCurrentPlayback;
    private long? _candidateStartFrameId;
    private DateTimeOffset? _candidateStartTimestampUtc;
    private FloorYieldDebugState _debugState = new();

    public FloorYieldController(
        IAssistantPlaybackMonitor assistantPlaybackMonitor,
        IAssistantSpeechPlaybackService playbackService,
        IOptionsMonitor<SpeechPresenceOptions> options,
        ILogger<FloorYieldController> logger)
    {
        _assistantPlaybackMonitor = assistantPlaybackMonitor;
        _playbackService = playbackService;
        _options = options;
        _logger = logger;
    }

    public async Task HandleOfficialDecisionAsync(
        SpeechPresenceOfficialDecision? decision,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var requiredSustainedMs = Math.Max(0, options.FloorYieldMinSustainedMs);
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
        lock (_syncRoot)
        {
            if (_yieldedCurrentPlayback)
            {
                ResetCandidateCore("already_yielded_current_playback", decision);
                return;
            }

            if (_candidateStartTimestampUtc is null || _candidateStartFrameId is null)
            {
                _candidateStartTimestampUtc = decision.TimestampUtc;
                _candidateStartFrameId = decision.FrameId;
                candidateDurationMs = 0;
                candidateStartFrameId = decision.FrameId;
                _debugState = _debugState with
                {
                    CandidateActive = true,
                    CandidateStartFrameId = candidateStartFrameId,
                    CandidateDurationMs = candidateDurationMs,
                    RequiredSustainedMs = requiredSustainedMs
                };
                LogCandidateStarted(decision, requiredSustainedMs);
            }
            else
            {
                candidateStartFrameId = _candidateStartFrameId.Value;
                candidateDurationMs = GetCandidateDurationMs(decision.TimestampUtc, _candidateStartTimestampUtc.Value);
                _debugState = _debugState with
                {
                    CandidateActive = true,
                    CandidateStartFrameId = candidateStartFrameId,
                    CandidateDurationMs = candidateDurationMs,
                    RequiredSustainedMs = requiredSustainedMs
                };
            }

            if (candidateDurationMs < requiredSustainedMs)
            {
                LogCandidateProgress(candidateStartFrameId, decision.FrameId, candidateDurationMs, requiredSustainedMs);
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
                RequiredSustainedMs = requiredSustainedMs
            };
            _candidateStartFrameId = null;
            _candidateStartTimestampUtc = null;
        }

        LogTriggered(decision, options, candidateStartFrameId, candidateDurationMs, requiredSustainedMs);
        await _playbackService.PauseCurrentSpeechAsync(cancellationToken);
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
        _debugState = _debugState with
        {
            CandidateActive = false,
            CandidateStartFrameId = startFrameId,
            CandidateDurationMs = durationMs
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
        int requiredSustainedMs)
    {
        var result = decision.Result;
        var evidence = result.Evidence;
        if (options.FloorYieldLogEvidence)
        {
            _logger.LogInformation(
                "FloorYieldTriggered. CandidateStartFrameId: {CandidateStartFrameId}. TriggerFrameId: {TriggerFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}. TimestampUtc: {TimestampUtc}. SpeechPresenceState: {SpeechPresenceState}. Confidence: {Confidence:N2}. Reason: {Reason}. RawMicRms: {RawMicRms:N4}. EchoReducedRms: {EchoReducedRms:N4}. PlaybackReferenceRms: {PlaybackReferenceRms:N4}. VadConfidence: {VadConfidence:N2}. PlaybackCorrelationScore: {PlaybackCorrelationScore}. AssistantPlaybackActive: {AssistantPlaybackActive}. PlaybackYieldMode: {PlaybackYieldMode}. Source: {Source}.",
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
                evidence.PlaybackCorrelationScore,
                _assistantPlaybackMonitor.IsPlaybackActive,
                PlaybackYieldMode,
                "official_speech_presence");
            return;
        }

        _logger.LogInformation(
            "FloorYieldTriggered. CandidateStartFrameId: {CandidateStartFrameId}. TriggerFrameId: {TriggerFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}. TimestampUtc: {TimestampUtc}. SpeechPresenceState: {SpeechPresenceState}. Confidence: {Confidence:N2}. Reason: {Reason}. AssistantPlaybackActive: {AssistantPlaybackActive}. PlaybackYieldMode: {PlaybackYieldMode}. Source: {Source}.",
            candidateStartFrameId,
            decision.FrameId,
            candidateDurationMs,
            requiredSustainedMs,
            decision.TimestampUtc,
            result.State,
            result.Confidence,
            result.Reason,
            _assistantPlaybackMonitor.IsPlaybackActive,
            PlaybackYieldMode,
            "official_speech_presence");
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
        int requiredSustainedMs)
    {
        _logger.LogInformation(
            "FloorYieldCandidateStarted. StartFrameId: {StartFrameId}. TimestampUtc: {TimestampUtc}. SpeechPresenceState: {SpeechPresenceState}. Confidence: {Confidence:N2}. Reason: {Reason}. RequiredSustainedMs: {RequiredSustainedMs}.",
            decision.FrameId,
            decision.TimestampUtc,
            decision.Result.State,
            decision.Result.Confidence,
            decision.Result.Reason,
            requiredSustainedMs);
    }

    private void LogCandidateProgress(
        long candidateStartFrameId,
        long currentFrameId,
        double candidateDurationMs,
        int requiredSustainedMs)
    {
        _logger.LogDebug(
            "FloorYieldCandidateProgress. StartFrameId: {StartFrameId}. CurrentFrameId: {CurrentFrameId}. CandidateDurationMs: {CandidateDurationMs:N1}. RequiredSustainedMs: {RequiredSustainedMs}.",
            candidateStartFrameId,
            currentFrameId,
            candidateDurationMs,
            requiredSustainedMs);
    }

    private static double GetCandidateDurationMs(
        DateTimeOffset currentTimestampUtc,
        DateTimeOffset candidateStartTimestampUtc)
    {
        return Math.Max(0, (currentTimestampUtc - candidateStartTimestampUtc).TotalMilliseconds);
    }
}
