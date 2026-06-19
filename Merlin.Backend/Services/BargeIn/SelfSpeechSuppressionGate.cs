using Merlin.Backend.Configuration;
using Microsoft.Extensions.Logging;

namespace Merlin.Backend.Services.BargeIn;

public sealed class SelfSpeechSuppressionGate : ISelfSpeechSuppressionGate
{
    private readonly ILogger<SelfSpeechSuppressionGate> _logger;
    private readonly ISelfSpeechGateDiagnosticsWriter _diagnosticsWriter;
    private int _consecutiveUncertainSpeechFrames;

    public SelfSpeechSuppressionGate(
        ILogger<SelfSpeechSuppressionGate> logger,
        ISelfSpeechGateDiagnosticsWriter diagnosticsWriter)
    {
        _logger = logger;
        _diagnosticsWriter = diagnosticsWriter;
    }

    public SelfSpeechGateResult Evaluate(SelfSpeechGateInput input, BargeInOptions options)
    {
        var gateOptions = options.SelfSpeechSuppression;
        if (!gateOptions.Enabled || !gateOptions.SuppressDuringPlayback)
        {
            Reset();
            return Result(SelfSpeechDecision.Allow, 1.0, "Self-speech suppression disabled.", input, options, 0.0, 1.0, 0);
        }

        if (!input.VadSaysSpeech)
        {
            Reset();
            return Result(SelfSpeechDecision.SuppressAsSelfEcho, 1.0, "VAD did not report speech.", input, options, 0.0, 0.0, 0);
        }

        if (!input.AssistantPlaybackActive)
        {
            Reset();
            return Result(SelfSpeechDecision.Allow, 1.0, "Assistant playback is not active.", input, options, 0.0, 1.0, 0);
        }

        var estimatedEcho = EstimateEchoEnergy(input.PlaybackEnergy, options);
        var userSpeechScore = CalculateUserSpeechScore(input.MicEnergy, estimatedEcho, gateOptions);
        var strongUserSpeech = IsStrongUserSpeech(input.MicEnergy, estimatedEcho, gateOptions);
        var correlationSaysSelfEcho = string.Equals(
            input.CorrelationDecision,
            SelfSpeechCorrelationDecision.SelfEcho,
            StringComparison.Ordinal);
        var inOnsetGrace = input.PlaybackAge is { } playbackAge
            && playbackAge <= TimeSpan.FromMilliseconds(Math.Max(0, gateOptions.PlaybackOnsetGraceMs));

        if (correlationSaysSelfEcho && CorrelationShouldSuppress(input, gateOptions))
        {
            Reset();
            return Result(SelfSpeechDecision.SuppressAsSelfEcho, 0.95, "Mic audio strongly correlates with assistant playback reference.", input, options, estimatedEcho, userSpeechScore, 0);
        }

        if (strongUserSpeech)
        {
            Reset();
            return Result(SelfSpeechDecision.Allow, Math.Clamp(userSpeechScore, 0.55, 1.0), "Mic energy clearly exceeds expected playback echo.", input, options, estimatedEcho, userSpeechScore, 0);
        }

        if (inOnsetGrace)
        {
            Reset();
            return Result(SelfSpeechDecision.SuppressAsSelfEcho, 0.9, "Playback onset grace suppressed weak speech-like audio.", input, options, estimatedEcho, userSpeechScore, 0);
        }

        if (input.MicEnergy <= estimatedEcho + Math.Max(0.0, gateOptions.EchoMargin))
        {
            Reset();
            return Result(SelfSpeechDecision.SuppressAsSelfEcho, 0.85, "Mic energy is consistent with expected playback echo.", input, options, estimatedEcho, userSpeechScore, 0);
        }

        _consecutiveUncertainSpeechFrames++;
        var sustainedFrames = _consecutiveUncertainSpeechFrames;
        if (ShouldAllowSustainedUncertain(input, estimatedEcho, gateOptions, sustainedFrames))
        {
            Reset();
            return Result(SelfSpeechDecision.Allow, 0.6, "Borderline speech allowed by strict fast hard-stop uncertainty policy.", input, options, estimatedEcho, userSpeechScore, sustainedFrames);
        }

        return Result(SelfSpeechDecision.Uncertain, 0.5, UncertainReason(input, gateOptions), input, options, estimatedEcho, userSpeechScore, sustainedFrames);
    }

    public void Reset()
    {
        _consecutiveUncertainSpeechFrames = 0;
    }

    private static double EstimateEchoEnergy(double playbackEnergy, BargeInOptions options)
    {
        var gateOptions = options.SelfSpeechSuppression;
        var leakageEstimate = playbackEnergy * Math.Max(0.0, gateOptions.EchoLeakageMultiplier);
        return Math.Max(leakageEstimate, options.VadEnergyThreshold);
    }

    private static bool IsStrongUserSpeech(double micEnergy, double estimatedEcho, SelfSpeechSuppressionOptions options)
    {
        var ratioThreshold = estimatedEcho * Math.Max(1.0, options.UserSpeechRatio);
        var marginThreshold = estimatedEcho + Math.Max(0.0, options.UserSpeechMargin);
        return micEnergy >= Math.Max(ratioThreshold, marginThreshold);
    }

    private static double CalculateUserSpeechScore(double micEnergy, double estimatedEcho, SelfSpeechSuppressionOptions options)
    {
        var margin = Math.Max(0.0001, options.UserSpeechMargin);
        return Math.Clamp((micEnergy - estimatedEcho) / margin, 0.0, 1.0);
    }

    private static bool ShouldAllowSustainedUncertain(
        SelfSpeechGateInput input,
        double estimatedEcho,
        SelfSpeechSuppressionOptions options,
        int sustainedFrames)
    {
        return input.Reason switch
        {
            "live_ducking" => options.AllowSustainedUncertainForDucking
                && sustainedFrames >= Math.Max(1, options.RequireSustainedUserSpeechFrames),
            "fast_near_end_ducking" => false,
            "comfort_ducking" => false,
            "normal_capture" or "vad_triggered_capture" => options.AllowSustainedUncertainForCapture
                && sustainedFrames >= Math.Max(1, options.RequireSustainedUserSpeechFrames),
            "fast_hard_stop_candidate" => options.AllowSustainedUncertainForFastHardStop
                && sustainedFrames >= Math.Max(1, options.FastHardStopUncertainFrames)
                && input.MicEnergy >= estimatedEcho + Math.Max(0.0, options.FastHardStopUncertainExtraMargin),
            _ => sustainedFrames >= Math.Max(1, options.RequireSustainedUserSpeechFrames)
        };
    }

    private static bool CorrelationShouldSuppress(
        SelfSpeechGateInput input,
        SelfSpeechSuppressionOptions options)
    {
        return input.Reason switch
        {
            "live_ducking" => true,
            "fast_near_end_ducking" => true,
            "comfort_ducking" => true,
            "normal_capture" or "vad_triggered_capture" => true,
            "fast_hard_stop_candidate" => input.CorrelationScore >= Math.Max(
                Math.Clamp(options.CorrelationSelfEchoThreshold, 0.0, 1.0),
                0.85),
            _ => input.MicEnergy < Math.Max(0.0, options.UserSpeechMargin) * 2.0
        };
    }

    private static string UncertainReason(SelfSpeechGateInput input, SelfSpeechSuppressionOptions options)
    {
        return input.Reason switch
        {
            "live_ducking" when !options.AllowSustainedUncertainForDucking =>
                "Borderline playback audio remains uncertain; strict live ducking policy suppresses sustained uncertain frames.",
            "fast_near_end_ducking" =>
                "Borderline playback audio remains uncertain; fast near-end ducking requires clear user speech.",
            "comfort_ducking" =>
                "Borderline playback audio remains uncertain; comfort ducking may lower volume without starting capture.",
            "normal_capture" or "vad_triggered_capture" when !options.AllowSustainedUncertainForCapture =>
                "Borderline playback audio remains uncertain; strict capture policy suppresses sustained uncertain frames.",
            "fast_hard_stop_candidate" =>
                "Borderline playback audio remains uncertain; fast hard-stop requires stricter sustained margin before probing.",
            _ => "Mic energy is above echo estimate but not clearly user speech yet."
        };
    }

    private SelfSpeechGateResult Result(
        SelfSpeechDecision decision,
        double confidence,
        string reason,
        SelfSpeechGateInput input,
        BargeInOptions options,
        double estimatedEcho,
        double userSpeechScore,
        int sustainedUncertainFrames)
    {
        var result = new SelfSpeechGateResult
        {
            Decision = decision,
            Confidence = confidence,
            Reason = reason,
            MicEnergy = input.MicEnergy,
            PlaybackEnergy = input.PlaybackEnergy,
            EstimatedEchoEnergy = estimatedEcho,
            UserSpeechScore = userSpeechScore,
            SustainedUncertainFrames = sustainedUncertainFrames,
            CorrelationScore = input.CorrelationScore,
            BestDelayMs = input.BestDelayMs,
            CorrelationDecision = input.CorrelationDecision,
            CorrelationAvailable = input.CorrelationAvailable,
            CorrelationReason = input.CorrelationReason,
            ReferenceWindowAvailable = input.ReferenceWindowAvailable,
            ReferenceWindowEnergy = input.ReferenceWindowEnergy,
            ReferenceWindowSampleCount = input.ReferenceWindowSampleCount,
            RequestedMicSampleCount = input.RequestedMicSampleCount,
            RequestedDelayMinMs = input.RequestedDelayMinMs,
            RequestedDelayMaxMs = input.RequestedDelayMaxMs,
            RequestedDelayStepMs = input.RequestedDelayStepMs,
            PlaybackRingBufferedSamples = input.PlaybackRingBufferedSamples,
            PlaybackRingCapacitySamples = input.PlaybackRingCapacitySamples,
            PlaybackRingBufferedMs = input.PlaybackRingBufferedMs,
            PlaybackTapSampleRate = input.PlaybackTapSampleRate,
            MicSampleRate = input.MicSampleRate,
            SampleRateMatches = input.SampleRateMatches,
            PlaybackWritePosition = input.PlaybackWritePosition,
            NumberOfDelayWindowsChecked = input.NumberOfDelayWindowsChecked,
            NumberOfDelayWindowsAvailable = input.NumberOfDelayWindowsAvailable,
            NumberOfDelayWindowsSkippedLowEnergy = input.NumberOfDelayWindowsSkippedLowEnergy,
            MaxReferenceEnergySeen = input.MaxReferenceEnergySeen,
            CorrelationUnavailableReason = input.CorrelationUnavailableReason,
            PlaybackReferenceSource = input.PlaybackReferenceSource,
            PlaybackReferenceIsConsumptionAligned = input.PlaybackReferenceIsConsumptionAligned,
            PlaybackConsumedSamplesTotal = input.PlaybackConsumedSamplesTotal,
            ReferenceBufferedMs = input.ReferenceBufferedMs,
            ReferenceNewestAgeMs = input.ReferenceNewestAgeMs,
            ReferenceOldestAgeMs = input.ReferenceOldestAgeMs,
            OutputReadSamples = input.OutputReadSamples,
            OutputReadDurationMs = input.OutputReadDurationMs,
            LastOutputReadAtUtc = input.LastOutputReadAtUtc
        };

        WriteDiagnostic(result, input, options);

        _logger.LogDebug(
            "Self-speech gate evaluated. Decision: {Decision}. Reason: {Reason}. AssistantPlaybackActive: {AssistantPlaybackActive}. VadSaysSpeech: {VadSaysSpeech}. MicEnergy: {MicEnergy:N4}. PlaybackEnergy: {PlaybackEnergy:N4}. EstimatedEcho: {EstimatedEcho:N4}. UserSpeechScore: {UserSpeechScore:N2}. InputReason: {InputReason}.",
            result.Decision,
            result.Reason,
            input.AssistantPlaybackActive,
            input.VadSaysSpeech,
            result.MicEnergy,
            result.PlaybackEnergy,
            result.EstimatedEchoEnergy,
            result.UserSpeechScore,
            input.Reason);

        return result;
    }

    private void WriteDiagnostic(
        SelfSpeechGateResult result,
        SelfSpeechGateInput input,
        BargeInOptions options)
    {
        var gateOptions = options.SelfSpeechSuppression;
        if (!gateOptions.LogDecisions && !gateOptions.DiagnosticsFileEnabled)
        {
            return;
        }

        var entry = new SelfSpeechGateDiagnosticEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            InputReason = input.Reason,
            Decision = result.Decision.ToString(),
            Reason = result.Reason,
            MicEnergy = result.MicEnergy,
            PlaybackEnergy = result.PlaybackEnergy,
            CurrentPlaybackEnergy = input.CurrentPlaybackEnergy,
            RecentPlaybackEnergy = input.RecentPlaybackEnergy,
            EstimatedEchoEnergy = result.EstimatedEchoEnergy,
            EchoLeakageMultiplier = gateOptions.EchoLeakageMultiplier,
            EchoMargin = gateOptions.EchoMargin,
            UserSpeechRatio = gateOptions.UserSpeechRatio,
            UserSpeechMargin = gateOptions.UserSpeechMargin,
            UserSpeechScore = result.UserSpeechScore,
            PlaybackAgeMs = input.PlaybackAge is null ? null : (long)input.PlaybackAge.Value.TotalMilliseconds,
            AssistantPlaybackActive = input.AssistantPlaybackActive,
            VadSaysSpeech = input.VadSaysSpeech,
            VadConfidence = input.VadConfidence,
            AecVerified = input.AecVerified,
            SustainedUncertainFrames = result.SustainedUncertainFrames,
            RequiredSustainedUserSpeechFrames = gateOptions.RequireSustainedUserSpeechFrames,
            ConfigPolicyMode = gateOptions.PolicyMode,
            CorrelationId = input.CorrelationId,
            CorrelationScore = input.CorrelationScore,
            BestDelayMs = input.BestDelayMs,
            CorrelationDecision = input.CorrelationDecision,
            CorrelationAvailable = input.CorrelationAvailable,
            CorrelationMinScore = Math.Max(gateOptions.CorrelationMinScore, gateOptions.CorrelationSelfEchoThreshold),
            CorrelationMinDelayMs = gateOptions.CorrelationMinDelayMs,
            CorrelationMaxDelayMs = gateOptions.CorrelationMaxDelayMs,
            CorrelationDelayStepMs = gateOptions.CorrelationStepMs,
            CorrelationReason = input.CorrelationReason,
            ReferenceWindowAvailable = input.ReferenceWindowAvailable,
            ReferenceWindowEnergy = input.ReferenceWindowEnergy,
            ReferenceWindowSampleCount = input.ReferenceWindowSampleCount,
            RequestedMicSampleCount = input.RequestedMicSampleCount,
            RequestedDelayMinMs = input.RequestedDelayMinMs,
            RequestedDelayMaxMs = input.RequestedDelayMaxMs,
            RequestedDelayStepMs = input.RequestedDelayStepMs,
            PlaybackRingBufferedSamples = input.PlaybackRingBufferedSamples,
            PlaybackRingCapacitySamples = input.PlaybackRingCapacitySamples,
            PlaybackRingBufferedMs = input.PlaybackRingBufferedMs,
            PlaybackTapSampleRate = input.PlaybackTapSampleRate,
            MicSampleRate = input.MicSampleRate,
            SampleRateMatches = input.SampleRateMatches,
            PlaybackWritePosition = input.PlaybackWritePosition,
            NumberOfDelayWindowsChecked = input.NumberOfDelayWindowsChecked,
            NumberOfDelayWindowsAvailable = input.NumberOfDelayWindowsAvailable,
            NumberOfDelayWindowsSkippedLowEnergy = input.NumberOfDelayWindowsSkippedLowEnergy,
            MaxReferenceEnergySeen = input.MaxReferenceEnergySeen,
            CorrelationUnavailableReason = input.CorrelationUnavailableReason,
            PlaybackReferenceSource = input.PlaybackReferenceSource,
            PlaybackReferenceIsConsumptionAligned = input.PlaybackReferenceIsConsumptionAligned,
            PlaybackConsumedSamplesTotal = input.PlaybackConsumedSamplesTotal,
            ReferenceBufferedMs = input.ReferenceBufferedMs,
            ReferenceNewestAgeMs = input.ReferenceNewestAgeMs,
            ReferenceOldestAgeMs = input.ReferenceOldestAgeMs,
            OutputReadSamples = input.OutputReadSamples,
            OutputReadDurationMs = input.OutputReadDurationMs,
            LastOutputReadAtUtc = input.LastOutputReadAtUtc
        };

        try
        {
            _diagnosticsWriter.Write(entry, options);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Self-speech gate diagnostics writer threw unexpectedly.");
        }
    }
}
