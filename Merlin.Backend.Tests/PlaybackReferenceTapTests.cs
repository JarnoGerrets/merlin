using Merlin.Backend.Configuration;
using Merlin.Backend.Services.BargeIn;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class PlaybackReferenceTapTests
{
    [Fact]
    public void QueuedAudio_DoesNotPopulateReference_UntilOutputRead()
    {
        var tap = CreateTap(16000);
        var buffered = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
        var provider = new PlaybackReferenceWaveProvider(buffered, tap, "queued-test");
        var samples = Enumerable.Range(0, 100).Select(index => (short)(index * 100)).ToArray();

        buffered.AddSamples(ToPcm(samples), 0, samples.Length * 2);

        Assert.Equal(0, tap.GetDebugSnapshot().BufferedSamples);

        var output = new byte[20];
        var read = provider.Read(output, 0, output.Length);

        Assert.Equal(output.Length, read);
        Assert.Equal(10, tap.GetDebugSnapshot().BufferedSamples);
    }

    [Fact]
    public void OutputReads_AdvanceReference_ByExactSamplesRead()
    {
        var tap = CreateTap(16000);
        var buffered = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
        var provider = new PlaybackReferenceWaveProvider(buffered, tap, "read-test");
        var samples = Enumerable.Range(0, 100).Select(index => (short)(index * 100)).ToArray();
        buffered.AddSamples(ToPcm(samples), 0, samples.Length * 2);

        _ = provider.Read(new byte[16], 0, 16);
        _ = provider.Read(new byte[24], 0, 24);

        var snapshot = tap.GetDebugSnapshot();
        Assert.Equal(20, snapshot.BufferedSamples);
        Assert.Equal(20, snapshot.PlaybackConsumedSamplesTotal);
        Assert.Equal(12, snapshot.LastOutputReadSamples);
        Assert.Equal("output_read", snapshot.PlaybackReferenceSource);
        Assert.True(snapshot.PlaybackReferenceIsConsumptionAligned);
    }

    [Fact]
    public void ReferenceRing_ContainsOnlyConsumedMidChunkSamples()
    {
        var tap = CreateTap(16000);
        var buffered = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
        var provider = new PlaybackReferenceWaveProvider(buffered, tap, "mid-chunk-test");
        var samples = Enumerable.Range(0, 1000).Select(index => (short)(index * 10)).ToArray();
        buffered.AddSamples(ToPcm(samples), 0, samples.Length * 2);

        _ = provider.Read(new byte[200 * 2], 0, 200 * 2);
        var consumedWindow = new float[8];

        Assert.True(tap.TryGetReferenceWindow(0, consumedWindow.Length, consumedWindow));
        AssertWindow([192, 193, 194, 195, 196, 197, 198, 199], consumedWindow, scale: 10);
        Assert.DoesNotContain(consumedWindow, sample => Math.Abs(sample - (500 * 10 / 32768.0f)) < 0.00001f);
    }

    [Fact]
    public void MicMatchingConsumedMidChunkAudio_CorrelatesHigh_ButFutureAudioDoesNot()
    {
        var options = CreateOptions();
        var tap = CreateTap(16000, options);
        var buffered = new BufferedWaveProvider(new WaveFormat(16000, 16, 1));
        var provider = new PlaybackReferenceWaveProvider(buffered, tap, "mid-correlation-test");
        var consumed = Enumerable.Range(0, 160)
            .Select(index => (float)Math.Sin(index / 7.0) * 0.2f)
            .ToArray();
        var future = Enumerable.Range(0, 160)
            .Select(index => (float)Math.Cos(index / 5.0) * 0.2f)
            .ToArray();
        var allPcm = consumed.Concat(future).Select(ToPcm16).ToArray();
        buffered.AddSamples(ToPcm(allPcm), 0, allPcm.Length * 2);

        _ = provider.Read(new byte[160 * 2], 0, 160 * 2);

        var consumedResult = SelfSpeechCorrelationDetector.Analyze(consumed, 16000, tap, options.SelfSpeechSuppression);
        var futureResult = SelfSpeechCorrelationDetector.Analyze(future, 16000, tap, options.SelfSpeechSuppression);

        Assert.Equal(SelfSpeechCorrelationDecision.SelfEcho, consumedResult.Decision);
        Assert.NotEqual(SelfSpeechCorrelationDecision.SelfEcho, futureResult.Decision);
    }

    [Fact]
    public void TryGetReferenceWindow_ReturnsLatestSamples_ForZeroDelay()
    {
        var tap = CreateTap(16000);
        PushSamples(tap, Enumerable.Range(0, 32).Select(index => (short)(index * 100)).ToArray(), 16000);
        var window = new float[4];

        var ok = tap.TryGetReferenceWindow(0, window.Length, window);

        Assert.True(ok);
        AssertWindow([28, 29, 30, 31], window);
    }

    [Fact]
    public void TryGetReferenceWindow_ReturnsDelayedSamples_ForKnownDelay()
    {
        var tap = CreateTap(16000);
        PushSamples(tap, Enumerable.Range(0, 400).Select(index => (short)(index * 20)).ToArray(), 16000);
        var window = new float[8];

        var ok = tap.TryGetReferenceWindow(10, window.Length, window);

        Assert.True(ok);
        AssertWindow([232, 233, 234, 235, 236, 237, 238, 239], window, scale: 20);
    }

    [Fact]
    public void TryGetReferenceWindow_HandlesWraparound()
    {
        var tap = CreateTap(8000);
        PushSamples(tap, Enumerable.Range(0, 16020).Select(index => (short)(index % 3000)).ToArray(), 8000);
        var window = new float[5];

        var ok = tap.TryGetReferenceWindow(0, window.Length, window);

        Assert.True(ok);
        AssertWindow([1015, 1016, 1017, 1018, 1019], window, scale: 1);
    }

    [Fact]
    public void TryGetReferenceWindow_ReturnsFalse_WhenNotEnoughHistory()
    {
        var tap = CreateTap(16000);
        PushSamples(tap, Enumerable.Range(0, 100).Select(index => (short)index).ToArray(), 16000);
        var window = new float[20];

        var ok = tap.TryGetReferenceWindow(10, window.Length, window);

        Assert.False(ok);
    }

    [Fact]
    public void ReferenceWindowEnergy_MatchesKnownSignal()
    {
        var tap = CreateTap(16000);
        PushSamples(tap, Enumerable.Repeat((short)3277, 200).ToArray(), 16000);
        var window = new float[160];

        var ok = tap.TryGetReferenceWindow(0, window.Length, window);

        Assert.True(ok);
        Assert.InRange(AudioEnergyCalculator.CalculateRms(window), 0.099, 0.101);
    }

    [Fact]
    public void CorrelationDetector_ReturnsHighCorrelation_ForDelayedPlaybackCopy()
    {
        var options = CreateOptions();
        var tap = CreateTap(16000, options);
        var mic = Enumerable.Range(0, 160)
            .Select(index => (float)Math.Sin(index / 9.0) * 0.2f)
            .ToArray();
        var playback = Enumerable.Repeat(0f, 160)
            .Concat(mic)
            .Concat(Enumerable.Repeat(0.03f, 160))
            .Select(ToPcm16)
            .ToArray();
        PushSamples(tap, playback, 16000);

        var result = SelfSpeechCorrelationDetector.Analyze(mic, 16000, tap, options.SelfSpeechSuppression);

        Assert.True(result.IsAvailable);
        Assert.Equal(SelfSpeechCorrelationDecision.SelfEcho, result.Decision);
        Assert.True(result.CorrelationScore >= 0.70);
        Assert.Equal(10, result.BestDelayMs);
        Assert.True(result.ReferenceWindowAvailable);
        Assert.Equal(160, result.ReferenceWindowSampleCount);
    }

    [Fact]
    public void CorrelationDetector_ReturnsLowCorrelation_ForUnrelatedMicSignal()
    {
        var options = CreateOptions();
        var tap = CreateTap(16000, options);
        var mic = Enumerable.Repeat(0.1f, 160).ToArray();
        var playback = Enumerable.Range(0, 480)
            .Select(index => index % 2 == 0 ? (short)3277 : (short)-3277)
            .ToArray();
        PushSamples(tap, playback, 16000);

        var result = SelfSpeechCorrelationDetector.Analyze(mic, 16000, tap, options.SelfSpeechSuppression);

        Assert.True(result.IsAvailable);
        Assert.Equal(SelfSpeechCorrelationDecision.LikelyUser, result.Decision);
    }

    [Fact]
    public void CorrelationDetector_ReturnsUnavailableReason_ForNoHistory()
    {
        var options = CreateOptions();
        var tap = CreateTap(16000, options);
        var mic = Enumerable.Repeat(0.1f, 160).ToArray();

        var result = SelfSpeechCorrelationDetector.Analyze(mic, 16000, tap, options.SelfSpeechSuppression);

        Assert.False(result.IsAvailable);
        Assert.Contains("enough history", result.CorrelationUnavailableReason);
        Assert.Equal(0, result.NumberOfDelayWindowsAvailable);
    }

    [Fact]
    public void CorrelationDetector_ReturnsUnavailableReason_ForLowReferenceEnergy()
    {
        var options = CreateOptions();
        var tap = CreateTap(16000, options);
        PushSamples(tap, new short[480], 16000);
        var mic = Enumerable.Repeat(0.1f, 160).ToArray();

        var result = SelfSpeechCorrelationDetector.Analyze(mic, 16000, tap, options.SelfSpeechSuppression);

        Assert.False(result.IsAvailable);
        Assert.Contains("below threshold", result.CorrelationUnavailableReason);
        Assert.True(result.NumberOfDelayWindowsSkippedLowEnergy > 0);
    }

    [Fact]
    public void CorrelationDetector_ReturnsUnavailableReason_ForSampleRateMismatch()
    {
        var options = CreateOptions();
        var tap = CreateTap(16000, options);
        PushSamples(tap, Enumerable.Repeat((short)3277, 480).ToArray(), 16000);
        var mic = Enumerable.Repeat(0.1f, 160).ToArray();

        var result = SelfSpeechCorrelationDetector.Analyze(mic, 48000, tap, options.SelfSpeechSuppression);

        Assert.False(result.IsAvailable);
        Assert.False(result.SampleRateMatches);
        Assert.Contains("sample rate", result.CorrelationUnavailableReason);
    }

    private static PlaybackReferenceTap CreateTap(int sampleRate, BargeInOptions? options = null)
    {
        options ??= CreateOptions();
        options.AecSampleRate = sampleRate;
        return new PlaybackReferenceTap(
            new BargeInCoordinatorTests.NoOpBargeInDiagnosticsLogger(),
            new TestOptionsMonitor<BargeInOptions>(options));
    }

    private static BargeInOptions CreateOptions()
    {
        return new BargeInOptions
        {
            AecSampleRate = 16000,
            SelfSpeechSuppression = new SelfSpeechSuppressionOptions
            {
                CorrelationDetectionEnabled = true,
                CorrelationMinScore = 0.65,
                CorrelationSelfEchoThreshold = 0.70,
                CorrelationLikelyUserThreshold = 0.35,
                CorrelationMinDelayMs = 0,
                CorrelationMaxDelayMs = 50,
                CorrelationStepMs = 10,
                CorrelationMinReferenceEnergy = 0.005,
                CorrelationMinMicEnergy = 0.005
            }
        };
    }

    private static void PushSamples(PlaybackReferenceTap tap, IReadOnlyList<short> samples, int sampleRate)
    {
        var pcm = ToPcm(samples);
        tap.PushPcm16Reference(pcm, sampleRate, 1, "test-correlation");
    }

    private static byte[] ToPcm(IReadOnlyList<short> samples)
    {
        var pcm = new byte[samples.Count * 2];
        for (var index = 0; index < samples.Count; index++)
        {
            pcm[index * 2] = (byte)(samples[index] & 0xff);
            pcm[index * 2 + 1] = (byte)((samples[index] >> 8) & 0xff);
        }

        return pcm;
    }

    private static short ToPcm16(float sample)
    {
        return (short)Math.Clamp((int)Math.Round(sample * 32768.0f), short.MinValue, short.MaxValue);
    }

    private static void AssertWindow(IReadOnlyList<int> expected, IReadOnlyList<float> actual, int scale = 100)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var index = 0; index < expected.Count; index++)
        {
            Assert.Equal(expected[index] * scale / 32768.0f, actual[index], precision: 5);
        }
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public TestOptionsMonitor(T value)
        {
            CurrentValue = value;
        }

        public T CurrentValue { get; }

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
