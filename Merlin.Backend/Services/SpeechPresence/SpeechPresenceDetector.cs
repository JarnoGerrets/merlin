using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.SpeechPresence;

public sealed class SpeechPresenceDetector : ISpeechPresenceDetector
{
    private readonly IOptionsMonitor<SpeechPresenceOptions> _options;

    public SpeechPresenceDetector(
        IOptionsMonitor<SpeechPresenceOptions> options,
        ILogger<SpeechPresenceDetector> logger,
        ISpeechPresenceDecisionLogSink? decisionLogSink = null)
    {
        _options = options;
    }

    public SpeechPresenceResult Evaluate(SpeechPresenceEvidence evidence)
    {
        var options = _options.CurrentValue;
        return options.Enabled
            ? EvaluateEnabled(evidence, options)
            : CreateResult(SpeechPresenceState.No, 0.0, false, "disabled", evidence);
    }

    private static SpeechPresenceResult EvaluateEnabled(
        SpeechPresenceEvidence evidence,
        SpeechPresenceOptions options)
    {
        var rawEnergyConfidence = EnergyConfidence(evidence.RawMicRms, options.MinRawMicRms);
        var residualEnergyConfidence = EnergyConfidence(evidence.EchoReducedRms, options.MinEchoReducedRms);
        var vadConfidence = evidence.VadSpeechDetected
            ? Math.Clamp(evidence.VadConfidence, options.MinVadConfidence, 1.0)
            : Math.Clamp(evidence.VadConfidence, 0.0, 1.0);
        var confidence = Math.Clamp(
            Math.Max(vadConfidence, Math.Max(rawEnergyConfidence, residualEnergyConfidence)),
            0.0,
            1.0);
        var hasStrongSelfEchoEvidence = evidence.StrongSelfEchoEvidence
            || evidence.PlaybackCorrelationScore >= options.StrongSelfEchoCorrelationThreshold;
        var hasStrongResidualEvidence = evidence.EchoReducedRms >= options.MinEchoReducedRms
            || evidence.UserSpeechScoreLegacy is >= 0.55;

        if (hasStrongSelfEchoEvidence && !hasStrongResidualEvidence)
        {
            return CreateResult(SpeechPresenceState.No, Math.Min(confidence, 0.45), false, "strong_self_echo", evidence);
        }

        if (evidence.VadSpeechDetected && evidence.EchoReducedRms >= options.MinEchoReducedRms)
        {
            if (IsSelfEchoContaminatedResidual(evidence, options))
            {
                return CreateResult(
                    SpeechPresenceState.No,
                    Math.Min(confidence, 0.45),
                    false,
                    "self_echo_contaminated_residual",
                    evidence);
            }

            var residualConfidence = Math.Clamp(Math.Max(confidence, residualEnergyConfidence), options.MaybeConfidenceThreshold, 1.0);
            var state = residualConfidence >= options.YesConfidenceThreshold
                ? SpeechPresenceState.Yes
                : SpeechPresenceState.Maybe;
            return CreateResult(state, residualConfidence, evidence.AssistantPlaybackActive, "residual_speech_detected", evidence);
        }

        if (evidence.VadSpeechDetected && evidence.RawMicRms >= options.MinRawMicRms)
        {
            var possibleConfidence = Math.Clamp(Math.Max(confidence, rawEnergyConfidence), options.MaybeConfidenceThreshold, 1.0);
            return CreateResult(SpeechPresenceState.Maybe, possibleConfidence, evidence.AssistantPlaybackActive, "possible_near_end_speech", evidence);
        }

        return CreateResult(SpeechPresenceState.No, Math.Min(confidence, 0.45), false, "no_speech_evidence", evidence);
    }

    private static bool IsSelfEchoContaminatedResidual(
        SpeechPresenceEvidence evidence,
        SpeechPresenceOptions options)
    {
        return evidence.AssistantPlaybackActive
            && evidence.StrongSelfEchoEvidence
            && evidence.PlaybackCorrelationScore >= options.SelfEchoContaminatedCorrelationThreshold
            && evidence.VadConfidence < options.ClearNearEndVadConfidence
            && evidence.EchoReducedRms < options.ClearNearEndEchoReducedRms
            && evidence.RawMicRms < options.ClearNearEndRawMicRms;
    }

    private static SpeechPresenceResult CreateResult(
        SpeechPresenceState state,
        double confidence,
        bool shouldYieldPlayback,
        string reason,
        SpeechPresenceEvidence evidence)
    {
        var isUserSpeaking = state is SpeechPresenceState.Maybe or SpeechPresenceState.Yes;
        return new SpeechPresenceResult
        {
            State = state,
            Confidence = Math.Clamp(confidence, 0.0, 1.0),
            IsUserSpeaking = isUserSpeaking,
            ShouldYieldPlayback = isUserSpeaking && evidence.AssistantPlaybackActive && shouldYieldPlayback,
            Reason = reason,
            Evidence = evidence
        };
    }

    private static double EnergyConfidence(double value, double threshold)
    {
        if (threshold <= 0.0)
        {
            return value > 0.0 ? 1.0 : 0.0;
        }

        return Math.Clamp(value / (threshold * 2.0), 0.0, 1.0);
    }
}
