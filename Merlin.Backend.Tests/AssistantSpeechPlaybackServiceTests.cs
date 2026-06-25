using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AssistantSpeechPlaybackServiceTests
{
    [Fact]
    public async Task BeginProvisionalAudioHoldAsync_PausesActivePlaybackWithoutCancellationSideEffects()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var tracker = new RecordingSpokenAnswerTrackingService();
        var events = new List<string>();
        var playback = CreatePlayback(tts, wave, tracker);

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (visualEvent, _) =>
            {
                events.Add(visualEvent.Event);
                return Task.CompletedTask;
            },
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));

        var result = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");
        var snapshot = playback.GetActivePlaybackSnapshot();

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.HoldId));
        Assert.Equal(1, wave.PauseCount);
        Assert.Equal(0, tracker.PlaybackCancelledCount);
        Assert.Equal(0, tracker.CompletedCount);
        Assert.DoesNotContain("SPEAKING_CANCELLED", events);
        Assert.NotNull(snapshot);
        Assert.True(snapshot!.IsActive);
        Assert.True(snapshot.IsHeld);
        Assert.False(snapshot.IsAudiblePlaybackActive);
        Assert.Equal(result.HoldId, snapshot.HoldId);
    }

    [Fact]
    public async Task ResumeProvisionalAudioHoldAsync_ContinuesSamePlaybackItem()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var playback = CreatePlayback(tts, wave);

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var hold = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");
        var resumed = await playback.ResumeProvisionalAudioHoldAsync(hold.HoldId!, "test_resume");
        wave.DrainAll();

        Assert.True(resumed.Success);
        Assert.True(resumed.WasResumed);
        Assert.Equal(2, wave.PlayCount);
        Assert.Equal(1, wave.PauseCount);
        Assert.True(await tts.WaitUntilCompletedAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task FlushProvisionalAudioHoldAsync_CancelsAndInvalidatesHeldPlayback()
    {
        var tts = new BlockingAfterAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var playback = CreatePlayback(tts, wave);

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var hold = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");

        var flushed = await playback.FlushProvisionalAudioHoldAsync(hold.HoldId!, "test_flush");
        tts.Release();

        Assert.True(flushed.Success);
        Assert.True(flushed.WasFlushed);
        Assert.True(await tts.WaitUntilCompletedAsync(TimeSpan.FromSeconds(2)));
        Assert.Null(playback.GetActivePlaybackSnapshot());
    }

    [Fact]
    public async Task BeginProvisionalAudioHoldAsync_WhenAlreadyHeld_ReturnsSameHold()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var playback = CreatePlayback(tts, wave);

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var first = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");
        var second = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold_again");

        Assert.True(second.Success);
        Assert.True(second.WasAlreadyHeld);
        Assert.Equal(first.HoldId, second.HoldId);
        Assert.Equal(1, wave.PauseCount);
    }

    [Fact]
    public async Task ProvisionalAudioHoldTimeout_ResumesByDefault()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var playback = CreatePlayback(
            tts,
            wave,
            options: new InterruptionHandlingOptions { ProvisionalAudioHoldTimeoutMs = 50 });

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var hold = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");

        Assert.True(hold.Success);
        Assert.True(await wave.WaitForPlayCountAsync(2, TimeSpan.FromSeconds(2)));
        var snapshot = playback.GetActivePlaybackSnapshot();
        Assert.NotNull(snapshot);
        Assert.False(snapshot!.IsHeld);
        Assert.True(snapshot.IsAudiblePlaybackActive);
    }

    [Fact]
    public async Task BeginProvisionalAudioHoldAsync_DoesNotClearSpokenAnswerTracker()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var tracker = new RecordingSpokenAnswerTrackingService();
        var playback = CreatePlayback(tts, wave, tracker);

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var hold = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");

        Assert.True(hold.Success);
        Assert.Equal(0, tracker.PlaybackCancelledCount);
        Assert.Equal(0, tracker.CompletedCount);
    }

    [Fact]
    public async Task FlushProvisionalAudioHoldAsync_ReleasesGateForNextSpeech()
    {
        var tts = new BlockingAfterAudioVoiceSynthesisService();
        var wavePlayers = new Queue<RecordingWavePlayer>([
            new RecordingWavePlayer(),
            new RecordingWavePlayer()
        ]);
        var playback = CreatePlayback(tts, wavePlayerFactory: () => wavePlayers.Dequeue());
        var firstWave = wavePlayers.Peek();

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await firstWave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var hold = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");

        await playback.EnqueueAsync(
            "stop confirmation",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.StopConfirmation);

        Assert.Single(wavePlayers);
        var secondWave = wavePlayers.Peek();
        Assert.Equal(0, secondWave.PlayCount);

        await playback.FlushProvisionalAudioHoldAsync(hold.HoldId!, "test_flush");
        tts.Release();

        Assert.True(await secondWave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task FlushFinalAnswerSpeechForTurnAsync_DiscardsLateOldFinalAnswerAudio()
    {
        var tts = new DelayedAudioVoiceSynthesisService();
        var tap = new RecordingPlaybackReferenceTap();
        var playback = new AssistantSpeechPlaybackService(
            tts,
            tap,
            new NoOpSpeakerDuckingService(),
            NullLogger<AssistantSpeechPlaybackService>.Instance);

        await playback.EnqueueAsync(
            "old final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await tts.WaitUntilEnteredAsync(TimeSpan.FromSeconds(2)));
        await playback.FlushFinalAnswerSpeechForTurnAsync("turn-1", "test_invalidation");
        tts.ReleaseAudio();
        Assert.True(await tts.WaitUntilCompletedAsync(TimeSpan.FromSeconds(2)));

        Assert.Equal(1, tts.CallCount);
        Assert.Equal(0, tap.SpeechStartedCount);
    }

    private static AssistantSpeechPlaybackService CreatePlayback(
        IVoiceSynthesisService tts,
        RecordingWavePlayer? wavePlayer = null,
        ILiveSpokenAnswerTrackingService? tracker = null,
        InterruptionHandlingOptions? options = null,
        Func<IWavePlayer>? wavePlayerFactory = null) =>
        new(
            tts,
            new RecordingPlaybackReferenceTap(),
            new NoOpSpeakerDuckingService(),
            NullLogger<AssistantSpeechPlaybackService>.Instance,
            tracker,
            Options.Create(options ?? new InterruptionHandlingOptions()),
            wavePlayerFactory ?? (() => wavePlayer ?? new RecordingWavePlayer()));

    private sealed class ImmediateAudioVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                await onMetadataAsync(CreateMetadata(), cancellationToken);
                await onAudioAsync(CreateAudio(milliseconds: 300), cancellationToken);
            }
            finally
            {
                _completed.TrySetResult();
            }
        }

        public async Task<bool> WaitUntilCompletedAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_completed.Task, Task.Delay(timeout));
            return completed == _completed.Task;
        }
    }

    private sealed class BlockingAfterAudioVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                await onMetadataAsync(CreateMetadata(), cancellationToken);
                await onAudioAsync(CreateAudio(milliseconds: 300), cancellationToken);
                await _release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                _completed.TrySetResult();
            }
        }

        public void Release() => _release.TrySetResult();

        public async Task<bool> WaitUntilCompletedAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_completed.Task, Task.Delay(timeout));
            return completed == _completed.Task;
        }
    }

    private static VoiceSynthesisStreamMetadata CreateMetadata() => new()
    {
        SampleRate = 48000,
        Channels = 1,
        Format = "s16le"
    };

    private static byte[] CreateAudio(int milliseconds)
    {
        var sampleCount = 48000 * milliseconds / 1000;
        return new byte[sampleCount * sizeof(short)];
    }

    private sealed class RecordingWavePlayer : IWavePlayer
    {
        private readonly TaskCompletionSource _played = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IWaveProvider? _provider;
        private int _playCount;

        public event EventHandler<StoppedEventArgs>? PlaybackStopped;

        public int PlayCount => _playCount;

        public int PauseCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public PlaybackState PlaybackState { get; private set; } = PlaybackState.Stopped;

        public float Volume { get; set; } = 1.0f;

        public WaveFormat OutputWaveFormat => _provider?.WaveFormat ?? new WaveFormat(48000, 16, 1);

        public void Init(IWaveProvider waveProvider) => _provider = waveProvider;

        public void Play()
        {
            Interlocked.Increment(ref _playCount);
            PlaybackState = PlaybackState.Playing;
            _played.TrySetResult();
        }

        public void Pause()
        {
            PauseCount++;
            PlaybackState = PlaybackState.Paused;
        }

        public void Stop()
        {
            StopCount++;
            PlaybackState = PlaybackState.Stopped;
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }

        public void Dispose()
        {
            DisposeCount++;
        }

        public async Task<bool> WaitForPlayAsync(TimeSpan timeout) => await WaitForPlayCountAsync(1, timeout);

        public async Task<bool> WaitForPlayCountAsync(int count, TimeSpan timeout)
        {
            var start = DateTimeOffset.UtcNow;
            while (DateTimeOffset.UtcNow - start < timeout)
            {
                if (PlayCount >= count)
                {
                    return true;
                }

                await Task.Delay(10);
            }

            return false;
        }

        public void DrainAll()
        {
            if (_provider is null)
            {
                return;
            }

            var buffer = new byte[8192];
            for (var index = 0; index < 1000; index++)
            {
                var read = _provider.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    return;
                }
            }
        }
    }

    private sealed class RecordingSpokenAnswerTrackingService : ILiveSpokenAnswerTrackingService
    {
        public int PlaybackCancelledCount { get; private set; }

        public int CompletedCount { get; private set; }

        public void StartAnswer(string turnId, string correlationId, string originalUserQuestion, string? originalAssistantDraft = null, string? currentTopicLabel = null)
        {
        }

        public void MarkChunkStarted(string turnId, string text, TimeSpan? playbackPosition = null)
        {
        }

        public void MarkChunkCompleted(string turnId, string text, TimeSpan? playbackPosition = null)
        {
        }

        public void MarkPlaybackCancelled(string turnId, string reason) => PlaybackCancelledCount++;

        public void CompleteAnswer(string turnId) => CompletedCount++;

        public SpokenAnswerCheckpoint? TryCreateCheckpoint(string turnId, bool discardCurrentPartialSentence = true) => null;
    }

    private sealed class DelayedAudioVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseAudio = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            CallCount++;
            _entered.TrySetResult();
            try
            {
                await _releaseAudio.Task.WaitAsync(cancellationToken);
                await onAudioAsync(new byte[] { 0, 0, 0, 0 }, cancellationToken);
            }
            finally
            {
                _completed.TrySetResult();
            }
        }

        public async Task<bool> WaitUntilEnteredAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_entered.Task, Task.Delay(timeout));
            return completed == _entered.Task;
        }

        public async Task<bool> WaitUntilCompletedAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_completed.Task, Task.Delay(timeout));
            return completed == _completed.Task;
        }

        public void ReleaseAudio() => _releaseAudio.TrySetResult();
    }

    private sealed class RecordingPlaybackReferenceTap : IPlaybackReferenceTap
    {
        public event EventHandler<BargeInSpeechContext>? SpeechStarted;

        public event EventHandler<BargeInSpeechContext>? SpeechStopped;

        public int SpeechStartedCount { get; private set; }

        public void NotifySpeechStarted(BargeInSpeechContext context)
        {
            SpeechStartedCount++;
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

        public ReadOnlyMemory<float> GetLatestReferenceFrame(int sampleCount) => new float[sampleCount];

        public bool TryGetReferenceWindow(int delayMs, int sampleCount, Span<float> destination) => false;

        public PlaybackReferenceDebugSnapshot GetDebugSnapshot() => new()
        {
            IsPlaybackActive = false,
            SampleRate = 16000,
            BufferedSamples = 0,
            CapacitySamples = 0,
            BufferedMilliseconds = 0,
            CurrentPlaybackEnergy = 0,
            RecentPlaybackEnergy = 0,
            WritePosition = 0,
            PlaybackStartedAt = null,
            PlaybackReferenceIsConsumptionAligned = false,
            PlaybackConsumedSamplesTotal = 0,
            ReferenceBufferedMilliseconds = 0,
            LastOutputReadSamples = 0,
            LastOutputReadDurationMilliseconds = 0
        };
    }

    private sealed class NoOpSpeakerDuckingService : ISpeakerDuckingService
    {
        public event EventHandler<SpeakerDuckingChangedEventArgs>? DuckingChanged
        {
            add { }
            remove { }
        }

        public float CurrentVolumeMultiplier => 1.0f;

        public bool IsDucked => false;

        public void StartDucking(BargeInSpeechContext context)
        {
        }

        public void StartDucking(BargeInSpeechContext context, string reason)
        {
        }

        public void Restore(BargeInSpeechContext context, string reason)
        {
        }
    }
}
