using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
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
    [InlineData("wait", InterruptionType.Pause)]
    [InlineData("pause", InterruptionType.Pause)]
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

        var withoutWakeWord = classifier.Classify(CreateInput("stop", isAecDegraded: true), new BargeInOptions());
        var withWakeWord = classifier.Classify(CreateInput("merlin stop", isAecDegraded: true), new BargeInOptions());

        Assert.Equal(InterruptionType.NoiseOrEcho, withoutWakeWord.Type);
        Assert.Equal(InterruptionType.HardStop, withWakeWord.Type);
    }

    private static InterruptionClassificationInput CreateInput(string transcript, bool isAecDegraded)
    {
        var normalized = InterruptionClassifier.Normalize(transcript);
        return new InterruptionClassificationInput
        {
            RawTranscript = transcript,
            NormalizedTranscript = normalized,
            AssistantTurnId = "turn-1",
            CurrentSpeechType = "FinalAnswer",
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
    public async Task ProcessMicrophoneFrame_HardStop_CancelsCurrentTurn()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin stop");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Backchannel_DoesNotCancelCurrentTurn()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin yeah");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(1, fixture.Playback.PauseCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
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

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(1, fixture.Playback.PauseCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Correction_CancelsCurrentTurn()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin no i mean beam");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(1, fixture.Playback.ClearQueueCount);
        Assert.Equal(1, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Playback.ResumeCount);
    }

    [Fact]
    public async Task ProcessMicrophoneFrame_Clarification_ResumesAndDoesNotCancel()
    {
        var fixture = CreateFixture(new BargeInOptions { Enabled = true }, "merlin what does that mean");
        fixture.Tap.NotifySpeechStarted(CreateContext());

        await SendTriggeredSpeechAsync(fixture.Coordinator);
        await WaitUntilAsync(() => fixture.Stt.CallCount > 0);

        Assert.Equal(1, fixture.Stt.CallCount);
        Assert.Equal(0, fixture.Playback.ClearQueueCount);
        Assert.Equal(1, fixture.Playback.ResumeCount);
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

    private static async Task SendTriggeredSpeechAsync(IBargeInCoordinator coordinator)
    {
        for (var index = 0; index < 40; index++)
        {
            await coordinator.ProcessMicrophoneFrameAsync(CreateAudioFrame(0.16f, index * 10));
        }
    }

    internal static TestFixture CreateFixture(
        BargeInOptions options,
        string transcript = "",
        IAcousticEchoCancellationService? aec = null,
        IBargeInVadService? vad = null)
    {
        options.TriggerPostSpeechWaitMs = 0;
        var diagnostics = new NoOpBargeInDiagnosticsLogger();
        var tap = new PlaybackReferenceTap(diagnostics, new TestOptionsMonitor<BargeInOptions>(options));
        var stt = new FakeBargeInSttService(transcript);
        var playback = new FakePlaybackService();
        var coordinator = new BargeInCoordinator(
            tap,
            aec ?? new FakeActiveAecService(),
            vad ?? new BargeInVadService(),
            new TestSpeakerDuckingService(),
            new BargeInTriggerBuffer(),
            stt,
            new InterruptionClassifier(),
            playback,
            diagnostics,
            new TestOptionsMonitor<BargeInOptions>(options));

        return new TestFixture(coordinator, tap, stt, playback);
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

    internal static BargeInAudioFrame CreateAudioFrame(float amplitude, int offsetMs)
    {
        return new BargeInAudioFrame
        {
            Samples = Enumerable.Repeat(amplitude, 160).ToArray(),
            SampleRate = 16000,
            Timestamp = DateTimeOffset.UnixEpoch.AddMilliseconds(offsetMs)
        };
    }

    internal sealed record TestFixture(
        BargeInCoordinator Coordinator,
        PlaybackReferenceTap Tap,
        FakeBargeInSttService Stt,
        FakePlaybackService Playback);

    internal sealed class FakeBargeInSttService : IBargeInSttService
    {
        private readonly string _transcript;

        public FakeBargeInSttService(string transcript)
        {
            _transcript = transcript;
        }

        public int CallCount { get; private set; }

        public Task<BargeInSttResult> TranscribeTriggerAsync(
            IReadOnlyList<BargeInAudioFrame> frames,
            BargeInOptions options,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new BargeInSttResult
            {
                Transcript = _transcript,
                AudioDuration = TimeSpan.FromMilliseconds(500)
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

    private sealed class TestSpeakerDuckingService : ISpeakerDuckingService
    {
        public float CurrentVolumeMultiplier => IsDucked ? 0.2f : 1.0f;

        public bool IsDucked { get; private set; }

        public void StartDucking(BargeInSpeechContext context)
        {
            IsDucked = true;
        }

        public void Restore(BargeInSpeechContext context, string reason)
        {
            IsDucked = false;
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
