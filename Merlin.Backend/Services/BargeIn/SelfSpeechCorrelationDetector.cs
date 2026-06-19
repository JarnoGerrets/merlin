using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public static class SelfSpeechCorrelationDetector
{
    public static SelfSpeechCorrelationResult Analyze(
        ReadOnlySpan<float> micSamples,
        int micSampleRate,
        IPlaybackReferenceTap playbackReferenceTap,
        SelfSpeechSuppressionOptions options)
    {
        var snapshot = playbackReferenceTap.GetDebugSnapshot();
        var result = CreateBaseResult(micSamples.Length, micSampleRate, options, snapshot);
        if (!options.CorrelationDetectionEnabled || micSamples.IsEmpty || micSampleRate <= 0)
        {
            return Unavailable(result, "Correlation unavailable because detection is disabled, samples are missing, or sample rate is invalid.");
        }

        if (snapshot.SampleRate != micSampleRate)
        {
            result = result with { SampleRateMatches = false };
            return Unavailable(result, "Correlation unavailable because mic sample rate does not match playback reference sample rate.");
        }

        var micEnergy = AudioEnergyCalculator.CalculateRms(micSamples);
        if (micEnergy < Math.Max(0.0, options.CorrelationMinMicEnergy))
        {
            return Unavailable(result, "Correlation unavailable because mic energy is below threshold.");
        }

        var minDelayMs = Math.Max(0, options.CorrelationMinDelayMs);
        var maxDelayMs = Math.Max(minDelayMs, options.CorrelationMaxDelayMs);
        var stepMs = Math.Max(1, options.CorrelationStepMs);
        var referenceWindow = new float[micSamples.Length];
        double bestScore = 0.0;
        int? bestDelayMs = null;
        double? bestReferenceEnergy = null;
        var checkedWindows = 0;
        var availableWindows = 0;
        var skippedLowEnergy = 0;
        var maxReferenceEnergySeen = 0.0;

        for (var delayMs = minDelayMs; delayMs <= maxDelayMs; delayMs += stepMs)
        {
            checkedWindows++;
            if (!playbackReferenceTap.TryGetReferenceWindow(delayMs, micSamples.Length, referenceWindow))
            {
                continue;
            }

            availableWindows++;
            var referenceEnergy = AudioEnergyCalculator.CalculateRms(referenceWindow);
            maxReferenceEnergySeen = Math.Max(maxReferenceEnergySeen, referenceEnergy);
            if (referenceEnergy < Math.Max(0.0, options.CorrelationMinReferenceEnergy))
            {
                skippedLowEnergy++;
                continue;
            }

            var score = Math.Abs(CalculateNormalizedCorrelation(micSamples, referenceWindow));
            if (score > bestScore)
            {
                bestScore = score;
                bestDelayMs = delayMs;
                bestReferenceEnergy = referenceEnergy;
            }
        }

        result = result with
        {
            ReferenceWindowAvailable = availableWindows > 0,
            ReferenceWindowEnergy = bestReferenceEnergy ?? (availableWindows > 0 ? maxReferenceEnergySeen : null),
            ReferenceWindowSampleCount = availableWindows > 0 ? micSamples.Length : 0,
            NumberOfDelayWindowsChecked = checkedWindows,
            NumberOfDelayWindowsAvailable = availableWindows,
            NumberOfDelayWindowsSkippedLowEnergy = skippedLowEnergy,
            MaxReferenceEnergySeen = maxReferenceEnergySeen
        };

        if (availableWindows == 0)
        {
            return Unavailable(result, "Correlation unavailable because playback reference does not have enough history for any requested delay window.");
        }

        if (skippedLowEnergy == availableWindows && bestDelayMs is null)
        {
            return Unavailable(result, "Correlation unavailable because all playback reference windows are below threshold.");
        }

        var selfEchoThreshold = Math.Clamp(
            Math.Max(options.CorrelationMinScore, options.CorrelationSelfEchoThreshold),
            0.0,
            1.0);
        var decision = bestScore >= selfEchoThreshold
            ? SelfSpeechCorrelationDecision.SelfEcho
            : bestScore <= Math.Clamp(options.CorrelationLikelyUserThreshold, 0.0, 1.0)
                ? SelfSpeechCorrelationDecision.LikelyUser
                : SelfSpeechCorrelationDecision.WeakCorrelation;
        var reason = decision switch
        {
            SelfSpeechCorrelationDecision.SelfEcho => "Mic frame strongly correlates with delayed playback reference.",
            SelfSpeechCorrelationDecision.LikelyUser => "Mic frame has low correlation with playback reference.",
            _ => "Mic frame has weak or borderline correlation with playback reference."
        };

        return result with
        {
            IsAvailable = true,
            CorrelationScore = bestScore,
            BestDelayMs = bestDelayMs,
            Decision = decision,
            Reason = reason,
            CorrelationUnavailableReason = null
        };
    }

    public static SelfSpeechCorrelationResult Analyze(
        ReadOnlySpan<float> micSamples,
        ReadOnlySpan<float> playbackReference,
        int sampleRate,
        SelfSpeechSuppressionOptions options)
    {
        if (!options.CorrelationDetectionEnabled
            || micSamples.IsEmpty
            || playbackReference.Length < micSamples.Length
            || sampleRate <= 0)
        {
            return Unavailable(CreateBaseResult(micSamples.Length, sampleRate, options, null), "Correlation unavailable because detection is disabled, samples are missing, or sample rate is invalid.");
        }

        var micEnergy = AudioEnergyCalculator.CalculateRms(micSamples);
        if (micEnergy < Math.Max(0.0, options.CorrelationMinMicEnergy))
        {
            return Unavailable(CreateBaseResult(micSamples.Length, sampleRate, options, null), "Correlation unavailable because mic energy is below threshold.");
        }

        var referenceEnergy = AudioEnergyCalculator.CalculateRms(playbackReference);
        if (referenceEnergy < Math.Max(0.0, options.CorrelationMinReferenceEnergy))
        {
            return Unavailable(
                CreateBaseResult(micSamples.Length, sampleRate, options, null) with
                {
                    ReferenceWindowAvailable = playbackReference.Length >= micSamples.Length,
                    ReferenceWindowEnergy = referenceEnergy,
                    ReferenceWindowSampleCount = Math.Min(playbackReference.Length, micSamples.Length),
                    MaxReferenceEnergySeen = referenceEnergy
                },
                "Correlation unavailable because playback reference energy is below threshold.");
        }

        var minDelaySamples = Math.Max(0, (int)Math.Round(sampleRate * options.CorrelationMinDelayMs / 1000.0));
        var maxDelaySamples = Math.Max(0, (int)Math.Round(sampleRate * options.CorrelationMaxDelayMs / 1000.0));
        var stepSamples = Math.Max(1, (int)Math.Round(sampleRate * Math.Max(1, options.CorrelationStepMs) / 1000.0));
        var availableDelaySamples = Math.Max(0, playbackReference.Length - micSamples.Length);
        var delayLimit = Math.Min(maxDelaySamples, availableDelaySamples);
        if (minDelaySamples > delayLimit)
        {
            return Unavailable(CreateBaseResult(micSamples.Length, sampleRate, options, null), "Correlation unavailable because playback reference does not cover the requested delay window.");
        }

        double bestScore = 0.0;
        var bestDelaySamples = minDelaySamples;

        for (var delay = minDelaySamples; delay <= delayLimit; delay += stepSamples)
        {
            var start = playbackReference.Length - micSamples.Length - delay;
            if (start < 0)
            {
                break;
            }

            var score = Math.Abs(CalculateNormalizedCorrelation(micSamples, playbackReference.Slice(start, micSamples.Length)));
            if (score > bestScore)
            {
                bestScore = score;
                bestDelaySamples = delay;
            }
        }

        var selfEchoThreshold = Math.Clamp(
            Math.Max(options.CorrelationMinScore, options.CorrelationSelfEchoThreshold),
            0.0,
            1.0);
        var decision = bestScore >= selfEchoThreshold
            ? SelfSpeechCorrelationDecision.SelfEcho
            : bestScore <= Math.Clamp(options.CorrelationLikelyUserThreshold, 0.0, 1.0)
                ? SelfSpeechCorrelationDecision.LikelyUser
                : SelfSpeechCorrelationDecision.WeakCorrelation;
        var reason = decision switch
        {
            SelfSpeechCorrelationDecision.SelfEcho => "Mic frame strongly correlates with delayed playback reference.",
            SelfSpeechCorrelationDecision.LikelyUser => "Mic frame has low correlation with playback reference.",
            _ => "Mic frame has weak or borderline correlation with playback reference."
        };

        return CreateBaseResult(micSamples.Length, sampleRate, options, null) with
        {
            IsAvailable = true,
            CorrelationScore = bestScore,
            BestDelayMs = bestDelaySamples * 1000.0 / sampleRate,
            Decision = decision,
            Reason = reason,
            ReferenceWindowAvailable = true,
            ReferenceWindowEnergy = referenceEnergy,
            ReferenceWindowSampleCount = micSamples.Length,
            NumberOfDelayWindowsChecked = 1 + ((delayLimit - minDelaySamples) / stepSamples),
            NumberOfDelayWindowsAvailable = 1 + ((delayLimit - minDelaySamples) / stepSamples),
            MaxReferenceEnergySeen = referenceEnergy
        };
    }

    private static SelfSpeechCorrelationResult CreateBaseResult(
        int micSampleCount,
        int micSampleRate,
        SelfSpeechSuppressionOptions options,
        PlaybackReferenceDebugSnapshot? snapshot)
    {
        return new SelfSpeechCorrelationResult
        {
            IsAvailable = false,
            CorrelationScore = null,
            BestDelayMs = null,
            Decision = SelfSpeechCorrelationDecision.Unavailable,
            Reason = "Correlation unavailable.",
            ReferenceWindowAvailable = false,
            ReferenceWindowEnergy = null,
            ReferenceWindowSampleCount = 0,
            RequestedMicSampleCount = micSampleCount,
            RequestedDelayMinMs = options.CorrelationMinDelayMs,
            RequestedDelayMaxMs = options.CorrelationMaxDelayMs,
            RequestedDelayStepMs = options.CorrelationStepMs,
            PlaybackRingBufferedSamples = snapshot?.BufferedSamples ?? 0,
            PlaybackRingCapacitySamples = snapshot?.CapacitySamples ?? 0,
            PlaybackRingBufferedMs = snapshot?.BufferedMilliseconds ?? 0.0,
            PlaybackTapSampleRate = snapshot?.SampleRate ?? micSampleRate,
            MicSampleRate = micSampleRate,
            SampleRateMatches = snapshot is null || snapshot.SampleRate == micSampleRate,
            PlaybackWritePosition = snapshot?.WritePosition ?? 0,
            NumberOfDelayWindowsChecked = 0,
            NumberOfDelayWindowsAvailable = 0,
            NumberOfDelayWindowsSkippedLowEnergy = 0,
            MaxReferenceEnergySeen = 0.0,
            CorrelationUnavailableReason = null,
            PlaybackReferenceSource = snapshot?.PlaybackReferenceSource,
            PlaybackReferenceIsConsumptionAligned = snapshot?.PlaybackReferenceIsConsumptionAligned ?? false,
            PlaybackConsumedSamplesTotal = snapshot?.PlaybackConsumedSamplesTotal ?? 0,
            ReferenceBufferedMs = snapshot?.ReferenceBufferedMilliseconds ?? 0.0,
            ReferenceNewestAgeMs = snapshot?.ReferenceNewestAgeMilliseconds,
            ReferenceOldestAgeMs = snapshot?.ReferenceOldestAgeMilliseconds,
            OutputReadSamples = snapshot?.LastOutputReadSamples ?? 0,
            OutputReadDurationMs = snapshot?.LastOutputReadDurationMilliseconds ?? 0.0,
            LastOutputReadAtUtc = snapshot?.LastOutputReadAtUtc
        };
    }

    private static SelfSpeechCorrelationResult Unavailable(SelfSpeechCorrelationResult result, string reason)
    {
        return result with
        {
            IsAvailable = false,
            CorrelationScore = null,
            BestDelayMs = null,
            Decision = SelfSpeechCorrelationDecision.Unavailable,
            Reason = reason,
            CorrelationUnavailableReason = reason
        };
    }

    private static double CalculateNormalizedCorrelation(
        ReadOnlySpan<float> first,
        ReadOnlySpan<float> second)
    {
        if (first.Length != second.Length || first.IsEmpty)
        {
            return 0.0;
        }

        double dot = 0.0;
        double firstEnergy = 0.0;
        double secondEnergy = 0.0;
        for (var index = 0; index < first.Length; index++)
        {
            var a = first[index];
            var b = second[index];
            dot += a * b;
            firstEnergy += a * a;
            secondEnergy += b * b;
        }

        var denominator = Math.Sqrt(firstEnergy * secondEnergy);
        return denominator <= 0.0000001
            ? 0.0
            : dot / denominator;
    }
}
