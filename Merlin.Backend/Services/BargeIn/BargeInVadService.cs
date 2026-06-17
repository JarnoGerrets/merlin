using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInVadService : IBargeInVadService
{
    private double _noiseFloor = 0.004;
    private DateTimeOffset? _lastTimestamp;
    private int _consecutiveSpeechMs;
    private bool _triggered;

    public VadFrameResult ProcessFrame(VadFrameInput input, BargeInOptions options)
    {
        var energy = CalculateRms(input.Samples.Span);
        var elapsedMs = CalculateElapsedMs(input);
        var threshold = Math.Max(options.VadEnergyThreshold, _noiseFloor * 3.0);
        var isSpeech = energy >= threshold;

        if (!isSpeech && options.VadUseAdaptiveNoiseFloor)
        {
            _noiseFloor = (_noiseFloor * 0.95) + (energy * 0.05);
        }

        if (isSpeech)
        {
            _consecutiveSpeechMs += elapsedMs;
        }
        else if (_consecutiveSpeechMs > 0)
        {
            _consecutiveSpeechMs = Math.Max(0, _consecutiveSpeechMs - elapsedMs);
        }

        var confidence = threshold <= 0
            ? 0.0
            : Math.Clamp((energy - threshold) / Math.Max(threshold, 0.0001), 0.0, 1.0);
        var isTriggered = !_triggered
            && _consecutiveSpeechMs >= Math.Max(options.VadMinSpeechMs, options.VadTriggerSpeechMs);

        if (isTriggered)
        {
            _triggered = true;
        }

        return new VadFrameResult
        {
            IsSpeech = isSpeech,
            IsTriggered = isTriggered,
            Energy = energy,
            NoiseFloor = _noiseFloor,
            Confidence = confidence,
            ConsecutiveSpeechMs = _consecutiveSpeechMs
        };
    }

    public void Reset()
    {
        _lastTimestamp = null;
        _consecutiveSpeechMs = 0;
        _triggered = false;
    }

    private int CalculateElapsedMs(VadFrameInput input)
    {
        if (_lastTimestamp is null)
        {
            _lastTimestamp = input.Timestamp;
            if (input.SampleRate <= 0)
            {
                return 10;
            }

            return Math.Max(1, (int)Math.Round(input.Samples.Length * 1000.0 / input.SampleRate));
        }

        var elapsed = Math.Max(1, (int)Math.Round((input.Timestamp - _lastTimestamp.Value).TotalMilliseconds));
        _lastTimestamp = input.Timestamp;
        return elapsed;
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
}
