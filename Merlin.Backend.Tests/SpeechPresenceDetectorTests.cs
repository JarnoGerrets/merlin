using Merlin.Backend.Services.SpeechPresence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpeechPresenceDetectorTests
{
    [Fact]
    public void Evaluate_WhenNoSpeechEvidence_ReturnsNo()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            RawMicRms = 0.001,
            RawMicPeak = 0.002,
            EchoReducedRms = 0.001,
            EchoReducedPeak = 0.002,
            VadConfidence = 0.05,
            VadSpeechDetected = false
        });

        Assert.Equal(SpeechPresenceState.No, result.State);
        Assert.False(result.ShouldYieldPlayback);
    }

    [Fact]
    public void Evaluate_WhenStrongSelfEchoAndWeakResidual_ReturnsNo()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            RawMicRms = 0.04,
            RawMicPeak = 0.05,
            EchoReducedRms = 0.002,
            EchoReducedPeak = 0.003,
            PlaybackReferenceRms = 0.08,
            PlaybackReferencePeak = 0.10,
            VadConfidence = 0.80,
            VadSpeechDetected = true,
            PlaybackCorrelationScore = 0.90,
            StrongSelfEchoEvidence = true
        });

        Assert.Equal(SpeechPresenceState.No, result.State);
        Assert.False(result.ShouldYieldPlayback);
        Assert.Contains("self_echo", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenFrame3294ShapeIsSelfEchoContaminatedResidual_DoesNotYield()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            StrongSelfEchoEvidence = true,
            PlaybackCorrelationScore = 0.574149,
            RawMicRms = 0.017154,
            EchoReducedRms = 0.021361,
            PlaybackReferenceRms = 0.058292,
            VadConfidence = 0.424097,
            VadSpeechDetected = true
        });

        Assert.Equal(SpeechPresenceState.No, result.State);
        Assert.False(result.IsUserSpeaking);
        Assert.False(result.ShouldYieldPlayback);
        Assert.Equal("self_echo_contaminated_residual", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenFrame76023ShapeIsSelfEchoContaminatedResidual_DoesNotYield()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            StrongSelfEchoEvidence = true,
            PlaybackCorrelationScore = 0.877365,
            RawMicRms = 0.0159,
            EchoReducedRms = 0.0165,
            VadConfidence = 0.0986,
            VadSpeechDetected = true
        });

        Assert.Equal(SpeechPresenceState.No, result.State);
        Assert.False(result.IsUserSpeaking);
        Assert.False(result.ShouldYieldPlayback);
        Assert.Equal("self_echo_contaminated_residual", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenMarkerFirstYesShapeDuringPlayback_StillYields()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            StrongSelfEchoEvidence = false,
            PlaybackCorrelationScore = 0.4136,
            RawMicRms = 0.0313,
            EchoReducedRms = 0.0166,
            PlaybackReferenceRms = 0.0437,
            VadConfidence = 0.1053,
            VadSpeechDetected = true
        });

        Assert.True(result.State is SpeechPresenceState.Maybe or SpeechPresenceState.Yes);
        Assert.True(result.IsUserSpeaking);
        Assert.True(result.ShouldYieldPlayback);
        Assert.Equal("residual_speech_detected", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenClearNearEndSpeechHasModerateCorrelationAndStrongEcho_StillYields()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            StrongSelfEchoEvidence = true,
            PlaybackCorrelationScore = 0.60,
            RawMicRms = 0.075,
            EchoReducedRms = 0.040,
            PlaybackReferenceRms = 0.075,
            VadConfidence = 0.95,
            VadSpeechDetected = true
        });

        Assert.True(result.State is SpeechPresenceState.Maybe or SpeechPresenceState.Yes);
        Assert.True(result.IsUserSpeaking);
        Assert.True(result.ShouldYieldPlayback);
        Assert.Equal("residual_speech_detected", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenCorrelationAtContaminationThresholdWithoutStrongEcho_StillYields()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            StrongSelfEchoEvidence = false,
            PlaybackCorrelationScore = 0.60,
            RawMicRms = 0.017,
            EchoReducedRms = 0.021,
            PlaybackReferenceRms = 0.058,
            VadConfidence = 0.42,
            VadSpeechDetected = true
        });

        Assert.True(result.State is SpeechPresenceState.Maybe or SpeechPresenceState.Yes);
        Assert.True(result.IsUserSpeaking);
        Assert.True(result.ShouldYieldPlayback);
        Assert.Equal("residual_speech_detected", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenPossibleNearEndSpeechDuringPlayback_ReturnsMaybeAndWouldYield()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            RawMicRms = 0.02,
            RawMicPeak = 0.04,
            EchoReducedRms = 0.002,
            EchoReducedPeak = 0.004,
            PlaybackReferenceRms = 0.05,
            VadConfidence = 0.70,
            VadSpeechDetected = true,
            StrongSelfEchoEvidence = false
        });

        Assert.Equal(SpeechPresenceState.Maybe, result.State);
        Assert.True(result.IsUserSpeaking);
        Assert.True(result.ShouldYieldPlayback);
        Assert.Equal("possible_near_end_speech", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenResidualSpeechDetectedDuringPlayback_ReturnsSpeechAndWouldYield()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            RawMicRms = 0.03,
            RawMicPeak = 0.05,
            EchoReducedRms = 0.02,
            EchoReducedPeak = 0.04,
            PlaybackReferenceRms = 0.05,
            VadConfidence = 0.80,
            VadSpeechDetected = true,
            StrongSelfEchoEvidence = false
        });

        Assert.True(result.State is SpeechPresenceState.Maybe or SpeechPresenceState.Yes);
        Assert.True(result.IsUserSpeaking);
        Assert.True(result.ShouldYieldPlayback);
        Assert.Equal("residual_speech_detected", result.Reason);
    }

    [Fact]
    public void Evaluate_WhenClearSpeechAndPlaybackInactive_DoesNotYield()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = false,
            RawMicRms = 0.03,
            RawMicPeak = 0.05,
            EchoReducedRms = 0.02,
            EchoReducedPeak = 0.04,
            VadConfidence = 0.85,
            VadSpeechDetected = true
        });

        Assert.True(result.State is SpeechPresenceState.Maybe or SpeechPresenceState.Yes);
        Assert.True(result.IsUserSpeaking);
        Assert.False(result.ShouldYieldPlayback);
    }

    [Fact]
    public void Evaluate_WhenMaybeDuringPlayback_IsEnoughForYield()
    {
        var detector = CreateDetector();

        var result = detector.Evaluate(new SpeechPresenceEvidence
        {
            AssistantPlaybackActive = true,
            RawMicRms = 0.012,
            RawMicPeak = 0.02,
            EchoReducedRms = 0.001,
            EchoReducedPeak = 0.002,
            VadConfidence = 0.45,
            VadSpeechDetected = true
        });

        Assert.Equal(SpeechPresenceState.Maybe, result.State);
        Assert.True(result.ShouldYieldPlayback);
    }

    [Fact]
    public void PublicDetectorSurface_DoesNotContainIntentClassificationVocabulary()
    {
        var forbidden = new[] { "HardStop", "Correction", "NewRequest", "Command", "Clarification" };
        var publicNames = typeof(SpeechPresenceDetector).GetMethods()
            .Select(method => method.Name)
            .Concat(typeof(SpeechPresenceState).GetEnumNames())
            .Concat(typeof(SpeechPresenceResult).GetProperties().Select(property => property.Name))
            .Concat(typeof(SpeechPresenceEvidence).GetProperties().Select(property => property.Name));

        foreach (var name in publicNames)
        {
            Assert.DoesNotContain(forbidden, value => name.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task OfficialDecision_WhenFileLoggingEnabled_WritesDedicatedJsonLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"speech-presence-{Guid.NewGuid():N}.tmp.log");
        var options = new SpeechPresenceOptions
        {
            LogDecisions = true,
            LogDecisionsToFile = true,
            DecisionLogFilePath = path,
            DecisionLogFlushIntervalMs = 50,
            DecisionLogMaxQueueEntries = 32
        };
        var monitor = new TestOptionsMonitor<SpeechPresenceOptions>(options);
        var sink = new SpeechPresenceDecisionLogService(
            monitor,
            NullLogger<SpeechPresenceDecisionLogService>.Instance);
        try
        {
            await sink.StartAsync(CancellationToken.None);
            var evidence = new SpeechPresenceEvidence
            {
                FrameId = 42,
                TimestampUtc = DateTimeOffset.UtcNow,
                AssistantPlaybackActive = true,
                RawMicRms = 0.02,
                RawMicPeak = 0.03,
                EchoReducedRms = 0.002,
                EchoReducedPeak = 0.004,
                VadConfidence = 0.60,
                VadSpeechDetected = true,
                SourcePath = "official_frame_decision"
            };
            sink.TryLogOfficialDecision(new SpeechPresenceOfficialDecision
            {
                FrameId = evidence.FrameId,
                TimestampUtc = evidence.TimestampUtc,
                Result = new SpeechPresenceResult
                {
                    State = SpeechPresenceState.Maybe,
                    Confidence = 0.7,
                    IsUserSpeaking = true,
                    ShouldYieldPlayback = true,
                    Reason = "test",
                    Evidence = evidence
                }
            });

            await WaitForFileAsync(path);
            var contents = File.ReadAllText(path);
            Assert.Contains("\"eventName\":\"SpeechPresenceOfficialDecision\"", contents);
            Assert.Contains("\"frameId\":42", contents);
            Assert.Contains("\"sourcePath\":\"official_frame_decision\"", contents);
            Assert.Contains("\"isAuthoritative\":true", contents);
            Assert.Contains("\"shouldYieldPlayback\":true", contents);
        }
        finally
        {
            await sink.StopAsync(CancellationToken.None);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ManualSpeechStartMarker_WhenFileLoggingEnabled_WritesMarkerJsonLine()
    {
        var path = Path.Combine(Path.GetTempPath(), $"speech-presence-{Guid.NewGuid():N}.tmp.log");
        var options = new SpeechPresenceOptions
        {
            LogDecisions = true,
            LogDecisionsToFile = true,
            DecisionLogFilePath = path,
            DecisionLogFlushIntervalMs = 50,
            DecisionLogMaxQueueEntries = 32
        };
        var monitor = new TestOptionsMonitor<SpeechPresenceOptions>(options);
        var sink = new SpeechPresenceDecisionLogService(
            monitor,
            NullLogger<SpeechPresenceDecisionLogService>.Instance);
        try
        {
            await sink.StartAsync(CancellationToken.None);
            sink.TryLogManualSpeechStartMarker(new SpeechPresenceManualMarker
            {
                TimestampUtc = DateTimeOffset.UnixEpoch.AddSeconds(1),
                MarkerType = "user_started_speaking",
                Source = "frontend_debug_button",
                ClientTimestampUtc = DateTimeOffset.UnixEpoch,
                Note = "manual speech start marker"
            });

            await WaitForFileAsync(path);
            var contents = File.ReadAllText(path);
            Assert.Contains("\"eventName\":\"ManualSpeechStartMarker\"", contents);
            Assert.Contains("\"markerType\":\"user_started_speaking\"", contents);
            Assert.Contains("\"source\":\"frontend_debug_button\"", contents);
            Assert.Contains("\"clientTimestampUtc\":\"1970-01-01T00:00:00+00:00\"", contents);
            Assert.Contains("\"note\":\"manual speech start marker\"", contents);
        }
        finally
        {
            await sink.StopAsync(CancellationToken.None);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static async Task WaitForFileAsync(string path)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(File.Exists(path), $"Expected speech presence log file to exist: {path}");
    }

    private static SpeechPresenceDetector CreateDetector(SpeechPresenceOptions? options = null)
    {
        return new SpeechPresenceDetector(
            new TestOptionsMonitor<SpeechPresenceOptions>(options ?? new SpeechPresenceOptions { LogDecisions = false }),
            NullLogger<SpeechPresenceDetector>.Instance);
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public TestOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
