using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.LiveUtterance;
using Merlin.Backend.Services.SpeechPresence;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NAudio.CoreAudioApi;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class BargeInClassifierTests
{
    [Theory]
    [InlineData("stop", InterruptionType.HardStop)]
    [InlineData("cancel", InterruptionType.HardStop)]
    [InlineData("wait", InterruptionType.HardStop)]
    [InlineData("pause", InterruptionType.HardStop)]
    [InlineData("no, I mean beam", InterruptionType.Correction)]
    [InlineData("actually use SQLite", InterruptionType.Correction)]
    [InlineData("what do you mean", InterruptionType.ClarificationQuestion)]
    [InlineData("how does that work", InterruptionType.ClarificationQuestion)]
    [InlineData("yeah", InterruptionType.Backchannel)]
    [InlineData("mhm", InterruptionType.Backchannel)]
    public void Classify_MatchesExpectedBargeInType(string transcript, InterruptionType expected)
    {
        var classifier = new InterruptionClassifier();
        var result = classifier.Classify(
            CreateInput(transcript, isAecDegraded: false),
            new BargeInOptions { RequireWakeWordForFirstVersion = false });

        Assert.Equal(expected, result.Type);
    }

    [Fact]
    public void Classify_WithDegradedAec_RequiresWakeWord()
    {
        var classifier = new InterruptionClassifier();

        var withoutWakeWord = classifier.Classify(CreateInput("stop", isAecDegraded: true, currentSpeechType: "Idle"), new BargeInOptions());
        var withWakeWord = classifier.Classify(CreateInput("merlin stop", isAecDegraded: true), new BargeInOptions());

        Assert.Equal(InterruptionType.NoiseOrEcho, withoutWakeWord.Type);
        Assert.Equal(InterruptionType.HardStop, withWakeWord.Type);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("please stop")]
    [InlineData("abort")]
    [InlineData("cancel that")]
    [InlineData("Merlin please stop")]
    [InlineData("stop talking")]
    [InlineData("shut up")]
    [InlineData("quiet")]
    [InlineData("enough")]
    [InlineData("never mind")]
    [InlineData("hold on")]
    public void Classify_WhileAssistantSpeaking_AllowsNaturalHardStopWithoutWakeWord(string transcript)
    {
        var classifier = new InterruptionClassifier();

        var result = classifier.Classify(
            CreateInput(transcript, isAecDegraded: false),
            new BargeInOptions { RequireWakeWordForFirstVersion = true });

        Assert.Equal(InterruptionType.HardStop, result.Type);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("shut up")]
    [InlineData("and shut up")]
    [InlineData("no stop")]
    public void Classify_WhileAssistantSpeaking_RejectsStopWithoutWakePrefix_WhenExperimentEnabled(string transcript)
    {
        var classifier = new InterruptionClassifier();

        var result = classifier.Classify(
            CreateInput(transcript, isAecDegraded: false),
            new BargeInOptions
            {
                RequireWakeWordForFirstVersion = true,
                RequireWakePrefixForStopDuringPlayback = true,
                StopWakePrefix = "merlin"
            });

        Assert.Equal(InterruptionType.NoiseOrEcho, result.Type);
    }

    [Theory]
    [InlineData("Merlin stop")]
    [InlineData("Merlin, stop")]
    [InlineData("Merlin shut up")]
    [InlineData("Hey Merlin stop")]
    public void Classify_WhileAssistantSpeaking_AcceptsStopWithWakePrefix_WhenExperimentEnabled(string transcript)
    {
        var classifier = new InterruptionClassifier();

        var result = classifier.Classify(
            CreateInput(transcript, isAecDegraded: false),
            new BargeInOptions
            {
                RequireWakeWordForFirstVersion = true,
                RequireWakePrefixForStopDuringPlayback = true,
                StopWakePrefix = "merlin"
            });

        Assert.Equal(InterruptionType.HardStop, result.Type);
    }

    [Fact]
    public void Classify_PlainStopWhileIdle_StillRequiresWakeWord()
    {
        var classifier = new InterruptionClassifier();

        var result = classifier.Classify(
            CreateInput("stop", isAecDegraded: false, currentSpeechType: "Idle"),
            new BargeInOptions { RequireWakeWordForFirstVersion = true });

        Assert.Equal(InterruptionType.NoiseOrEcho, result.Type);
    }

    private static InterruptionClassificationInput CreateInput(
        string transcript,
        bool isAecDegraded,
        string currentSpeechType = "FinalAnswer")
    {
        var normalized = InterruptionClassifier.Normalize(transcript);
        return new InterruptionClassificationInput
        {
            RawTranscript = transcript,
            NormalizedTranscript = normalized,
            AssistantTurnId = "turn-1",
            CurrentSpeechType = currentSpeechType,
            SpokenTextSoFar = "Merlin is speaking.",
            VadConfidence = 0.9,
            WasWakeWordPresent = normalized.StartsWith("merlin", StringComparison.OrdinalIgnoreCase),
            IsAecDegraded = isAecDegraded
        };
    }
}

public sealed class BargeInVadTests
{
    [Fact]
    public void ProcessFrame_DoesNotTriggerOnSingleLoudFrame()
    {
        var vad = new BargeInVadService();
        var options = new BargeInOptions();
        var result = vad.ProcessFrame(CreateFrame(0.15f, 0), options);

        Assert.True(result.IsSpeech);
        Assert.False(result.IsTriggered);
    }

    [Fact]
    public void ProcessFrame_TriggersAfterSustainedSpeech()
    {
        var vad = new BargeInVadService();
        var options = new BargeInOptions { VadTriggerSpeechMs = 350, VadMinSpeechMs = 250 };
        VadFrameResult result = default!;
        var triggered = false;

        for (var index = 0; index < 36; index++)
        {
            result = vad.ProcessFrame(CreateFrame(0.12f, index * 10), options);
            triggered |= result.IsTriggered;
        }

        Assert.True(triggered);
        Assert.True(result.ConsecutiveSpeechMs >= 350);
    }

    private static VadFrameInput CreateFrame(float amplitude, int offsetMs)
    {
        return new VadFrameInput
        {
            Samples = Enumerable.Repeat(amplitude, 160).ToArray(),
            SampleRate = 16000,
            Timestamp = DateTimeOffset.UnixEpoch.AddMilliseconds(offsetMs)
        };
    }
}

public sealed class SpeakerDuckingServiceTests
{
    [Fact]
    public void StartAndRestore_RaisesDuckingChangedEventImmediately()
    {
        var service = new SpeakerDuckingService(
            Options.Create(new BargeInOptions { DuckingVolumePercent = 15, DuckingFadeMs = 40, DuckingRestoreMs = 90 }),
            NullLogger<SpeakerDuckingService>.Instance);
        var events = new List<SpeakerDuckingChangedEventArgs>();
        service.DuckingChanged += (_, eventArgs) => events.Add(eventArgs);

        service.StartDucking(BargeInCoordinatorTests.CreateContext(), "vad_active_frame");
        service.Restore(BargeInCoordinatorTests.CreateContext(), "speech_hangover_elapsed");

        Assert.Collection(
            events,
            started =>
            {
                Assert.True(started.IsDucked);
                Assert.Equal(0.15f, started.VolumeMultiplier);
                Assert.Equal("vad_active_frame", started.Reason);
                Assert.Equal(TimeSpan.FromMilliseconds(40), started.FadeDuration);
            },
            restored =>
            {
                Assert.False(restored.IsDucked);
                Assert.Equal(1.0f, restored.VolumeMultiplier);
                Assert.Equal("speech_hangover_elapsed", restored.Reason);
                Assert.Equal(TimeSpan.FromMilliseconds(90), restored.FadeDuration);
            });
    }

    [Fact]
    public void DuckingChanged_UpdatesActivePlaybackVolumeImmediately()
    {
        var ducking = new SpeakerDuckingService(
            Options.Create(new BargeInOptions { DuckingVolumePercent = 12 }),
            NullLogger<SpeakerDuckingService>.Instance);
        var playback = new AssistantSpeechPlaybackService(
            new FakeVoiceSynthesisService(),
            new FakePlaybackReferenceTap(),
            ducking,
            NullLogger<AssistantSpeechPlaybackService>.Instance);
        var applied = new List<float>();

        playback.SetActiveVolumeSetterForTests(applied.Add);
        ducking.StartDucking(BargeInCoordinatorTests.CreateContext(), "vad_triggered");
        ducking.Restore(BargeInCoordinatorTests.CreateContext(), "speech_hangover_elapsed");

        Assert.Equal([0.12f, 1.0f], applied);
        playback.ClearActiveVolumeSetterForTests();
    }

    [Fact]
    public async Task EnqueueAsync_RestoresStaleDuckingBeforePlaybackStart()
    {
        var ducking = new SpeakerDuckingService(
            Options.Create(new BargeInOptions { DuckingVolumePercent = 20 }),
            NullLogger<SpeakerDuckingService>.Instance);
        ducking.StartDucking(BargeInCoordinatorTests.CreateContext(), "vad_triggered");
        var playback = new AssistantSpeechPlaybackService(
            new SingleChunkVoiceSynthesisService(),
            new FakePlaybackReferenceTap(),
            ducking,
            NullLogger<AssistantSpeechPlaybackService>.Instance);
        var events = new List<SpeakerDuckingChangedEventArgs>();
        ducking.DuckingChanged += (_, eventArgs) => events.Add(eventArgs);

        await playback.EnqueueAsync(
            "hello",
            "correlation-1",
            (_, _) => Task.CompletedTask,
            null,
            null,
            CancellationToken.None);
        await Task.Delay(250);

        Assert.False(ducking.IsDucked);
        Assert.Contains(events, e => !e.IsDucked && e.Reason == "playback_start_reset_stale_ducking");
    }

    private sealed class FakeVoiceSynthesisService : IVoiceSynthesisService
    {
        public Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class SingleChunkVoiceSynthesisService : IVoiceSynthesisService
    {
        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            await onMetadataAsync(
                new VoiceSynthesisStreamMetadata
                {
                    Format = "s16le",
                    SampleRate = 48000,
                    Channels = 1
                },
                cancellationToken);
            await onAudioAsync(new byte[960], cancellationToken);
        }
    }

    private sealed class FakePlaybackReferenceTap : IPlaybackReferenceTap
    {
        public event EventHandler<BargeInSpeechContext>? SpeechStarted;

        public event EventHandler<BargeInSpeechContext>? SpeechStopped;

        public void NotifySpeechStarted(BargeInSpeechContext context)
        {
            SpeechStarted?.Invoke(this, context);
        }

        public void NotifySpeechStopped(BargeInSpeechContext context)
        {
            SpeechStopped?.Invoke(this, context);
        }

        public void PushPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId)
        {
        }

        public void PushConsumedPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId)
        {
        }

        public ReadOnlyMemory<float> GetLatestReferenceFrame(int sampleCount)
        {
            return new float[sampleCount];
        }

        public bool TryGetReferenceWindow(int delayMs, int sampleCount, Span<float> destination)
        {
            destination[..sampleCount].Clear();
            return true;
        }

        public PlaybackReferenceDebugSnapshot GetDebugSnapshot()
        {
            return new PlaybackReferenceDebugSnapshot
            {
                IsPlaybackActive = false,
                SampleRate = 16000,
                BufferedSamples = 0,
                CapacitySamples = 32000,
                BufferedMilliseconds = 0,
                CurrentPlaybackEnergy = 0,
                RecentPlaybackEnergy = 0,
                WritePosition = 0,
                PlaybackStartedAt = null,
                PlaybackReferenceSource = "none",
                PlaybackReferenceIsConsumptionAligned = true,
                PlaybackConsumedSamplesTotal = 0,
                ReferenceBufferedMilliseconds = 0,
                ReferenceNewestAgeMilliseconds = null,
                ReferenceOldestAgeMilliseconds = null,
                LastOutputReadSamples = 0,
                LastOutputReadDurationMilliseconds = 0,
                LastOutputReadAtUtc = null
            };
        }
    }
}

public sealed class WindowsWasapiAecClientPropertiesTests
{
    [Fact]
    public void NAudioProperties_UseCommunicationsCategoryAndExpectedSize()
    {
        var diagnostics = WindowsWasapiAecClientProperties.CreateNAudioDiagnostics();

        Assert.Equal(WindowsWasapiAecClientProperties.ModeNAudio, diagnostics.Mode);
        Assert.Equal(16, diagnostics.CbSize);
        Assert.Equal(0, diagnostics.BIsOffload);
        Assert.Equal((int)AudioStreamCategory.Communications, diagnostics.ECategory);
        Assert.Equal(nameof(AudioStreamCategory.Communications), diagnostics.ECategoryName);
        Assert.Equal(0, diagnostics.Options);
    }

    [Fact]
    public void CustomInteropProperties_MatchWindowsAudioClientPropertiesLayout()
    {
        var diagnostics = WindowsWasapiAecClientProperties.CreateCustomInteropDiagnostics();

        Assert.Equal(WindowsWasapiAecClientProperties.ModeCustomInterop, diagnostics.Mode);
        Assert.Equal(16, diagnostics.CbSize);
        Assert.Equal(0, diagnostics.BIsOffload);
        Assert.Equal((int)AudioStreamCategory.Communications, diagnostics.ECategory);
        Assert.Equal(0, diagnostics.Options);
    }

    [Fact]
    public void SetClientPropertiesFailure_MapsToClearDiagnosticReason()
    {
        var reason = WindowsWasapiAecClientProperties.FormatSetClientPropertiesFailureReason(unchecked((int)0x88890001));

        Assert.Contains("Windows WASAPI AEC unavailable: SetClientProperties failed.", reason);
        Assert.Contains("0x88890001", reason);
        Assert.Contains("AUDCLNT_E_NOT_INITIALIZED", reason);
    }

    [Fact]
    public void ModeNormalization_DefaultsToNAudio()
    {
        Assert.Equal(WindowsWasapiAecClientProperties.ModeNAudio, WindowsWasapiAecClientProperties.NormalizeMode(null));
        Assert.Equal(WindowsWasapiAecClientProperties.ModeNAudio, WindowsWasapiAecClientProperties.NormalizeMode("unknown"));
        Assert.Equal(WindowsWasapiAecClientProperties.ModeCustomInterop, WindowsWasapiAecClientProperties.NormalizeMode("custominterop"));
        Assert.Equal(WindowsWasapiAecClientProperties.ModeDisabledForDiagnostics, WindowsWasapiAecClientProperties.NormalizeMode("DisabledForDiagnostics"));
    }

    [Fact]
    public async Task RequireRealAec_BlocksVadAfterSetClientPropertiesFailureState()
    {
        var vad = new BargeInCoordinatorTests.RecordingVadService();
        var fixture = BargeInCoordinatorTests.CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                AecProvider = "WindowsWasapiAec",
                AllowDegradedAecFallback = false,
                RequireRealAecForBargeIn = true
            },
            transcript: "merlin stop",
            aec: new UnavailableAecWithSetClientPropertiesReason(),
            vad: vad);

        fixture.Tap.NotifySpeechStarted(BargeInCoordinatorTests.CreateContext());
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(BargeInCoordinatorTests.CreateAudioFrame(0.25f, 0));

        Assert.Equal(0, vad.CallCount);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
    }

    private sealed class UnavailableAecWithSetClientPropertiesReason : IAcousticEchoCancellationService
    {
        public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
        {
            return new AecProcessResult
            {
                EchoReducedFrame = microphoneFrame,
                Mode = AecMode.Unavailable,
                IsEchoCancellationActive = false,
                Reason = WindowsWasapiAecClientProperties.FormatSetClientPropertiesFailureReason(unchecked((int)0x88890001))
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class WebRtcApmAecTests
{
    [Fact]
    public void BargeInOptions_DefaultProviderIsWebRtcApm_WhileDisabledByDefault()
    {
        var options = new BargeInOptions();

        Assert.False(options.Enabled);
        Assert.Equal("WebRtcApm", options.AecProvider);
        Assert.True(options.RequireRealAecForBargeIn);
        Assert.False(options.AllowDegradedAecFallback);
        Assert.True(options.RequireWakeWordForFirstVersion);
        Assert.False(options.AllowNaturalSoftBargeInWhenAecVerified);
    }

    [Fact]
    public async Task WebRtcApm_ReportsRealAec_WhenInitialized()
    {
        await using var provider = CreateProvider();

        await provider.InitializeAsync(new AecConfiguration(48000, 10, "WebRtcApm"));
        var result = provider.ProcessFrame(CreateSamples(480, 0.05f), CreateSamples(480, 0.02f));

        Assert.True(result.IsEchoCancellationActive);
        Assert.Equal(AecMode.Active, result.Mode);
        Assert.Equal(480, result.EchoReducedFrame.Length);
    }

    [Fact]
    public async Task WebRtcApm_AcceptsFarEndAndNearEndFrames_AndEmitsEchoReducedFrame()
    {
        await using var provider = CreateProvider();

        await provider.InitializeAsync(new AecConfiguration(48000, 10, "WebRtcApm"));
        var result = provider.ProcessFrame(CreateSamples(480, 0.08f), CreateSamples(480, 0.08f));

        Assert.True(result.IsEchoCancellationActive);
        Assert.Contains("echo-reduced", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(480, result.EchoReducedFrame.Length);
    }

    [Fact]
    public async Task DegradedNoOp_IsNeverTreatedAsRealAec()
    {
        await using var provider = new DegradedAcousticEchoCancellationService(new BargeInCoordinatorTests.NoOpBargeInDiagnosticsLogger());

        await provider.InitializeAsync(new AecConfiguration(48000, 10, "DegradedNoOp"));
        var result = provider.ProcessFrame(CreateSamples(480, 0.1f), CreateSamples(480, 0.1f));

        Assert.False(result.IsEchoCancellationActive);
        Assert.Equal(AecMode.DegradedNoOp, result.Mode);
    }

    [Fact]
    public async Task WebRtcApmUnavailable_BlocksNaturalBargeInBeforeVad()
    {
        var vad = new BargeInCoordinatorTests.RecordingVadService();
        var fixture = BargeInCoordinatorTests.CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                AecProvider = "WebRtcApm",
                RequireRealAecForBargeIn = true,
                AllowDegradedAecFallback = false
            },
            transcript: "merlin stop",
            aec: new FakeUnavailableWebRtcApmService(),
            vad: vad);

        fixture.Tap.NotifySpeechStarted(BargeInCoordinatorTests.CreateContext());
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(BargeInCoordinatorTests.CreateAudioFrame(0.3f, 0));

        Assert.Equal(0, vad.CallCount);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
    }

    private static WebRtcApmAcousticEchoCancellationService CreateProvider()
    {
        return new WebRtcApmAcousticEchoCancellationService(
            new BargeInCoordinatorTests.NoOpBargeInDiagnosticsLogger(),
            NullLogger<WebRtcApmAcousticEchoCancellationService>.Instance);
    }

    private static float[] CreateSamples(int count, float amplitude)
    {
        return Enumerable.Repeat(amplitude, count).ToArray();
    }

    private sealed class FakeUnavailableWebRtcApmService : IAcousticEchoCancellationService
    {
        public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
        {
            return new AecProcessResult
            {
                EchoReducedFrame = microphoneFrame,
                Mode = AecMode.Unavailable,
                IsEchoCancellationActive = false,
                Reason = "WebRTC APM initialization failed in test."
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class BargeInCoordinatorTests
{
    [Fact]
    public void ContinuousMicAudioBuffer_ExtractsAudioByTimeRange_WithPreRoll()
    {
        var buffer = new ContinuousMicAudioBuffer();
        var options = new BargeInOptions { ContinuousMicBufferMs = 10000, TriggerPreRollMs = 450 };
        var startedAt = DateTimeOffset.UtcNow;

        for (var index = 0; index < 300; index++)
        {
            buffer.Append(CreateAlternatingAudioFrame(0.12f, startedAt, index * 10), options);
        }

        var trigger = startedAt.AddMilliseconds(1000);
        var range = buffer.GetAudioRange(
            trigger,
            trigger.AddMilliseconds(-450),
            trigger.AddMilliseconds(2000),
            450,
            options);

        Assert.True(range.Frames.Count >= 245);
        Assert.Equal(450, range.ActualPreRollMsAvailable);
        Assert.Equal(450, range.ActualPreRollMsIncluded);
        Assert.Equal(45, range.PreRollFramesIncluded);
        Assert.Equal(0, range.FrameGapCount);
        Assert.True(CalculateDurationMs(range.Frames) >= 2450);
    }

    [Fact]
    public async Task AnalysisQueue_DropsOldest_WhenAnalyzerFallsBehind()
    {
        var queue = new BargeInAnalysisFrameQueue(capacity: 3);
        var startedAt = DateTimeOffset.UtcNow;

        for (var index = 0; index < 5; index++)
        {
            queue.Enqueue(CreateAlternatingAudioFrame(0.1f, startedAt, index * 10) with { SequenceNumber = index + 1 });
        }

        var first = await queue.DequeueAsync(CancellationToken.None);
        var second = await queue.DequeueAsync(CancellationToken.None);
        var third = await queue.DequeueAsync(CancellationToken.None);

        Assert.Equal(2, queue.DroppedFrames);
        Assert.Equal(3, first?.SequenceNumber);
        Assert.Equal(4, second?.SequenceNumber);
        Assert.Equal(5, third?.SequenceNumber);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CreatesOneOfficialSpeechPresenceDecisionPerFrame()
    {
        var detector = new RecordingSpeechPresenceDetector();
        var sink = new RecordingSpeechPresenceDecisionLogSink();
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            speechPresenceDetector: detector,
            speechPresenceDecisionLogSink: sink);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        var official = Assert.Single(sink.OfficialDecisions);
        Assert.Equal(1, official.FrameId);
        Assert.Equal("official_frame_decision", official.SourcePath);
        Assert.Equal("official_frame_decision", official.Result.Evidence.SourcePath);
        Assert.Single(detector.Evaluations.Where(evidence => evidence.SourcePath == "official_frame_decision"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_RecordsBranchObservationAsNonAuthoritative()
    {
        var detector = new RecordingSpeechPresenceDetector();
        var sink = new RecordingSpeechPresenceDecisionLogSink();
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            speechPresenceDetector: detector,
            speechPresenceDecisionLogSink: sink);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        var observation = Assert.Single(sink.BranchObservations);
        Assert.Equal(1, observation.FrameId);
        Assert.False(observation.IsAuthoritative);
        Assert.Equal("fast_hard_stop_candidate", observation.SourcePath);
        Assert.Equal("fast_hard_stop_candidate", observation.Result.Evidence.SourcePath);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_DebugSnapshotUsesOfficialSpeechPresenceNotBranchObservation()
    {
        var detector = new RecordingSpeechPresenceDetector(
            officialState: SpeechPresenceState.No,
            branchState: SpeechPresenceState.Maybe);
        var sink = new RecordingSpeechPresenceDecisionLogSink();
        var debugSnapshots = new RecordingBargeInDebugSnapshotService();
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            debugSnapshots: debugSnapshots,
            speechPresenceDetector: detector,
            speechPresenceDecisionLogSink: sink);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        Assert.Equal(SpeechPresenceState.No, Assert.Single(sink.OfficialDecisions).Result.State);
        Assert.Equal(SpeechPresenceState.Maybe, Assert.Single(sink.BranchObservations).Result.State);
        Assert.NotEmpty(debugSnapshots.Published);
        Assert.All(debugSnapshots.Published, snapshot =>
        {
            Assert.Equal("No", snapshot.SpeechPresenceState);
            Assert.Equal(1, snapshot.SpeechPresenceFrameId);
        });
    }

    [Fact]
    public async Task ContinuousMicAudioBuffer_SlowAnalysisDoesNotShortenRecordedAudio()
    {
        var buffer = new ContinuousMicAudioBuffer();
        var queue = new BargeInAnalysisFrameQueue(capacity: 500);
        var options = new BargeInOptions { ContinuousMicBufferMs = 10000, TriggerPreRollMs = 450 };
        var startedAt = DateTimeOffset.UtcNow;

        for (var index = 0; index < 300; index++)
        {
            var recorded = buffer.Append(CreateAlternatingAudioFrame(0.12f, startedAt, index * 10), options);
            queue.Enqueue(recorded);
        }

        for (var index = 0; index < 20; index++)
        {
            _ = await queue.DequeueAsync(CancellationToken.None);
        }

        var trigger = startedAt.AddMilliseconds(450);
        var range = buffer.GetAudioRange(
            trigger,
            startedAt,
            startedAt.AddMilliseconds(2990),
            450,
            options);

        Assert.True(CalculateDurationMs(range.Frames) >= 3000);
        Assert.True(queue.Count > 250);
        Assert.Equal(0, buffer.DroppedFrames);
    }

    [Fact]
    public void TriggerBuffer_CapturesConfiguredPreRollWindow()
    {
        var buffer = new BargeInTriggerBuffer();
        var startedAt = DateTimeOffset.UnixEpoch;
        var options = new BargeInOptions
        {
            TriggerPreRollMs = 450,
            TriggerMaxCaptureMs = 1000,
            InterruptionCaptureMaxMs = 1000
        };

        for (var offsetMs = -500; offsetMs <= 100; offsetMs += 50)
        {
            buffer.AddFrame(CreateAudioFrame(offsetMs / 1000f, startedAt, offsetMs));
        }

        var trigger = CreateAudioFrame(1.0f, startedAt, 0);
        var captured = buffer.CaptureTriggeredWindow(trigger, options, startedAt);
        var capturedOffsets = captured
            .Select(frame => (int)(frame.Timestamp - startedAt).TotalMilliseconds)
            .ToArray();

        Assert.DoesNotContain(-500, capturedOffsets);
        Assert.Contains(-450, capturedOffsets);
        Assert.Contains(0, capturedOffsets);
    }

    [Fact]
    public async Task InterruptionCaptureDiagnosticsWriter_SavesWavAndJsonSidecar()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"merlin-interruption-captures-{Guid.NewGuid():N}");
        try
        {
            var options = new BargeInOptions
            {
                SaveDebugAudio = true,
                DebugAudioPath = tempRoot,
                GatedSttMaxAudioMs = 10000
            };
            var writer = new InterruptionCaptureDiagnosticsWriter(
                new TestOptionsMonitor<BargeInOptions>(options),
                new TestHostEnvironment(tempRoot),
                NullLogger<InterruptionCaptureDiagnosticsWriter>.Instance);
            var diagnostic = new InterruptionCaptureDiagnostic
            {
                TimestampUtc = DateTimeOffset.Parse("2026-06-18T12:00:00Z"),
                CaptureKind = "normal_interruption",
                AssistantTurnId = "turn-1",
                CorrelationId = "correlation-1",
                SpeechType = SpeechPlaybackItemType.FinalAnswer.ToString(),
                AssistantWasSpeaking = true,
                DuckingWasActive = true,
                FrameCount = 2,
                AudioMs = 20,
                PreRollMs = 450,
                RequestedPreRollMs = 450,
                ActualPreRollMsAvailable = 430,
                ActualPreRollMsIncluded = 430,
                PreRollFramesIncluded = 43,
                OldestBufferedFrameAgeMs = 500,
                BufferResetReason = "speech_started",
                BufferOwnerAssistantTurnId = "turn-1",
                CurrentAssistantTurnId = "turn-1",
                PostPaddingMs = 800,
                MaxCaptureMs = 10000,
                CaptureEndReason = "sustained_silence",
                FirstSpeechFrameRelativeMs = 0,
                LastSpeechFrameRelativeMs = 20,
                CapturedSpeechMs = 20,
                RawSpeechFrames = 2,
                AecSpeechFrames = 2,
                VadSpeechFrames = 2,
                CaptureSpeechFrames = 2,
                FalseSilenceFramesWhileVadSpeech = 0,
                CaptureStartUtc = DateTimeOffset.UnixEpoch,
                CaptureEndUtc = DateTimeOffset.UnixEpoch.AddMilliseconds(20),
                CaptureWallClockMs = 20,
                SttInputAudioMs = 20,
                ContinuousRecorderBufferMs = 10000,
                AnalysisFramesDropped = 0,
                ContinuousFramesDropped = 0,
                MaxProcessingLagMs = 0,
                AverageProcessingLagMs = 0,
                FrameGapCount = 0,
                MaxCaptureFrameGapMs = 0,
                BuiltFromContinuousRecorder = true,
                SampleRate = 16000,
                SampleCount = 320,
                SttTranscript = "stop",
                NormalizedTranscript = "stop",
                ClassificationType = InterruptionType.HardStop.ToString(),
                ClassificationConfidence = 0.95,
                ClassificationReason = "Matched hard-stop phrase.",
                DecisionAction = BargeInAction.HardCancel.ToString(),
                DecisionAccepted = true,
                DecisionReason = "Hard cancellation accepted.",
                VadConfidence = 0.9,
                WasWakeWordPresent = false,
                IsAecDegraded = false,
                AecMode = AecMode.Active.ToString(),
                WavPath = null,
                JsonPath = null,
                FramesJsonlPath = null
            };
            var frames = new[]
            {
                CreateAlternatingAudioFrame(0.15f, DateTimeOffset.UnixEpoch, 0),
                CreateAlternatingAudioFrame(0.15f, DateTimeOffset.UnixEpoch, 10)
            };
            var frameDiagnostics = new[]
            {
                new InterruptionCaptureFrameDiagnostic
                {
                    FrameIndex = 0,
                    SequenceNumber = 1,
                    CapturedAtUtc = DateTimeOffset.UnixEpoch,
                    ProcessedAtUtc = DateTimeOffset.UnixEpoch,
                    TimestampUtc = DateTimeOffset.UnixEpoch,
                    RelativeMs = 0,
                    CaptureRelativeMs = 0,
                    ProcessingLagMs = 0,
                    DurationMs = 10,
                    QueueDepth = 0,
                    RawEnergy = 0.15,
                    AecEnergy = 0.15,
                    VadConfidence = 1,
                    VadSaysSpeech = true,
                    ComfortDuckingActive = true,
                    ComfortDuckingWouldAllow = true,
                    SelfSpeechDecision = SelfSpeechDecision.Allow.ToString(),
                    SelfSpeechReason = "test",
                    CaptureIsSpeechFrame = true,
                    LastSpeechAgeMs = 0,
                    AppendedToCapture = true,
                    AppendedToContinuousRecorder = true,
                    ProcessedByAnalyzer = true,
                    EndpointSilenceMs = 0
                }
            };

            await writer.SaveAsync(diagnostic, frames, frameDiagnostics, CancellationToken.None);

            var wav = Assert.Single(Directory.GetFiles(tempRoot, "*.wav"));
            var json = Assert.Single(Directory.GetFiles(tempRoot, "*.json"));
            var framesJsonl = Assert.Single(Directory.GetFiles(tempRoot, "*.frames.jsonl"));
            var jsonText = await File.ReadAllTextAsync(json);
            var framesJsonlText = await File.ReadAllTextAsync(framesJsonl);

            Assert.True(new FileInfo(wav).Length > 44);
            Assert.Contains("\"SttTranscript\": \"stop\"", jsonText);
            Assert.Contains("\"PreRollMs\": 450", jsonText);
            Assert.Contains("\"ActualPreRollMsAvailable\": 430", jsonText);
            Assert.Contains("\"WavPath\":", jsonText);
            Assert.Contains("\"FrameIndex\":0", framesJsonlText);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InterruptionCaptureDiagnosticsWriter_DoesNotOverwriteExistingCaptureForSameTimestamp()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"merlin-interruption-captures-{Guid.NewGuid():N}");
        try
        {
            var options = new BargeInOptions
            {
                SaveDebugAudio = true,
                DebugAudioPath = tempRoot,
                GatedSttMaxAudioMs = 10000
            };
            var writer = new InterruptionCaptureDiagnosticsWriter(
                new TestOptionsMonitor<BargeInOptions>(options),
                new TestHostEnvironment(tempRoot),
                NullLogger<InterruptionCaptureDiagnosticsWriter>.Instance);
            var diagnostic = CreateInterruptionCaptureDiagnostic(
                DateTimeOffset.Parse("2026-06-18T12:00:00Z"),
                "suppressed_fast_hard_stop_candidate");
            var frames = new[]
            {
                CreateAlternatingAudioFrame(0.15f, DateTimeOffset.UnixEpoch, 0)
            };

            await writer.SaveAsync(diagnostic, frames, [], CancellationToken.None);
            await writer.SaveAsync(diagnostic, frames, [], CancellationToken.None);

            Assert.Equal(2, Directory.GetFiles(tempRoot, "*.wav").Length);
            Assert.Equal(2, Directory.GetFiles(tempRoot, "*.json").Length);
            Assert.Equal(2, Directory.GetFiles(tempRoot, "*.frames.jsonl").Length);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SavesInterruptionCaptureDiagnostics_AfterClassification()
    {
        var captureWriter = new RecordingInterruptionCaptureDiagnosticsWriter();
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                TriggerPreRollMs = 450,
                GatedSttMaxAudioMs = 10000,
                PauseInsteadOfCancelOnSpeech = false,
                RequireWakeWordForFirstVersion = false,
                AllowNaturalSoftBargeInWhenAecVerified = true
            },
            "stop",
            captureDiagnosticsWriter: captureWriter);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.16f);

        await WaitUntilAsync(() => captureWriter.CallCount == 1);

        Assert.Equal(1, captureWriter.CallCount);
        Assert.Equal("normal_interruption", captureWriter.LastDiagnostic?.CaptureKind);
        Assert.Equal("stop", captureWriter.LastDiagnostic?.SttTranscript);
        Assert.Equal(450, captureWriter.LastDiagnostic?.PreRollMs);
        Assert.True(captureWriter.LastDiagnostic?.ActualPreRollMsAvailable >= 0);
        Assert.Equal(InterruptionType.HardStop.ToString(), captureWriter.LastDiagnostic?.ClassificationType);
        Assert.NotEmpty(captureWriter.LastFrames);
        Assert.NotEmpty(captureWriter.LastFrameDiagnostics);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CaptureContinues_WhenRawSpeechContinuesAfterAecLooksSilent()
    {
        var captureWriter = new RecordingInterruptionCaptureDiagnosticsWriter();
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadEndSilenceMs = 800,
                TriggerMaxCaptureMs = 5000,
                InterruptionCaptureMaxMs = 5000,
                GatedSttMaxAudioMs = 5000,
                CaptureContinuationRawEnergyThreshold = 0.025,
                CaptureContinuationAecEnergyThreshold = 0.010,
                PauseInsteadOfCancelOnSpeech = false,
                RequireWakeWordForFirstVersion = false,
                AllowNaturalSoftBargeInWhenAecVerified = true
            },
            "still talking for a while",
            aec: new PassThroughThenSilenceAecService(passThroughFrames: 40),
            captureDiagnosticsWriter: captureWriter);
        fixture.Tap.NotifySpeechStarted(CreateContext());
        var started = DateTimeOffset.UtcNow;

        for (var index = 0; index < 40; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.16f, started, index * 10));
        }

        for (var index = 40; index < 260; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.16f, started, index * 10));
        }

        for (var index = 260; index < 360; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => captureWriter.CallCount > 0);

        Assert.True(fixture.Stt.LastAudioDuration.TotalMilliseconds >= 3000);
        Assert.Equal("sustained_silence", captureWriter.LastDiagnostic?.CaptureEndReason);
        Assert.True(captureWriter.LastDiagnostic?.RawSpeechFrames >= 200);
        Assert.True(captureWriter.LastDiagnostic?.CaptureSpeechFrames >= 200);
        Assert.Contains(captureWriter.LastFrameDiagnostics, frame => frame.RawEnergy > 0.025 && frame.AecEnergy < 0.010 && frame.CaptureIsSpeechFrame);
    }

    [Fact]
    public void TriggerBuffer_ReportsActualAvailablePreRoll()
    {
        var buffer = new BargeInTriggerBuffer();
        var startedAt = DateTimeOffset.UnixEpoch;
        var options = new BargeInOptions
        {
            TriggerPreRollMs = 450,
            TriggerMaxCaptureMs = 1000,
            InterruptionCaptureMaxMs = 1000
        };

        for (var offsetMs = -80; offsetMs <= 20; offsetMs += 10)
        {
            buffer.AddFrame(CreateAudioFrame(0.1f, startedAt, offsetMs));
        }

        var trigger = CreateAudioFrame(1.0f, startedAt, 0);
        var capture = buffer.CaptureTriggeredWindowWithDiagnostics(trigger, options, startedAt, "turn-1");

        Assert.Equal(450, capture.RequestedPreRollMs);
        Assert.Equal(80, capture.ActualPreRollMsAvailable);
        Assert.Equal(80, capture.ActualPreRollMsIncluded);
        Assert.Equal(8, capture.PreRollFramesIncluded);
        Assert.Equal(80, capture.OldestBufferedFrameAgeMs);
    }

    [Fact]
    public async Task Monitor_DoesNotStart_WhenDisabledByDefault()
    {
        var fixture = CreateFixture(new BargeInOptions());

        fixture.Tap.NotifySpeechStarted(CreateContext());
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        Assert.False(fixture.Coordinator.IsMonitoring);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public void Monitor_StartsAndStops_WhenPlaybackLifecycleRuns()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true });
        var context = CreateContext();

        fixture.Tap.NotifySpeechStarted(context);
        Assert.True(fixture.Coordinator.IsMonitoring);

        fixture.Tap.NotifySpeechStopped(context);
        Assert.False(fixture.Coordinator.IsMonitoring);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_DoesNotCallGatedStt_UntilVadTriggers()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true });
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 20; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.001f, index * 10));
        }

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_Disabled_AllowsExistingCapture()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                RequireSustainedUserSpeechScoreDuringPlayback = false
            },
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(0.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        Assert.Equal(0, fixture.Playback.PauseCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_BlocksLowScoreBeforeStt()
    {
        var fixture = CreateFixture(
            SustainedUserSpeechScoreOptions(),
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(0.2));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 10; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_BlocksBriefHighScore()
    {
        var options = SustainedUserSpeechScoreOptions();
        options.SustainedUserSpeechScoreDurationMs = 250;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 5; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_AllowsSustainedHighScore()
    {
        var options = SustainedUserSpeechScoreOptions();
        options.SustainedUserSpeechScoreDurationMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 3; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_ResetsWhenScoreDrops()
    {
        var options = SustainedUserSpeechScoreOptions();
        options.SustainedUserSpeechScoreDurationMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0, 1.0, 0.2, 1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 4; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_MissingScoreBlocksCandidateWithoutBelowThresholdReason()
    {
        var options = SustainedUserSpeechScoreOptions();
        options.PausePlaybackOnRollingUserSpeechEvidence = true;
        options.BurstCapturePromotion = new BurstCapturePromotionOptions
        {
            Enabled = true,
            MinBurstMs = 1,
            MaxWindowMs = 350,
            MinCandidateFrames = 1,
            MinVadSpeechFrameRatio = 0.01,
            RequireAssistantPlayback = true
        };
        var logger = new RecordingLogger<BargeInCoordinator>();
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            logger: logger);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Contains(logger.Messages, message => message.Contains("missing_user_speech_score", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("UserSpeechScore below sustained threshold", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_Disabled_DoesNotYieldPlayback()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.PausePlaybackOnRollingUserSpeechEvidence = false;
        options.PausePlaybackOnSustainedUserSpeechScore = false;
        options.FloorYieldRequiredHighScoreMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 5; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_BelowThreshold_DoesNotYieldPlayback()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(0.2));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 5; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_BriefHighScore_DoesNotYieldPlayback()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 180;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 5; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_SustainedHighScore_DoesNotYieldPlayback()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 3; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_MissingScoreDoesNotResetRollingEvidence()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 30;
        options.FloorYieldEvidenceWindowMs = 350;
        var logger = new RecordingLogger<BargeInCoordinator>();
        var fixture = CreateFixture(
            options,
            vad: new SequenceVadService(true, true, false, true),
            selfSpeechGate: new FrameScoreSequenceSelfSpeechGate(1.0, 1.0, 1.0),
            logger: logger);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 4; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("missing_score_ignored", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_FragmentedHighScoreIslands_DoNotYieldPlayback()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 180;
        options.FloorYieldEvidenceWindowMs = 350;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new FrameScoreSequenceSelfSpeechGate(
                1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
                0.2, 0.2, 0.2,
                1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0,
                0.2, 0.2, 0.2,
                1.0, 1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 24; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_BelowThresholdFramesDoNotHardReset()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 50;
        options.FloorYieldEvidenceWindowMs = 350;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new FrameScoreSequenceSelfSpeechGate(1.0, 1.0, 0.2, 1.0, 1.0, 1.0));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_MeasuredLowScoreStillBlocks()
    {
        var logger = new RecordingLogger<BargeInCoordinator>();
        var fixture = CreateFixture(
            SustainedUserSpeechScoreOptions(),
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(0.0),
            logger: logger);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Contains(logger.Messages, message => message.Contains("measured_score_below_threshold", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScoreGate_SuppressAsSelfEchoStillBlocks()
    {
        var fixture = CreateFixture(
            SustainedUserSpeechScoreOptions(),
            vad: new AlwaysTriggeredVadService(),
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.SuppressAsSelfEcho));
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_ResetsWhenPlaybackStops()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        var context = CreateContext();
        fixture.Tap.NotifySpeechStarted(context);

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 0));
        fixture.Tap.NotifySpeechStopped(context);
        fixture.Tap.NotifySpeechStarted(context);
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, 10));

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SustainedUserSpeechScorePause_DoesNotCancelActiveTurn()
    {
        var options = SustainedUserSpeechScorePauseOptions();
        options.FloorYieldRequiredHighScoreMs = 30;
        var fixture = CreateFixture(
            options,
            vad: new AlwaysSpeechNeverTriggeredVadService(),
            selfSpeechGate: new ScoreSequenceSelfSpeechGate(1.0));
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 3; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.2f, index * 10));
        }

        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.False(fixture.LiveTurnService.IsCancelled("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_FastNearEndSpeech_DoesNotDuckBeforeCaptureThreshold()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50,
                    MinVadConfidence = 0.4,
                    MinEnergyRatioOverNoise = 4.0,
                    MinAbsoluteEnergy = 0.008,
                    HangoverMs = 220,
                    UseSelfSpeechGate = true
                }
            });
        var started = DateTimeOffset.UtcNow.AddMilliseconds(400);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("comfort_ducking_likely_user", fixture.SpeakerDuckingService.Reasons);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.PauseCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_UncertainNearEndSpeech_DoesNotStartComfortDucking()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    AllowUncertain = true,
                    MinSpeechMs = 50
                },
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinVadConfidence = 0.4,
                    MinEnergyRatioOverNoise = 4.0,
                    MinAbsoluteEnergy = 0.008
                }
            });
        var started = DateTimeOffset.UtcNow.AddMilliseconds(400);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.045f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("comfort_ducking_uncertain_near_end", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_UncertainNearEndSpeech_DoesNotStartCapture()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    AllowUncertain = true,
                    MinSpeechMs = 50
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow.AddMilliseconds(400);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.045f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.DoesNotContain("vad_triggered", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_StrictCapturePolicy_StillBlocksUncertainFrames()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadMinSpeechMs = 120,
                VadTriggerSpeechMs = 120,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    AllowUncertain = true,
                    MinSpeechMs = 50
                },
                SelfSpeechSuppression = new SelfSpeechSuppressionOptions
                {
                    AllowSustainedUncertainForCapture = false
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow.AddMilliseconds(400);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 20; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.045f, started, index * 10));
        }

        await Task.Delay(50);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("comfort_ducking_uncertain_near_end", fixture.SpeakerDuckingService.Reasons);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.DoesNotContain("vad_triggered", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_AllowLikelyUser_DoesNotStartComfortDucking()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50
                }
            });
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("comfort_ducking_likely_user", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_FastNearEndSelfEcho_DoesNotDuck()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                FastNearEndDucking = new FastNearEndDuckingOptions { Enabled = true, MinSpeechMs = 50 }
            });
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.20f);

        for (var index = 0; index < 8; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.07f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_FastNearEndDucking_DoesNotDuckOrRestoreAfterHangoverWithoutCapture()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50,
                    HangoverMs = 30
                }
            });
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, 70));

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_FastNearEndDucking_DoesNotHoldThroughSuppressedMessyFrames()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50,
                    HangoverMs = 120
                }
            });
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        PushReferenceAudio(fixture.Tap, 0.20f);
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.07f, started, 70));
        await Task.Delay(40);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_NormalCapture_TakesOverAfterFastDucking()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadMinSpeechMs = 180,
                VadTriggerSpeechMs = 180,
                VadEndSilenceMs = 80,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50,
                    HangoverMs = 30
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow;
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        for (var index = 6; index < 24; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        for (var index = 24; index < 40; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.DoesNotContain("comfort_ducking_likely_user", fixture.SpeakerDuckingService.Reasons);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_PendingFastDuckingRestore_IsIgnoredWhenCaptureStarts()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadMinSpeechMs = 180,
                VadTriggerSpeechMs = 180,
                VadEndSilenceMs = 1000,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50,
                    HangoverMs = 40
                },
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, 70));

        for (var index = 8; index < 24; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        await Task.Delay(90);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
        Assert.Equal(0, fixture.SpeakerDuckingService.RestoreCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_ZeroDelayRestoreTask_DoesNotRestoreWhenCaptureOwnsDucking()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadMinSpeechMs = 180,
                VadTriggerSpeechMs = 180,
                VadEndSilenceMs = 1000,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50,
                    HangoverMs = 0
                },
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 50
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, 70));
        for (var index = 8; index < 24; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        await Task.Delay(50);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_ActiveCapture_HoldsDuckingThroughSuppressedSpeechFrames()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                DuckingSpeechHangoverMs = 30,
                VadMinSpeechMs = 50,
                VadTriggerSpeechMs = 50,
                VadEndSilenceMs = 1000,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 30
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 6; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        PushReferenceAudio(fixture.Tap, 0.20f);
        for (var index = 6; index < 10; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.07f, started, index * 10));
        }

        await Task.Delay(80);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CaptureOwnsDuckingUntilCaptureCompletes()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                DuckingSpeechHangoverMs = 20,
                VadMinSpeechMs = 50,
                VadTriggerSpeechMs = 50,
                VadEndSilenceMs = 1000,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 30,
                    HangoverMs = 20
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 8; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, 90));
        await Task.Delay(70);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.SpeakerDuckingService.RestoreCount);
        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_RestoresAfterCaptureCompletes()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                DuckingSpeechHangoverMs = 20,
                VadMinSpeechMs = 50,
                VadTriggerSpeechMs = 50,
                VadEndSilenceMs = 30,
                TriggerMaxCaptureMs = 1000,
                InterruptionCaptureMaxMs = 1000,
                GatedSttMaxAudioMs = 1000,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 30,
                    HangoverMs = 20
                }
            },
            "merlin yeah");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 8; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        for (var index = 9; index < 18; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.SpeakerDuckingService.RestoreCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_RepeatedRestoreTimers_DoNotFightCaptureOwner()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                DuckingSpeechHangoverMs = 10,
                VadMinSpeechMs = 80,
                VadTriggerSpeechMs = 80,
                VadEndSilenceMs = 1000,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000,
                ComfortDucking = new ComfortDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 30,
                    HangoverMs = 10
                },
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 30
                }
            },
            "merlin stop");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 5; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, 60));
        for (var index = 7; index < 14; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        for (var index = 14; index < 20; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await Task.Delay(80);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.SpeakerDuckingService.RestoreCount);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_RepeatedFastNearEndFrames_DoNotRestartDucking()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadTriggerSpeechMs = 350,
                FastNearEndDucking = new FastNearEndDuckingOptions
                {
                    Enabled = true,
                    MinSpeechMs = 30
                }
            });
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 12; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.SpeakerDuckingService.StartCount);
    }

    [Fact]
    public async Task BurstPromotion_StartsCaptureAfterSustainedUncertainNearEndSpeech()
    {
        var captureWriter = new RecordingInterruptionCaptureDiagnosticsWriter();
        var fixture = CreateFixture(
            BurstPromotionOptions(),
            "hello merlin",
            captureDiagnosticsWriter: captureWriter,
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Uncertain));
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 70; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        for (var index = 70; index < 150; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => captureWriter.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal("burst_capture_promotion", captureWriter.LastDiagnostic?.CaptureStartReason);
        Assert.True(captureWriter.LastDiagnostic?.CandidateBurstMsAtPromotion >= 350);
        Assert.True(captureWriter.LastDiagnostic?.BurstUncertainFrames > 0);
    }

    [Fact]
    public async Task BurstPromotion_DoesNotStartCaptureForShortUncertainSpike()
    {
        var fixture = CreateFixture(
            BurstPromotionOptions(),
            "hello merlin",
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Uncertain));
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 18; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        for (var index = 18; index < 70; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await Task.Delay(100);

        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task BurstPromotion_DoesNotStartCaptureWhenStrongSelfEchoDominates()
    {
        var fixture = CreateFixture(
            BurstPromotionOptions(),
            "stop",
            selfSpeechGate: new SequenceSelfSpeechGate(
                SelfSpeechDecision.SuppressAsSelfEcho,
                "Mic audio strongly correlates with assistant playback reference.",
                SelfSpeechCorrelationDecision.SelfEcho,
                0.95));
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 220; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        for (var index = 220; index < 280; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await Task.Delay(100);

        Assert.Equal(0, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task BurstPromotion_AllowsMixedAllowUncertainFrames()
    {
        var captureWriter = new RecordingInterruptionCaptureDiagnosticsWriter();
        var fixture = CreateFixture(
            BurstPromotionOptions(),
            "hello merlin",
            captureDiagnosticsWriter: captureWriter,
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Uncertain, SelfSpeechDecision.Allow));
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 70; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(0.18f, started, index * 10));
        }

        for (var index = 70; index < 150; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => captureWriter.CallCount > 0);

        var diagnostic = Assert.IsType<InterruptionCaptureDiagnostic>(captureWriter.LastDiagnostic);
        Assert.Equal("burst_capture_promotion", diagnostic.CaptureStartReason);
        Assert.True(diagnostic.BurstAllowFrames.GetValueOrDefault() + diagnostic.BurstUncertainFrames.GetValueOrDefault() > 0);
        Assert.True(diagnostic.BurstStrongSelfEchoFrames.GetValueOrDefault() < diagnostic.BurstTotalFrames.GetValueOrDefault());
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_DuckingDoesNotRestoreBeforeSttClassification()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                DuckingSpeechHangoverMs = 30,
                VadEndSilenceMs = 1000,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000
            },
            "merlin yeah");
        var started = DateTimeOffset.UtcNow;
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 40; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.16f, started, index * 10));
        }

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, 400));
        await Task.Delay(80);

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.DoesNotContain("speech_hangover_elapsed", fixture.SpeakerDuckingService.Reasons);

        for (var index = 401; index < 510; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        Assert.False(fixture.SpeakerDuckingService.IsDucked);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_HardStop_CancelsCurrentTurn()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.False(fixture.LiveTurnService.ShouldEmit("correlation-1"));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("please stop")]
    [InlineData("abort")]
    [InlineData("cancel that")]
    [InlineData("merlin please stop")]
    public async Task ProcessMicrophoneFrame_NaturalHardStopWhileSpeaking_CancelsCurrentTurn(string transcript)
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, transcript);
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.False(fixture.LiveTurnService.ShouldEmit("correlation-1"));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("shut up")]
    public async Task ProcessMicrophoneFrame_StopWithoutWakePrefixWhileSpeaking_DoesNotCancel_WhenExperimentEnabled(string transcript)
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                RequireWakePrefixForStopDuringPlayback = true,
                StopWakePrefix = "merlin"
            },
            transcript,
            liveUtteranceGate: CreateLiveUtteranceGate());
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await Task.Delay(50);

        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.False(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.True(fixture.LiveTurnService.ShouldEmit("correlation-1"));
        Assert.True(fixture.Playback.ResumeCount > 0);
    }

    [Theory]
    [InlineData("Merlin stop")]
    [InlineData("Merlin shut up")]
    public async Task ProcessMicrophoneFrame_StopWithWakePrefixWhileSpeaking_Cancels_WhenExperimentEnabled(string transcript)
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                RequireWakePrefixForStopDuringPlayback = true,
                StopWakePrefix = "merlin"
            },
            transcript,
            liveUtteranceGate: CreateLiveUtteranceGate());
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.False(fixture.LiveTurnService.ShouldEmit("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_GateClarificationSuppressesLegacySideCommentResume()
    {
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            "Wait, that's not what I meant.",
            liveUtteranceGate: CreateLiveUtteranceGate());
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(0, fixture.Playback.ResumeCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_EmbeddedShutUpGateCancelsAndSuppressesLegacySideComment()
    {
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            "And shut up.",
            liveUtteranceGate: CreateLiveUtteranceGate());
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(0, fixture.Playback.ResumeCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.False(fixture.LiveTurnService.ShouldEmit("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_NonDecisiveGateAllowsLegacySideCommentFallback()
    {
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            "thanks",
            liveUtteranceGate: CreateLiveUtteranceGate());
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ResumeCount > 0);

        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("abort")]
    public async Task ProcessMicrophoneFrame_ShortHardStopBurstWhileSpeaking_CancelsCurrentTurn(string transcript)
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                FastHardStopMinSpeechMs = 120,
                FastHardStopPostSpeechPaddingMs = 80,
                FastHardStopCaptureWindowMs = 700
            },
            transcript);
        var requestCount = 0;
        fixture.Coordinator.CorrectionRegenerationRequested += (_, _) =>
        {
            requestCount++;
            return Task.CompletedTask;
        };
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendUncorrelatedShortSpeechBurstAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.False(fixture.LiveTurnService.ShouldEmit("correlation-1"));
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_ShortNoiseBurstWhileSpeaking_DoesNotCancel()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                FastHardStopMinSpeechMs = 120,
                FastHardStopPostSpeechPaddingMs = 80,
                FastHardStopCaptureWindowMs = 700
            },
            "");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        for (var index = 0; index < 20; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.001f, index * 10));
        }

        await Task.Delay(50);

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_WithLoudPlaybackReference_RejectsLikelySpeakerLeakage()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                PlaybackLeakageReferenceEnergyThreshold = 0.03,
                PlaybackLeakageMinEchoReducedEnergyMultiplier = 2.5,
                PlaybackLeakageMinNearEndToReferenceRatio = 0.55,
                PlaybackLeakageMinVadConfidence = 0.75
            },
            "stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.20f);

        for (var index = 0; index < 40; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.03f, index * 10));
        }

        await Task.Delay(50);

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_WithLoudPlaybackReference_AllowsCloseUserSpeech()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                PlaybackLeakageReferenceEnergyThreshold = 0.03,
                PlaybackLeakageMinEchoReducedEnergyMultiplier = 2.5,
                PlaybackLeakageMinNearEndToReferenceRatio = 0.55,
                PlaybackLeakageMinVadConfidence = 0.75
            },
            "stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.20f);

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CapturedWindowSelfPlayback_SkipsSttAndDoesNotCancel()
    {
        var fixture = CreateFixture(
            CapturedWindowSelfPlaybackOptions(),
            "stop",
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Allow));
        var routedCount = 0;
        fixture.Coordinator.LiveUserUtteranceRouted += (_, _) =>
        {
            routedCount++;
            return Task.CompletedTask;
        };
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.16f, 16000);

        await SendTriggeredSpeechAsync(fixture.Coordinator, 0.16f);
        await Task.Delay(100);

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(0, routedCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CapturedWindowLowCorrelation_AllowsStt()
    {
        var fixture = CreateFixture(
            CapturedWindowSelfPlaybackOptions(),
            "stop",
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Allow));
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.16f, 16000);

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.16f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CapturedWindowReferenceUnavailable_AllowsStt()
    {
        var fixture = CreateFixture(
            CapturedWindowSelfPlaybackOptions(),
            "stop",
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Allow));
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator, 0.16f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CapturedWindowDelayedPlaybackEcho_SkipsStt()
    {
        var options = CapturedWindowSelfPlaybackOptions();
        options.CapturedWindowSelfPlaybackDelayMsMin = 80;
        options.CapturedWindowSelfPlaybackDelayMsMax = 120;
        var fixture = CreateFixture(
            options,
            "stop",
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Allow));
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.16f, 16000);
        PushReferenceAudio(fixture.Tap, 0.0f, 16000, milliseconds: 100);

        await SendTriggeredSpeechAsync(fixture.Coordinator, 0.16f);
        await Task.Delay(100);

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_CapturedWindowMixedUserAndPlayback_AllowsStt()
    {
        var fixture = CreateFixture(
            CapturedWindowSelfPlaybackOptions(),
            "stop",
            selfSpeechGate: new SequenceSelfSpeechGate(SelfSpeechDecision.Allow));
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.04f, 16000);

        await SendTriggeredSpeechAsync(fixture.Coordinator, 0.16f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_PlaybackOnsetTransient_DoesNotDuck()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "stop");
        var context = CreateContext();
        var started = DateTimeOffset.UtcNow;
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(context);

        for (var index = 0; index < 30; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.03f, started, index * 10));
        }

        await Task.Delay(50);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SelfEcho_DoesNotStartDuckingOrCapture()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.20f);

        for (var index = 0; index < 40; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.07f, DateTimeOffset.UtcNow, index * 10));
        }

        await Task.Delay(50);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_UserSpeechOverPlayback_StillCapturesWithoutLegacyDucking()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.20f);

        await SendUncorrelatedTriggeredSpeechAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.False(fixture.SpeakerDuckingService.IsDucked);
        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_UserStopOverPlayback_ReachesFastHardStopPath()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                FastHardStopMinSpeechMs = 120,
                FastHardStopPostSpeechPaddingMs = 80,
                FastHardStopCaptureWindowMs = 700
            },
            "stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());
        PushReferenceAudio(fixture.Tap, 0.20f);

        await SendUncorrelatedShortSpeechBurstAsync(fixture.Coordinator, 0.22f);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_ShortStopWhileIdle_DoesNotBypassWakePolicy()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                FastHardStopMinSpeechMs = 120,
                FastHardStopPostSpeechPaddingMs = 80,
                FastHardStopCaptureWindowMs = 700
            },
            "stop");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");

        await SendShortSpeechBurstAsync(fixture.Coordinator);
        await Task.Delay(50);

        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Backchannel_DoesNotCancelCurrentTurn()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin yeah");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ResumeCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
        Assert.True(fixture.LiveTurnService.ShouldEmit("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_SideComment_ResumesWithoutCancellation()
    {
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true, AllowNaturalSoftBargeInWhenAecVerified = true, RequireWakeWordForFirstVersion = true },
            "that sounds risky");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Correction_CancelsCurrentTurn()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin no i mean beam");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ClearQueueCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Playback.ResumeCount);
        Assert.True(fixture.LiveTurnService.IsCancelled("correlation-1"));
        Assert.False(fixture.LiveTurnService.ShouldEmit("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Correction_RaisesRegenerationRequest()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin no open firefox instead");
        var requests = new List<CorrectionRegenerationRequested>();
        fixture.Coordinator.CorrectionRegenerationRequested += (request, _) =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        };
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => requests.Count > 0);

        var request = Assert.Single(requests);
        Assert.Equal("correlation-1", request.OriginalCorrelationId);
        Assert.Equal("no open firefox instead", request.CorrectionText);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_LongCorrection_CapturesUntilSilence()
    {
        var fixture = CreateFixture(
            new BargeInOptions
            {
                Enabled = true,
                VadEndSilenceMs = 800,
                TriggerMaxCaptureMs = 10000,
                InterruptionCaptureMaxMs = 10000,
                GatedSttMaxAudioMs = 10000
            },
            "merlin no i mean the orb should feel magical and tactile not technical");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        var started = DateTimeOffset.UtcNow;
        for (var index = 0; index < 420; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.16f, started, index * 10));
        }

        for (var index = 420; index < 520; index++)
        {
            await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }

        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.NotNull(fixture.Stt.LastFrames);
        var firstTimestamp = fixture.Stt.LastFrames.First().Timestamp;
        var lastTimestamp = fixture.Stt.LastFrames.Last().Timestamp;
        Assert.True((lastTimestamp - firstTimestamp).TotalMilliseconds >= 4000);
        Assert.True(fixture.Stt.LastAudioDuration.TotalMilliseconds >= 4000);
    }

    [Theory]
    [InlineData("merlin stop")]
    [InlineData("merlin yeah")]
    [InlineData("merlin what does that mean")]
    public async Task ProcessMicrophoneFrame_NonCorrection_DoesNotRaiseRegenerationRequest(string transcript)
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, transcript);
        var requestCount = 0;
        fixture.Coordinator.CorrectionRegenerationRequested += (_, _) =>
        {
            requestCount++;
            return Task.CompletedTask;
        };
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await Task.Delay(50);

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_BackendIdleVoice_RaisesBackendVoiceRequest()
    {
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true },
            "What is the meaning of life?",
            liveUtteranceGate: CreateLiveUtteranceGate());
        var requests = new List<BackendVoiceRequestCaptured>();
        fixture.Coordinator.BackendVoiceRequestCaptured += (request, _) =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        };

        await fixture.Coordinator.StartLiveMonitoringAsync();
        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => requests.Count > 0);

        var request = Assert.Single(requests);
        Assert.Equal("What is the meaning of life?", request.Text);
        Assert.Equal("backend_idle_voice", request.InteractionSource);
        Assert.Equal(LiveAssistantTurnState.IdleListening, request.Utterance.StateWhenCaptured);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Clarification_ResumesAndDoesNotCancel()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin what does that mean");
        fixture.LiveTurnService.BeginTurn("conversation-1", "correlation-1");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);
        await WaitUntilAsync(() => fixture.Playback.ResumeCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
        Assert.True(fixture.LiveTurnService.IsActive("correlation-1"));
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_NoWakeWordNaturalBargeIn_BlockedByDefault()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "yeah");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_WhenRealAecRequiredAndUnavailable_DoesNotRunVadOrStt()
    {
        var vad = new RecordingVadService();
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true, AecProvider = "WindowsWasapiAec", RequireRealAecForBargeIn = true },
            transcript: "stop",
            aec: new FakeUnavailableAecService(),
            vad: vad);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);

        Assert.Equal(0, vad.CallCount);
        Assert.Equal(0, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_PassesEchoReducedFrameToVad()
    {
        var vad = new RecordingVadService();
        var fixture = CreateFixture(
            new BargeInOptions { Enabled = true, RequireRealAecForBargeIn = true },
            aec: new RewritingAecService(0.42f),
            vad: vad);
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await fixture.Coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.99f, 0));

        Assert.Equal(1, vad.CallCount);
        Assert.All(vad.LastSamples, sample => Assert.Equal(0.42f, sample));
    }

    private static async Task SendTriggeredSpeechAsync(IBargeInCoordinator coordinator, float amplitude = 0.16f)
    {
        for (var index = 0; index < 40; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(amplitude, index * 10));
        }
    }

    private static async Task SendShortSpeechBurstAsync(IBargeInCoordinator coordinator, float amplitude = 0.16f)
    {
        var started = DateTimeOffset.UtcNow;
        for (var index = 0; index < 12; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(amplitude, started, index * 10));
        }

        for (var index = 12; index < 32; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }
    }

    private static async Task SendUncorrelatedTriggeredSpeechAsync(IBargeInCoordinator coordinator, float amplitude)
    {
        for (var index = 0; index < 40; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(amplitude, DateTimeOffset.UnixEpoch, index * 10));
        }
    }

    private static async Task SendUncorrelatedShortSpeechBurstAsync(IBargeInCoordinator coordinator, float amplitude)
    {
        var started = DateTimeOffset.UtcNow;
        for (var index = 0; index < 12; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAlternatingAudioFrame(amplitude, started, index * 10));
        }

        for (var index = 12; index < 32; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.0f, started, index * 10));
        }
    }

    private static BargeInOptions CapturedWindowSelfPlaybackOptions()
    {
        return new BargeInOptions
        {
            Enabled = true,
            AecSampleRate = 16000,
            RequireWakeWordForFirstVersion = false,
            AllowNaturalSoftBargeInWhenAecVerified = true,
            CapturedWindowSelfPlaybackCorrelationThreshold = 0.82,
            CapturedWindowSelfPlaybackLikelyUserThreshold = 0.35,
            CapturedWindowSelfPlaybackMinReferenceEnergy = 0.008,
            CapturedWindowSelfPlaybackMinCaptureEnergy = 0.008,
            CapturedWindowSelfPlaybackSliceMs = 200,
            CapturedWindowSelfPlaybackMaxSlices = 4,
            CapturedWindowSelfPlaybackDelayMsMin = 0,
            CapturedWindowSelfPlaybackDelayMsMax = 250,
            CapturedWindowSelfPlaybackDelayStepMs = 10,
            CapturedWindowSelfPlaybackStrongUserEnergyRatio = 2.25,
            FastHardStopMinSpeechMs = 120,
            FastHardStopCaptureWindowMs = 700,
            FastHardStopPostSpeechPaddingMs = 80
        };
    }

    private static BargeInOptions SustainedUserSpeechScoreOptions()
    {
        return new BargeInOptions
        {
            Enabled = true,
            RequireSustainedUserSpeechScoreDuringPlayback = true,
            SustainedUserSpeechScoreThreshold = 0.90,
            SustainedUserSpeechScoreDurationMs = 250,
            PauseInsteadOfCancelOnSpeech = true,
            EnableSpeakerDucking = false,
            BurstCapturePromotion = new BurstCapturePromotionOptions
            {
                Enabled = false
            },
            FastNearEndDucking = new FastNearEndDuckingOptions
            {
                Enabled = false
            },
            ComfortDucking = new ComfortDuckingOptions
            {
                Enabled = false
            }
        };
    }

    private static BargeInOptions SustainedUserSpeechScorePauseOptions()
    {
        return new BargeInOptions
        {
            Enabled = true,
            PausePlaybackOnRollingUserSpeechEvidence = true,
            PausePlaybackOnSustainedUserSpeechScore = false,
            RequireSustainedUserSpeechScoreDuringPlayback = false,
            SustainedUserSpeechScoreThreshold = 0.90,
            SustainedUserSpeechScoreDurationMs = 250,
            FloorYieldEvidenceWindowMs = 350,
            FloorYieldHighScoreThreshold = 0.90,
            FloorYieldRequiredHighScoreMs = 180,
            FloorYieldAverageScoreThreshold = 0.65,
            FloorYieldRequireRecentHighFrame = true,
            FloorYieldRecentHighFrameWindowMs = 80,
            PauseInsteadOfCancelOnSpeech = false,
            EnableSpeakerDucking = true,
            BurstCapturePromotion = new BurstCapturePromotionOptions
            {
                Enabled = false
            },
            FastNearEndDucking = new FastNearEndDuckingOptions
            {
                Enabled = true,
                MinSpeechMs = 50,
                MinVadConfidence = 0.4,
                MinEnergyRatioOverNoise = 4.0,
                MinAbsoluteEnergy = 0.008,
                HangoverMs = 220,
                UseSelfSpeechGate = true
            },
            ComfortDucking = new ComfortDuckingOptions
            {
                Enabled = true,
                MinSpeechMs = 50
            }
        };
    }

    private static BargeInOptions BurstPromotionOptions()
    {
        return new BargeInOptions
        {
            Enabled = true,
            PauseInsteadOfCancelOnSpeech = false,
            RequireWakeWordForFirstVersion = false,
            AllowNaturalSoftBargeInWhenAecVerified = true,
            VadTriggerSpeechMs = 2000,
            VadMinSpeechMs = 2000,
            VadEndSilenceMs = 450,
            TriggerPreRollMs = 450,
            GatedSttMaxAudioMs = 10000,
            BurstCapturePromotion = new BurstCapturePromotionOptions
            {
                Enabled = true,
                MinBurstMs = 350,
                MaxWindowMs = 600,
                MinCandidateFrames = 8,
                MinVadSpeechFrameRatio = 0.35,
                AllowUncertainPromotion = true,
                StrongSelfEchoVetoRatio = 0.60,
                StrongSelfEchoVetoMinFrames = 5,
                RequireAssistantPlayback = true
            },
            FastNearEndDucking = new FastNearEndDuckingOptions
            {
                Enabled = true,
                MinSpeechMs = 50,
                MinVadConfidence = 0.2,
                MinEnergyRatioOverNoise = 1.0,
                MinAbsoluteEnergy = 0.001,
                UseSelfSpeechGate = true
            }
        };
    }

    private static void PushReferenceAudio(
        PlaybackReferenceTap tap,
        float amplitude,
        int sampleRate = 48000,
        int milliseconds = 1000)
    {
        var pcm = new byte[Math.Max(1, sampleRate * milliseconds / 1000) * 2];
        var sample = (short)Math.Clamp((int)Math.Round(amplitude * short.MaxValue), short.MinValue, short.MaxValue);
        for (var offset = 0; offset < pcm.Length; offset += 2)
        {
            pcm[offset] = (byte)(sample & 0xff);
            pcm[offset + 1] = (byte)((sample >> 8) & 0xff);
        }

        tap.PushPcm16Reference(pcm, sampleRate, 1, "correlation-1");
    }

    internal static TestFixture CreateFixture(
        BargeInOptions options,
        string transcript = "",
        IAcousticEchoCancellationService? aec = null,
        IBargeInVadService? vad = null,
        IInterruptionCaptureDiagnosticsWriter? captureDiagnosticsWriter = null,
        ISelfSpeechSuppressionGate? selfSpeechGate = null,
        ILiveUtteranceGate? liveUtteranceGate = null,
        ILogger<BargeInCoordinator>? logger = null,
        IBargeInDebugSnapshotService? debugSnapshots = null,
        ISpeechPresenceDetector? speechPresenceDetector = null,
        ISpeechPresenceDecisionLogSink? speechPresenceDecisionLogSink = null)
    {
        options.TriggerPostSpeechWaitMs = 0;
        var diagnostics = new NoOpBargeInDiagnosticsLogger();
        var tap = new PlaybackReferenceTap(diagnostics, new TestOptionsMonitor<BargeInOptions>(options));
        var stt = new FakeBargeInSttService(transcript);
        var playback = new FakePlaybackService();
        var liveTurnService = new LiveAssistantTurnService(NullLogger<LiveAssistantTurnService>.Instance);
        var speakerDucking = new TestSpeakerDuckingService();
        var defaultSelfSpeechGate = new SelfSpeechSuppressionGate(
            NullLogger<SelfSpeechSuppressionGate>.Instance,
            new NoOpSelfSpeechGateDiagnosticsWriter());
        var coordinator = new BargeInCoordinator(
            tap,
            tap,
            aec ?? new FakeActiveAecService(),
            vad ?? new BargeInVadService(),
            speakerDucking,
            selfSpeechGate ?? defaultSelfSpeechGate,
            new ContinuousMicAudioBuffer(),
            new BargeInTriggerBuffer(),
            stt,
            new InterruptionClassifier(),
            liveTurnService,
            playback,
            captureDiagnosticsWriter ?? new NoOpInterruptionCaptureDiagnosticsWriter(),
            diagnostics,
            logger ?? NullLogger<BargeInCoordinator>.Instance,
            new TestOptionsMonitor<BargeInOptions>(options),
            new TestOptionsMonitor<VoiceInputOptions>(new VoiceInputOptions()),
            liveUtteranceGate,
            debugSnapshots,
            speechPresenceDetector,
            speechPresenceDecisionLogSink);

        return new TestFixture(coordinator, tap, stt, playback, liveTurnService, speakerDucking);
    }

    private static LiveUtteranceGate CreateLiveUtteranceGate()
    {
        return new LiveUtteranceGate(
            NullLogger<LiveUtteranceGate>.Instance,
            Options.Create(new LiveUtteranceGateOptions()));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    internal static BargeInSpeechContext CreateContext()
    {
        return new BargeInSpeechContext
        {
            AssistantTurnId = "turn-1",
            CorrelationId = "correlation-1",
            SpeechType = SpeechPlaybackItemType.FinalAnswer,
            SpokenText = "This is a long answer."
        };
    }

    private static InterruptionCaptureDiagnostic CreateInterruptionCaptureDiagnostic(
        DateTimeOffset timestampUtc,
        string captureKind)
    {
        return new InterruptionCaptureDiagnostic
        {
            TimestampUtc = timestampUtc,
            CaptureKind = captureKind,
            AssistantTurnId = "turn-1",
            CorrelationId = "correlation-1",
            SpeechType = SpeechPlaybackItemType.FinalAnswer.ToString(),
            AssistantWasSpeaking = true,
            DuckingWasActive = true,
            FrameCount = 1,
            AudioMs = 10,
            PreRollMs = 450,
            RequestedPreRollMs = 450,
            ActualPreRollMsAvailable = 10,
            ActualPreRollMsIncluded = 10,
            PreRollFramesIncluded = 1,
            OldestBufferedFrameAgeMs = 10,
            BufferResetReason = "continuous_recorder",
            BufferOwnerAssistantTurnId = "turn-1",
            CurrentAssistantTurnId = "turn-1",
            PostPaddingMs = 450,
            MaxCaptureMs = 10000,
            CaptureEndReason = "suppressed_before_stt",
            FirstSpeechFrameRelativeMs = 0,
            LastSpeechFrameRelativeMs = 10,
            CapturedSpeechMs = 10,
            RawSpeechFrames = 1,
            AecSpeechFrames = 1,
            VadSpeechFrames = 1,
            CaptureSpeechFrames = 1,
            FalseSilenceFramesWhileVadSpeech = 0,
            CaptureStartUtc = DateTimeOffset.UnixEpoch,
            CaptureEndUtc = DateTimeOffset.UnixEpoch.AddMilliseconds(10),
            CaptureWallClockMs = 10,
            SttInputAudioMs = 10,
            ContinuousRecorderBufferMs = 1000,
            AnalysisFramesDropped = 0,
            ContinuousFramesDropped = 0,
            MaxProcessingLagMs = 0,
            AverageProcessingLagMs = 0,
            FrameGapCount = 0,
            MaxCaptureFrameGapMs = 0,
            BuiltFromContinuousRecorder = true,
            SampleRate = 16000,
            SampleCount = 160,
            SttTranscript = string.Empty,
            NormalizedTranscript = string.Empty,
            ClassificationType = InterruptionType.NoiseOrEcho.ToString(),
            ClassificationConfidence = 0,
            ClassificationReason = "suppressed",
            DecisionAction = BargeInAction.Ignore.ToString(),
            DecisionAccepted = false,
            DecisionReason = "suppressed",
            VadConfidence = 1,
            WasWakeWordPresent = false,
            IsAecDegraded = false,
            AecMode = AecMode.Active.ToString(),
            WavPath = null,
            JsonPath = null,
            FramesJsonlPath = null
        };
    }

    internal static BargeInAudioFrame CreateAudioFrame(float amplitude, int offsetMs)
    {
        return CreateAudioFrame(amplitude, DateTimeOffset.UnixEpoch, offsetMs);
    }

    internal static BargeInAudioFrame CreateAudioFrame(float amplitude, DateTimeOffset startedAt, int offsetMs)
    {
        return new BargeInAudioFrame
        {
            Samples = Enumerable.Repeat(amplitude, 160).ToArray(),
            SampleRate = 16000,
            Timestamp = startedAt.AddMilliseconds(offsetMs)
        };
    }

    private static int CalculateDurationMs(IReadOnlyList<BargeInAudioFrame> frames)
    {
        if (frames.Count == 0)
        {
            return 0;
        }

        return (int)Math.Round(frames.Sum(frame => frame.Samples.Length) * 1000.0 / frames[0].SampleRate);
    }

    private static BargeInAudioFrame CreateAlternatingAudioFrame(float amplitude, DateTimeOffset startedAt, int offsetMs)
    {
        var samples = Enumerable.Range(0, 160)
            .Select(index => index % 2 == 0 ? amplitude : -amplitude)
            .ToArray();
        return new BargeInAudioFrame
        {
            Samples = samples,
            SampleRate = 16000,
            Timestamp = startedAt.AddMilliseconds(offsetMs)
        };
    }

    internal sealed record TestFixture(
        BargeInCoordinator Coordinator,
        PlaybackReferenceTap Tap,
        FakeBargeInSttService Stt,
        FakePlaybackService Playback,
        LiveAssistantTurnService LiveTurnService,
        TestSpeakerDuckingService SpeakerDuckingService);

    private sealed class RecordingSpeechPresenceDetector : ISpeechPresenceDetector
    {
        private readonly SpeechPresenceState _officialState;
        private readonly SpeechPresenceState _branchState;
        private readonly List<SpeechPresenceEvidence> _evaluations = new();

        public RecordingSpeechPresenceDetector(
            SpeechPresenceState officialState = SpeechPresenceState.No,
            SpeechPresenceState branchState = SpeechPresenceState.Maybe)
        {
            _officialState = officialState;
            _branchState = branchState;
        }

        public IReadOnlyList<SpeechPresenceEvidence> Evaluations => _evaluations;

        public SpeechPresenceResult Evaluate(SpeechPresenceEvidence evidence)
        {
            _evaluations.Add(evidence);
            var state = evidence.SourcePath == "official_frame_decision"
                ? _officialState
                : _branchState;
            var isUserSpeaking = state is SpeechPresenceState.Maybe or SpeechPresenceState.Yes;
            return new SpeechPresenceResult
            {
                State = state,
                Confidence = isUserSpeaking ? 0.75 : 0.10,
                IsUserSpeaking = isUserSpeaking,
                ShouldYieldPlayback = isUserSpeaking && evidence.AssistantPlaybackActive,
                Reason = evidence.SourcePath,
                Evidence = evidence
            };
        }
    }

    private sealed class RecordingSpeechPresenceDecisionLogSink : ISpeechPresenceDecisionLogSink
    {
        private readonly List<SpeechPresenceOfficialDecision> _officialDecisions = new();
        private readonly List<SpeechPresenceBranchObservation> _branchObservations = new();

        public IReadOnlyList<SpeechPresenceOfficialDecision> OfficialDecisions => _officialDecisions;

        public IReadOnlyList<SpeechPresenceBranchObservation> BranchObservations => _branchObservations;

        public void TryLogOfficialDecision(SpeechPresenceOfficialDecision decision)
        {
            _officialDecisions.Add(decision);
        }

        public void TryLogBranchObservation(SpeechPresenceBranchObservation observation)
        {
            _branchObservations.Add(observation);
        }

        public void TryLogManualSpeechStartMarker(SpeechPresenceManualMarker marker)
        {
        }
    }

    private sealed class RecordingBargeInDebugSnapshotService : IBargeInDebugSnapshotService
    {
        private readonly List<BargeInDebugSnapshot> _published = new();

        public event Func<BargeInDebugSnapshot, CancellationToken, Task>? SnapshotAvailable;

        public bool IsEnabled => true;

        public IReadOnlyList<BargeInDebugSnapshot> Published => _published;

        public void Publish(BargeInDebugSnapshot snapshot, bool force = false)
        {
            _published.Add(snapshot);
            _ = SnapshotAvailable?.Invoke(snapshot, CancellationToken.None);
        }
    }

    internal sealed class FakeBargeInSttService : IBargeInSttService
    {
        private readonly string _transcript;

        public FakeBargeInSttService(string transcript)
        {
            _transcript = transcript;
        }

        public int CallCount { get; private set; }

        public IReadOnlyList<BargeInAudioFrame> LastFrames { get; private set; } = [];

        public TimeSpan LastAudioDuration { get; private set; }

        public Task<BargeInSttResult> TranscribeTriggerAsync(
            IReadOnlyList<BargeInAudioFrame> frames,
            BargeInOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastFrames = frames.ToArray();
            LastAudioDuration = frames.Count == 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(frames.Sum(frame => frame.Samples.Length) / (double)frames[0].SampleRate);
            return Task.FromResult(new BargeInSttResult
            {
                Transcript = _transcript,
                AudioDuration = LastAudioDuration
            });
        }
    }

    internal sealed class FakePlaybackService : IAssistantSpeechPlaybackService
    {
        public int ClearQueueCount { get; private set; }
        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }

        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            SpeechPlaybackItemType itemType = SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false)
        {
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            PauseCount++;
            return Task.CompletedTask;
        }

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            ResumeCount++;
            return Task.CompletedTask;
        }

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            ClearQueueCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActiveAecService : IAcousticEchoCancellationService
    {
        public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
        {
            return new AecProcessResult
            {
                EchoReducedFrame = microphoneFrame,
                Mode = AecMode.Active,
                IsEchoCancellationActive = true,
                Reason = "Fake active AEC for tests."
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeUnavailableAecService : IAcousticEchoCancellationService
    {
        public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
        {
            return new AecProcessResult
            {
                EchoReducedFrame = microphoneFrame,
                Mode = AecMode.Unavailable,
                IsEchoCancellationActive = false,
                Reason = "AEC unavailable in test."
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PassThroughThenSilenceAecService : IAcousticEchoCancellationService
    {
        private readonly int _passThroughFrames;
        private int _frameCount;

        public PassThroughThenSilenceAecService(int passThroughFrames)
        {
            _passThroughFrames = passThroughFrames;
        }

        public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
        {
            _frameCount++;
            return new AecProcessResult
            {
                EchoReducedFrame = _frameCount <= _passThroughFrames
                    ? microphoneFrame.ToArray()
                    : new float[microphoneFrame.Length],
                Mode = AecMode.Active,
                IsEchoCancellationActive = true,
                Reason = "Pass-through then silence AEC for capture continuation test."
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RewritingAecService : IAcousticEchoCancellationService
    {
        private readonly float _value;

        public RewritingAecService(float value)
        {
            _value = value;
        }

        public Task InitializeAsync(AecConfiguration config, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public AecProcessResult ProcessFrame(ReadOnlyMemory<float> microphoneFrame, ReadOnlyMemory<float> playbackReferenceFrame)
        {
            return new AecProcessResult
            {
                EchoReducedFrame = Enumerable.Repeat(_value, microphoneFrame.Length).ToArray(),
                Mode = AecMode.Active,
                IsEchoCancellationActive = true,
                Reason = "Rewritten echo-reduced frame for test."
            };
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class RecordingVadService : IBargeInVadService
    {
        public int CallCount { get; private set; }

        public float[] LastSamples { get; private set; } = [];

        public VadFrameResult ProcessFrame(VadFrameInput input, BargeInOptions options)
        {
            CallCount++;
            LastSamples = input.Samples.ToArray();
            return new VadFrameResult
            {
                IsSpeech = false,
                IsTriggered = false,
                Energy = 0,
                NoiseFloor = 0,
                Confidence = 0,
                ConsecutiveSpeechMs = 0
            };
        }

        public void Reset()
        {
        }
    }

    private sealed class AlwaysTriggeredVadService : IBargeInVadService
    {
        public VadFrameResult ProcessFrame(VadFrameInput input, BargeInOptions options)
        {
            return new VadFrameResult
            {
                IsSpeech = true,
                IsTriggered = true,
                Energy = 0.2,
                NoiseFloor = 0.01,
                Confidence = 1.0,
                ConsecutiveSpeechMs = 350
            };
        }

        public void Reset()
        {
        }
    }

    private sealed class AlwaysSpeechNeverTriggeredVadService : IBargeInVadService
    {
        public VadFrameResult ProcessFrame(VadFrameInput input, BargeInOptions options)
        {
            return new VadFrameResult
            {
                IsSpeech = true,
                IsTriggered = false,
                Energy = 0.2,
                NoiseFloor = 0.01,
                Confidence = 1.0,
                ConsecutiveSpeechMs = 350
            };
        }

        public void Reset()
        {
        }
    }

    private sealed class SequenceVadService : IBargeInVadService
    {
        private readonly bool[] _speech;
        private int _index;

        public SequenceVadService(params bool[] speech)
        {
            _speech = speech.Length == 0 ? [false] : speech;
        }

        public VadFrameResult ProcessFrame(VadFrameInput input, BargeInOptions options)
        {
            var isSpeech = _speech[Math.Min(_index, _speech.Length - 1)];
            _index++;
            return new VadFrameResult
            {
                IsSpeech = isSpeech,
                IsTriggered = false,
                Energy = isSpeech ? 0.2 : 0.001,
                NoiseFloor = 0.01,
                Confidence = isSpeech ? 1.0 : 0.0,
                ConsecutiveSpeechMs = isSpeech ? 350 : 0
            };
        }

        public void Reset()
        {
        }
    }

    internal sealed class TestSpeakerDuckingService : ISpeakerDuckingService
    {
        public event EventHandler<SpeakerDuckingChangedEventArgs>? DuckingChanged;

        public float CurrentVolumeMultiplier => IsDucked ? 0.2f : 1.0f;

        public bool IsDucked { get; private set; }

        public int StartCount { get; private set; }

        public int RestoreCount { get; private set; }

        public List<string> Reasons { get; } = [];

        public void StartDucking(BargeInSpeechContext context)
        {
            StartDucking(context, "test");
        }

        public void StartDucking(BargeInSpeechContext context, string reason)
        {
            if (IsDucked)
            {
                return;
            }

            IsDucked = true;
            StartCount++;
            Reasons.Add(reason);
            DuckingChanged?.Invoke(this, new SpeakerDuckingChangedEventArgs
            {
                IsDucked = true,
                VolumeMultiplier = CurrentVolumeMultiplier,
                Reason = reason,
                FadeDuration = TimeSpan.Zero,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });
        }

        public void Restore(BargeInSpeechContext context, string reason)
        {
            if (!IsDucked)
            {
                return;
            }

            IsDucked = false;
            RestoreCount++;
            Reasons.Add(reason);
            DuckingChanged?.Invoke(this, new SpeakerDuckingChangedEventArgs
            {
                IsDucked = false,
                VolumeMultiplier = CurrentVolumeMultiplier,
                Reason = reason,
                FadeDuration = TimeSpan.Zero,
                ChangedAtUtc = DateTimeOffset.UtcNow
            });
        }
    }

    internal sealed class NoOpBargeInDiagnosticsLogger : IBargeInDiagnosticsLogger
    {
        public void MonitorStarted(BargeInSpeechContext context, AecMode aecMode) { }
        public void MonitorStopped(BargeInSpeechContext context) { }
        public void AecInitialized(AecMode mode, string reason) { }
        public void PlaybackReferenceFrameReceived(string? correlationId, int sampleCount) { }
        public void MicFrameProcessed(BargeInSpeechContext context, long frameCount) { }
        public void EchoReducedFrameProcessed(BargeInSpeechContext context, long frameCount, AecMode aecMode) { }
        public void VadPossibleSpeech(BargeInSpeechContext context, VadFrameResult result, AecMode aecMode) { }
        public void TriggerBufferCaptured(BargeInSpeechContext context, int frameCount, TimeSpan duration) { }
        public void GatedSttStarted(BargeInSpeechContext context, TimeSpan duration) { }
        public void GatedSttResult(BargeInSpeechContext context, BargeInSttResult result) { }
        public void ClassificationResult(BargeInSpeechContext context, InterruptionClassificationResult result) { }
        public void StateChanged(BargeInSpeechContext context, BargeInState state, string reason) { }
        public void ActionSelected(BargeInSpeechContext context, BargeInAction action, string reason) { }
        public void PlaybackResumed(BargeInSpeechContext context, string reason) { }
        public void CorrectionRegenerationStarted(BargeInSpeechContext context, string correctionText) { }
        public void Ignored(BargeInSpeechContext context, string reason) { }
        public void Accepted(BargeInSpeechContext context, InterruptionClassificationResult result) { }
        public void AssistantTurnCancelled(BargeInSpeechContext context, InterruptionClassificationResult result) { }
    }

    private sealed class NoOpSelfSpeechGateDiagnosticsWriter : ISelfSpeechGateDiagnosticsWriter
    {
        public void Write(SelfSpeechGateDiagnosticEntry entry, BargeInOptions options)
        {
        }
    }

    private sealed class NoOpInterruptionCaptureDiagnosticsWriter : IInterruptionCaptureDiagnosticsWriter
    {
        public Task SaveAsync(
            InterruptionCaptureDiagnostic diagnostic,
            IReadOnlyList<BargeInAudioFrame> frames,
            IReadOnlyList<InterruptionCaptureFrameDiagnostic> frameDiagnostics,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingInterruptionCaptureDiagnosticsWriter : IInterruptionCaptureDiagnosticsWriter
    {
        public int CallCount { get; private set; }

        public InterruptionCaptureDiagnostic? LastDiagnostic { get; private set; }

        public IReadOnlyList<BargeInAudioFrame> LastFrames { get; private set; } = [];

        public IReadOnlyList<InterruptionCaptureFrameDiagnostic> LastFrameDiagnostics { get; private set; } = [];

        public Task SaveAsync(
            InterruptionCaptureDiagnostic diagnostic,
            IReadOnlyList<BargeInAudioFrame> frames,
            IReadOnlyList<InterruptionCaptureFrameDiagnostic> frameDiagnostics,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastDiagnostic = diagnostic;
            LastFrames = frames.ToArray();
            LastFrameDiagnostics = frameDiagnostics.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class SequenceSelfSpeechGate : ISelfSpeechSuppressionGate
    {
        private readonly SelfSpeechDecision[] _decisions;
        private readonly string _reason;
        private readonly string? _correlationDecision;
        private readonly double? _correlationScore;
        private int _index;

        public SequenceSelfSpeechGate(params SelfSpeechDecision[] decisions)
            : this(decisions, "test gate result", null, null)
        {
        }

        public SequenceSelfSpeechGate(
            SelfSpeechDecision decision,
            string reason,
            string? correlationDecision,
            double? correlationScore)
            : this([decision], reason, correlationDecision, correlationScore)
        {
        }

        private SequenceSelfSpeechGate(
            SelfSpeechDecision[] decisions,
            string reason,
            string? correlationDecision,
            double? correlationScore)
        {
            _decisions = decisions.Length == 0 ? [SelfSpeechDecision.Allow] : decisions;
            _reason = reason;
            _correlationDecision = correlationDecision;
            _correlationScore = correlationScore;
        }

        public SelfSpeechGateResult Evaluate(SelfSpeechGateInput input, BargeInOptions options)
        {
            var decision = _decisions[_index++ % _decisions.Length];
            return new SelfSpeechGateResult
            {
                Decision = decision,
                Confidence = decision is SelfSpeechDecision.Allow ? 0.8 : 0.5,
                Reason = _reason,
                MicEnergy = input.MicEnergy,
                PlaybackEnergy = input.PlaybackEnergy,
                EstimatedEchoEnergy = input.PlaybackEnergy * options.SelfSpeechSuppression.EchoLeakageMultiplier,
                UserSpeechScore = decision is SelfSpeechDecision.Allow ? 1.0 : 0.4,
                SustainedUncertainFrames = decision is SelfSpeechDecision.Uncertain ? 1 : 0,
                CorrelationScore = _correlationScore,
                CorrelationDecision = _correlationDecision
            };
        }

        public void Reset()
        {
            _index = 0;
        }
    }

    private sealed class ScoreSequenceSelfSpeechGate : ISelfSpeechSuppressionGate
    {
        private readonly double[] _scores;
        private int _index;

        public ScoreSequenceSelfSpeechGate(params double[] scores)
        {
            _scores = scores.Length == 0 ? [1.0] : scores;
        }

        public SelfSpeechGateResult Evaluate(SelfSpeechGateInput input, BargeInOptions options)
        {
            var score = _scores[Math.Min(_index, _scores.Length - 1)];
            _index++;
            return new SelfSpeechGateResult
            {
                Decision = SelfSpeechDecision.Allow,
                Confidence = 0.9,
                Reason = "test user speech score gate result",
                MicEnergy = input.MicEnergy,
                PlaybackEnergy = input.PlaybackEnergy,
                EstimatedEchoEnergy = input.PlaybackEnergy * options.SelfSpeechSuppression.EchoLeakageMultiplier,
                UserSpeechScore = score,
                SustainedUncertainFrames = 0,
                CorrelationScore = input.CorrelationScore,
                BestDelayMs = input.BestDelayMs,
                CorrelationDecision = input.CorrelationDecision
            };
        }

        public void Reset()
        {
            _index = 0;
        }
    }

    private sealed class FrameScoreSequenceSelfSpeechGate : ISelfSpeechSuppressionGate
    {
        private readonly double[] _scores;
        private int _index;
        private DateTimeOffset? _currentTimestamp;
        private double _currentScore;

        public FrameScoreSequenceSelfSpeechGate(params double[] scores)
        {
            _scores = scores.Length == 0 ? [1.0] : scores;
            _currentScore = _scores[0];
        }

        public SelfSpeechGateResult Evaluate(SelfSpeechGateInput input, BargeInOptions options)
        {
            if (_currentTimestamp != input.Timestamp)
            {
                _currentTimestamp = input.Timestamp;
                _currentScore = _scores[Math.Min(_index, _scores.Length - 1)];
                _index++;
            }

            var decision = _currentScore >= 0.9
                ? SelfSpeechDecision.Allow
                : SelfSpeechDecision.Uncertain;
            return new SelfSpeechGateResult
            {
                Decision = decision,
                Confidence = 0.9,
                Reason = "test frame user speech score gate result",
                MicEnergy = input.MicEnergy,
                PlaybackEnergy = input.PlaybackEnergy,
                EstimatedEchoEnergy = input.PlaybackEnergy * options.SelfSpeechSuppression.EchoLeakageMultiplier,
                UserSpeechScore = _currentScore,
                SustainedUncertainFrames = decision is SelfSpeechDecision.Uncertain ? 1 : 0,
                CorrelationScore = input.CorrelationScore,
                BestDelayMs = input.BestDelayMs,
                CorrelationDecision = input.CorrelationDecision
            };
        }

        public void Reset()
        {
            _index = 0;
            _currentTimestamp = null;
            _currentScore = _scores[0];
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

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
            ContentRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Merlin.Backend.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
