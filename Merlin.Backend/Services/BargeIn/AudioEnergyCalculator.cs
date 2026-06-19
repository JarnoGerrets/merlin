namespace Merlin.Backend.Services.BargeIn;

public static class AudioEnergyCalculator
{
    public static double CalculateRms(ReadOnlySpan<float> samples)
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

    public static double CalculatePcm16Rms(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length < 2)
        {
            return 0.0;
        }

        double sumSquares = 0.0;
        var sampleCount = 0;
        for (var index = 0; index + 1 < pcm.Length; index += 2)
        {
            var sample = (short)(pcm[index] | (pcm[index + 1] << 8));
            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
            sampleCount++;
        }

        return sampleCount <= 0
            ? 0.0
            : Math.Sqrt(sumSquares / sampleCount);
    }
}
