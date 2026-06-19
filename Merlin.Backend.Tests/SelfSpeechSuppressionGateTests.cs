using Merlin.Backend.Configuration;
using Merlin.Backend.Services.BargeIn;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SelfSpeechSuppressionGateTests
{
    [Fact]
    public void Evaluate_Allows_WhenAssistantNotPlayingAndVadSaysSpeech()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(CreateInput(active: false, micEnergy: 0.03, playbackEnergy: 0.0), CreateOptions());

        Assert.Equal(SelfSpeechDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_Suppresses_WhenAssistantPlayingAndMicEnergyMatchesEcho()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(CreateInput(micEnergy: 0.07, playbackEnergy: 0.20), CreateOptions());

        Assert.Equal(SelfSpeechDecision.SuppressAsSelfEcho, result.Decision);
    }

    [Fact]
    public void Evaluate_Allows_WhenAssistantPlayingAndMicEnergyExceedsEcho()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(CreateInput(micEnergy: 0.16, playbackEnergy: 0.20), CreateOptions());

        Assert.Equal(SelfSpeechDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_SuppressesWeakVad_DuringPlaybackOnsetGrace()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(
            CreateInput(micEnergy: 0.03, playbackEnergy: 0.0, playbackAge: TimeSpan.FromMilliseconds(100)),
            CreateOptions());

        Assert.Equal(SelfSpeechDecision.SuppressAsSelfEcho, result.Decision);
    }

    [Fact]
    public void Evaluate_AllowsStrongUserSpeech_DuringPlaybackOnsetGrace()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(
            CreateInput(micEnergy: 0.12, playbackEnergy: 0.0, playbackAge: TimeSpan.FromMilliseconds(100)),
            CreateOptions());

        Assert.Equal(SelfSpeechDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_UncertainLiveDucking_DoesNotBecomeAllowDuringPlayback()
    {
        var gate = CreateGate();
        var options = CreateOptions();

        var first = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "live_ducking"), options);
        var second = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "live_ducking"), options);
        var third = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "live_ducking"), options);

        Assert.Equal(SelfSpeechDecision.Uncertain, first.Decision);
        Assert.Equal(SelfSpeechDecision.Uncertain, second.Decision);
        Assert.Equal(SelfSpeechDecision.Uncertain, third.Decision);
    }

    [Fact]
    public void Evaluate_UncertainNormalCapture_DoesNotBecomeAllowDuringPlayback()
    {
        var gate = CreateGate();
        var options = CreateOptions();

        var first = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "normal_capture"), options);
        var second = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "normal_capture"), options);
        var third = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "normal_capture"), options);

        Assert.Equal(SelfSpeechDecision.Uncertain, first.Decision);
        Assert.Equal(SelfSpeechDecision.Uncertain, second.Decision);
        Assert.Equal(SelfSpeechDecision.Uncertain, third.Decision);
    }

    [Fact]
    public void Evaluate_FastHardStop_AllowsOnlyAfterStricterUncertainPolicy()
    {
        var gate = CreateGate();
        var options = CreateOptions();
        options.SelfSpeechSuppression.FastHardStopUncertainFrames = 3;
        options.SelfSpeechSuppression.FastHardStopUncertainExtraMargin = 0.02;

        var first = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "fast_hard_stop_candidate"), options);
        var second = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "fast_hard_stop_candidate"), options);
        var third = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "fast_hard_stop_candidate"), options);

        Assert.Equal(SelfSpeechDecision.Uncertain, first.Decision);
        Assert.Equal(SelfSpeechDecision.Uncertain, second.Decision);
        Assert.Equal(SelfSpeechDecision.Allow, third.Decision);
    }

    [Fact]
    public void Evaluate_FastHardStop_DoesNotAllowPlainSelfEcho()
    {
        var gate = CreateGate();
        var options = CreateOptions();
        options.SelfSpeechSuppression.FastHardStopUncertainFrames = 2;
        options.SelfSpeechSuppression.FastHardStopUncertainExtraMargin = 0.08;

        var first = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "fast_hard_stop_candidate"), options);
        var second = gate.Evaluate(CreateInput(micEnergy: 0.10, playbackEnergy: 0.20, reason: "fast_hard_stop_candidate"), options);

        Assert.Equal(SelfSpeechDecision.Uncertain, first.Decision);
        Assert.Equal(SelfSpeechDecision.Uncertain, second.Decision);
    }

    [Fact]
    public void Evaluate_DisabledGate_AllowsExistingBehavior()
    {
        var gate = CreateGate();
        var options = CreateOptions();
        options.SelfSpeechSuppression.Enabled = false;

        var result = gate.Evaluate(CreateInput(micEnergy: 0.01, playbackEnergy: 0.20), options);

        Assert.Equal(SelfSpeechDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_Suppresses_WhenCorrelationSaysSelfEcho()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(
            CreateInput(
                micEnergy: 0.12,
                playbackEnergy: 0.20,
                reason: "live_ducking",
                correlationScore: 0.86,
                bestDelayMs: 40,
                correlationDecision: SelfSpeechCorrelationDecision.SelfEcho),
            CreateOptions());

        Assert.Equal(SelfSpeechDecision.SuppressAsSelfEcho, result.Decision);
        Assert.Equal(SelfSpeechCorrelationDecision.SelfEcho, result.CorrelationDecision);
        Assert.Equal(0.86, result.CorrelationScore);
        Assert.Equal(40, result.BestDelayMs);
    }

    [Theory]
    [InlineData("live_ducking")]
    [InlineData("normal_capture")]
    [InlineData("vad_triggered_capture")]
    public void Evaluate_CorrelationSelfEcho_SuppressesDuckingAndCaptureEvenWhenEnergyWouldAllow(string reason)
    {
        var gate = CreateGate();

        var result = gate.Evaluate(
            CreateInput(
                micEnergy: 0.19,
                playbackEnergy: 0.04,
                reason: reason,
                correlationScore: 0.82,
                bestDelayMs: 180,
                correlationDecision: SelfSpeechCorrelationDecision.SelfEcho,
                correlationAvailable: true),
            CreateOptions());

        Assert.Equal(SelfSpeechDecision.SuppressAsSelfEcho, result.Decision);
    }

    [Fact]
    public void Evaluate_LowCorrelationHighEnergy_AllowsLikelyUserSpeech()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(
            CreateInput(
                micEnergy: 0.19,
                playbackEnergy: 0.04,
                reason: "live_ducking",
                correlationScore: 0.12,
                bestDelayMs: 30,
                correlationDecision: SelfSpeechCorrelationDecision.LikelyUser,
                correlationAvailable: true),
            CreateOptions());

        Assert.Equal(SelfSpeechDecision.Allow, result.Decision);
    }

    [Fact]
    public void Evaluate_CorrelationUnavailable_FallsBackToEnergyPolicy()
    {
        var gate = CreateGate();

        var result = gate.Evaluate(
            CreateInput(
                micEnergy: 0.16,
                playbackEnergy: 0.20,
                reason: "live_ducking",
                correlationDecision: SelfSpeechCorrelationDecision.Unavailable,
                correlationAvailable: false),
            CreateOptions());

        Assert.Equal(SelfSpeechDecision.Allow, result.Decision);
    }

    [Fact]
    public void CorrelationDetector_ReturnsSelfEcho_ForMatchingReference()
    {
        var samples = Enumerable.Range(0, 160)
            .Select(index => (float)Math.Sin(index / 8.0))
            .ToArray();
        var reference = new float[320];
        Array.Copy(samples, 0, reference, 80, samples.Length);

        var result = SelfSpeechCorrelationDetector.Analyze(
            samples,
            reference,
            16000,
            CreateOptions().SelfSpeechSuppression);

        Assert.Equal(SelfSpeechCorrelationDecision.SelfEcho, result.Decision);
        Assert.True(result.CorrelationScore >= 0.70);
        Assert.Equal(5, result.BestDelayMs);
    }

    [Fact]
    public void CorrelationDetector_FindsBestDelayWithinSearchWindow()
    {
        var samples = Enumerable.Range(0, 160)
            .Select(index => (float)((index * 17 % 29) / 29.0 - 0.5))
            .ToArray();
        var reference = new float[640];
        Array.Copy(samples, 0, reference, 320, samples.Length);
        var options = CreateOptions().SelfSpeechSuppression;
        options.CorrelationStepMs = 10;

        var result = SelfSpeechCorrelationDetector.Analyze(samples, reference, 16000, options);

        Assert.Equal(SelfSpeechCorrelationDecision.SelfEcho, result.Decision);
        Assert.Equal(10, result.BestDelayMs);
    }

    [Fact]
    public void CorrelationDetector_ReturnsLikelyUser_ForLowCorrelation()
    {
        var mic = Enumerable.Repeat(0.1f, 160).ToArray();
        var reference = Enumerable.Range(0, 320)
            .Select(index => index % 2 == 0 ? 0.1f : -0.1f)
            .ToArray();

        var result = SelfSpeechCorrelationDetector.Analyze(
            mic,
            reference,
            16000,
            CreateOptions().SelfSpeechSuppression);

        Assert.Equal(SelfSpeechCorrelationDecision.LikelyUser, result.Decision);
    }

    [Fact]
    public void CorrelationDetector_ReturnsUnavailable_WhenReferenceEnergyTooLow()
    {
        var mic = Enumerable.Repeat(0.1f, 160).ToArray();
        var reference = new float[320];

        var result = SelfSpeechCorrelationDetector.Analyze(
            mic,
            reference,
            16000,
            CreateOptions().SelfSpeechSuppression);

        Assert.False(result.IsAvailable);
        Assert.Equal(SelfSpeechCorrelationDecision.Unavailable, result.Decision);
        Assert.Contains("reference energy", result.Reason);
    }

    [Fact]
    public void CorrelationDetector_ReturnsUnavailable_WhenMicEnergyTooLow()
    {
        var mic = new float[160];
        var reference = Enumerable.Repeat(0.1f, 320).ToArray();

        var result = SelfSpeechCorrelationDetector.Analyze(
            mic,
            reference,
            16000,
            CreateOptions().SelfSpeechSuppression);

        Assert.False(result.IsAvailable);
        Assert.Equal(SelfSpeechCorrelationDecision.Unavailable, result.Decision);
        Assert.Contains("mic energy", result.Reason);
    }

    [Fact]
    public void Evaluate_WritesDiagnosticsFile_WhenEnabled()
    {
        using var temp = new TempDirectory();
        var writer = new SelfSpeechGateDiagnosticsWriter(temp.Path, NullLogger<SelfSpeechGateDiagnosticsWriter>.Instance);
        var gate = CreateGate(writer);
        var options = CreateOptions();
        options.SelfSpeechSuppression.LogDecisions = true;
        options.SelfSpeechSuppression.DiagnosticsFileEnabled = true;
        options.SelfSpeechSuppression.DiagnosticsFilePath = "self-speech.jsonl";

        _ = gate.Evaluate(CreateInput(micEnergy: 0.07, playbackEnergy: 0.20, reason: "live_ducking"), options);

        var path = Path.Combine(temp.Path, "self-speech.jsonl");
        Assert.True(File.Exists(path));
        var line = File.ReadLines(path).Single();
        Assert.Contains("\"inputReason\":\"live_ducking\"", line);
        Assert.Contains("\"decision\":\"SuppressAsSelfEcho\"", line);
        Assert.Contains("\"micEnergy\"", line);
        Assert.Contains("\"playbackEnergy\"", line);
        Assert.Contains("\"estimatedEchoEnergy\"", line);
        Assert.Contains("\"echoLeakageMultiplier\"", line);
        Assert.Contains("\"sustainedUncertainFrames\"", line);
        Assert.Contains("\"correlationScore\"", line);
        Assert.Contains("\"bestDelayMs\"", line);
        Assert.Contains("\"correlationDecision\"", line);
        Assert.Contains("\"correlationAvailable\"", line);
        Assert.Contains("\"correlationMinScore\"", line);
        Assert.Contains("\"correlationMinDelayMs\"", line);
        Assert.Contains("\"correlationMaxDelayMs\"", line);
        Assert.Contains("\"correlationDelayStepMs\"", line);
        Assert.Contains("\"correlationReason\"", line);
        Assert.Contains("\"referenceWindowAvailable\"", line);
        Assert.Contains("\"referenceWindowEnergy\"", line);
        Assert.Contains("\"referenceWindowSampleCount\"", line);
        Assert.Contains("\"requestedMicSampleCount\"", line);
        Assert.Contains("\"requestedDelayMinMs\"", line);
        Assert.Contains("\"requestedDelayMaxMs\"", line);
        Assert.Contains("\"requestedDelayStepMs\"", line);
        Assert.Contains("\"playbackRingBufferedSamples\"", line);
        Assert.Contains("\"playbackRingCapacitySamples\"", line);
        Assert.Contains("\"playbackRingBufferedMs\"", line);
        Assert.Contains("\"playbackTapSampleRate\"", line);
        Assert.Contains("\"micSampleRate\"", line);
        Assert.Contains("\"sampleRateMatches\"", line);
        Assert.Contains("\"numberOfDelayWindowsChecked\"", line);
        Assert.Contains("\"numberOfDelayWindowsAvailable\"", line);
        Assert.Contains("\"numberOfDelayWindowsSkippedLowEnergy\"", line);
        Assert.Contains("\"maxReferenceEnergySeen\"", line);
        Assert.Contains("\"correlationUnavailableReason\"", line);
        Assert.Contains("\"playbackReferenceSource\"", line);
        Assert.Contains("\"playbackReferenceIsConsumptionAligned\"", line);
        Assert.Contains("\"playbackConsumedSamplesTotal\"", line);
        Assert.Contains("\"referenceBufferedMs\"", line);
        Assert.Contains("\"referenceNewestAgeMs\"", line);
        Assert.Contains("\"referenceOldestAgeMs\"", line);
        Assert.Contains("\"outputReadSamples\"", line);
        Assert.Contains("\"outputReadDurationMs\"", line);
        Assert.Contains("\"lastOutputReadAtUtc\"", line);
    }

    [Fact]
    public void Evaluate_DiagnosticsFailure_DoesNotCrashGate()
    {
        var gate = CreateGate(new ThrowingDiagnosticsWriter());
        var options = CreateOptions();
        options.SelfSpeechSuppression.LogDecisions = true;
        options.SelfSpeechSuppression.DiagnosticsFileEnabled = true;

        var result = gate.Evaluate(CreateInput(micEnergy: 0.07, playbackEnergy: 0.20), options);

        Assert.Equal(SelfSpeechDecision.SuppressAsSelfEcho, result.Decision);
    }

    [Fact]
    public void DiagnosticsWriter_RespectsEnabledFalse()
    {
        using var temp = new TempDirectory();
        var writer = new SelfSpeechGateDiagnosticsWriter(temp.Path, NullLogger<SelfSpeechGateDiagnosticsWriter>.Instance);
        var gate = CreateGate(writer);
        var options = CreateOptions();
        options.SelfSpeechSuppression.LogDecisions = true;
        options.SelfSpeechSuppression.DiagnosticsFileEnabled = false;
        options.SelfSpeechSuppression.DiagnosticsFilePath = "self-speech.jsonl";

        _ = gate.Evaluate(CreateInput(micEnergy: 0.07, playbackEnergy: 0.20), options);

        Assert.False(File.Exists(Path.Combine(temp.Path, "self-speech.jsonl")));
    }

    private static SelfSpeechSuppressionGate CreateGate(ISelfSpeechGateDiagnosticsWriter? writer = null)
    {
        return new SelfSpeechSuppressionGate(
            NullLogger<SelfSpeechSuppressionGate>.Instance,
            writer ?? new NoOpDiagnosticsWriter());
    }

    private static BargeInOptions CreateOptions()
    {
        return new BargeInOptions
        {
            VadEnergyThreshold = 0.015,
            SelfSpeechSuppression = new SelfSpeechSuppressionOptions
            {
                Enabled = true,
                SuppressDuringPlayback = true,
                PlaybackOnsetGraceMs = 250,
                EchoLeakageMultiplier = 0.35,
                EchoMargin = 0.02,
                UserSpeechRatio = 1.8,
                UserSpeechMargin = 0.05,
                RequireSustainedUserSpeechFrames = 2,
                DiagnosticsFileEnabled = false,
                AllowSustainedUncertainForDucking = false,
                AllowSustainedUncertainForCapture = false,
                AllowSustainedUncertainForFastHardStop = true,
                FastHardStopUncertainFrames = 6,
                FastHardStopUncertainExtraMargin = 0.04,
                CorrelationDetectionEnabled = true,
                CorrelationMinScore = 0.65,
                CorrelationSelfEchoThreshold = 0.70,
                CorrelationLikelyUserThreshold = 0.35,
                CorrelationMinDelayMs = 0,
                CorrelationMaxDelayMs = 250,
                CorrelationStepMs = 5,
                CorrelationMinReferenceEnergy = 0.005,
                CorrelationMinMicEnergy = 0.005
            }
        };
    }

    private static SelfSpeechGateInput CreateInput(
        bool active = true,
        double micEnergy = 0.05,
        double playbackEnergy = 0.10,
        TimeSpan? playbackAge = null,
        string reason = "test",
        double? correlationScore = null,
        double? bestDelayMs = null,
        string? correlationDecision = null,
        bool? correlationAvailable = null,
        string? correlationReason = null)
    {
        return new SelfSpeechGateInput
        {
            AssistantPlaybackActive = active,
            MicEnergy = micEnergy,
            PlaybackEnergy = playbackEnergy,
            CurrentPlaybackEnergy = playbackEnergy,
            RecentPlaybackEnergy = playbackEnergy,
            AecVerified = true,
            VadSaysSpeech = true,
            VadConfidence = 0.8,
            Timestamp = DateTimeOffset.UtcNow,
            PlaybackAge = playbackAge ?? TimeSpan.FromSeconds(1),
            Reason = reason,
            CorrelationId = "correlation-1",
            CorrelationScore = correlationScore,
            BestDelayMs = bestDelayMs,
            CorrelationDecision = correlationDecision ?? SelfSpeechCorrelationDecision.Unavailable,
            CorrelationAvailable = correlationAvailable ?? false,
            CorrelationReason = correlationReason ?? "test",
            ReferenceWindowAvailable = false,
            ReferenceWindowEnergy = null,
            ReferenceWindowSampleCount = 0,
            RequestedMicSampleCount = 160,
            RequestedDelayMinMs = 0,
            RequestedDelayMaxMs = 250,
            RequestedDelayStepMs = 10,
            PlaybackRingBufferedSamples = 0,
            PlaybackRingCapacitySamples = 32000,
            PlaybackRingBufferedMs = 0,
            PlaybackTapSampleRate = 16000,
            MicSampleRate = 16000,
            SampleRateMatches = true,
            PlaybackWritePosition = 0,
            NumberOfDelayWindowsChecked = 0,
            NumberOfDelayWindowsAvailable = 0,
            NumberOfDelayWindowsSkippedLowEnergy = 0,
            MaxReferenceEnergySeen = 0,
            CorrelationUnavailableReason = correlationReason ?? "test",
            PlaybackReferenceSource = "output_read",
            PlaybackReferenceIsConsumptionAligned = true,
            PlaybackConsumedSamplesTotal = 480,
            ReferenceBufferedMs = 10,
            ReferenceNewestAgeMs = 2,
            ReferenceOldestAgeMs = 12,
            OutputReadSamples = 480,
            OutputReadDurationMs = 10,
            LastOutputReadAtUtc = DateTimeOffset.UtcNow
        };
    }

    private sealed class NoOpDiagnosticsWriter : ISelfSpeechGateDiagnosticsWriter
    {
        public void Write(SelfSpeechGateDiagnosticEntry entry, BargeInOptions options)
        {
        }
    }

    private sealed class ThrowingDiagnosticsWriter : ISelfSpeechGateDiagnosticsWriter
    {
        public void Write(SelfSpeechGateDiagnosticEntry entry, BargeInOptions options)
        {
            throw new IOException("test failure");
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "merlin-self-speech-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
