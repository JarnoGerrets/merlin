using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Services.StreamingResponses;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AssistantSpeechPlaybackServiceTests
{
    [Fact]
    public async Task EnqueueAsync_FinalAnswer_SanitizesTextBeforeVoiceSynthesis()
    {
        var tts = new RecordingTextVoiceSynthesisService();
        var playback = CreatePlayback(tts, ttsTextSanitizer: new TtsTextSanitizer());

        await playback.EnqueueAsync(
            "Compared to petrol cars, **Better (Advantages of EVs):** 1.. Lower running costs. 2. Quiet driving.",
            "turn-sanitize",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await tts.WaitUntilCompletedAsync(TimeSpan.FromSeconds(2)));
        Assert.NotNull(tts.LastText);
        Assert.DoesNotContain("**", tts.LastText);
        Assert.DoesNotContain("1..", tts.LastText);
        Assert.DoesNotContain("EVs", tts.LastText);
        Assert.Contains("Better: advantages of electric vehicles.", tts.LastText);
    }

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
    public async Task BeginProvisionalAudioHoldAsync_EmitsCanonicalListeningState()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);

        await playback.EnqueueAsync(
            "held final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");

        Assert.Contains(sink.Events, uiState =>
            uiState.BaseState == "listening"
            && uiState.Reason == "provisional_audio_hold_started"
            && uiState.InterruptionState == "held_for_user_speech"
            && !uiState.AudiblePlaybackActive);
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
    public async Task ProvisionalAudioHoldTimeout_DoesNotResumeWhileInterruptionSttPending()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var scheduler = new RecordingGpuWorkScheduler { HasPendingInterruptionSttValue = true };
        var playback = CreatePlayback(
            tts,
            wave,
            options: new InterruptionHandlingOptions { ProvisionalAudioHoldTimeoutMs = 50 },
            gpuWorkScheduler: scheduler);

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
        await Task.Delay(180);
        Assert.Equal(1, wave.PlayCount);
        var heldSnapshot = playback.GetActivePlaybackSnapshot();
        Assert.NotNull(heldSnapshot);
        Assert.True(heldSnapshot!.IsHeld);

        scheduler.HasPendingInterruptionSttValue = false;

        Assert.True(await wave.WaitForPlayCountAsync(2, TimeSpan.FromSeconds(2)));
        var resumedSnapshot = playback.GetActivePlaybackSnapshot();
        Assert.NotNull(resumedSnapshot);
        Assert.False(resumedSnapshot!.IsHeld);
        Assert.True(resumedSnapshot.IsAudiblePlaybackActive);
    }

    [Fact]
    public async Task FinalAnswerFutureTtsChunksSuspendWhileProvisionalHoldIsActive()
    {
        var tts = new TwoChunkVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var playback = CreatePlayback(tts, wave);

        await playback.EnqueueAsync(
            "held final answer with multiple chunks",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        var hold = await playback.BeginProvisionalAudioHoldAsync("turn-1", "test_hold");
        Assert.True(hold.Success);

        tts.ReleaseSecondChunk(250);

        Assert.False(await tts.WaitUntilCompletedAsync(TimeSpan.FromMilliseconds(180)));

        var resumed = await playback.ResumeProvisionalAudioHoldAsync(hold.HoldId!, "test_resume");

        Assert.True(resumed.Success);
        Assert.True(await tts.WaitUntilCompletedAsync(TimeSpan.FromSeconds(2)));
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
    public async Task StopConfirmation_IgnoresQueueGenerationChangeAfterStop()
    {
        var tts = new BlockingAfterAudioVoiceSynthesisService();
        var logger = new RecordingLogger<AssistantSpeechPlaybackService>();
        var wavePlayers = new Queue<RecordingWavePlayer>([
            new RecordingWavePlayer(),
            new RecordingWavePlayer()
        ]);
        var playback = CreatePlayback(
            tts,
            logger: logger,
            wavePlayerFactory: () => wavePlayers.Dequeue());
        var finalAnswerWave = wavePlayers.Peek();

        await playback.EnqueueAsync(
            "old final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await finalAnswerWave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        await playback.EnqueueAsync(
            "Got it, I'll stop.",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.StopConfirmation);

        Assert.Single(wavePlayers);
        var stopConfirmationWave = wavePlayers.Peek();

        await playback.ClearQueueAsync();
        tts.Release();

        Assert.True(await stopConfirmationWave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        stopConfirmationWave.DrainAll();
        await WaitUntilAsync(() => logger.Messages.Any(message => message.Contains("stop_confirmation_playback_completed", StringComparison.Ordinal)));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_generation_mismatch_ignored", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_playback_started", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_tts_started", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_output_opened", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_audio_write_started", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_audio_write_completed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_tts_completed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_output_drain_started", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_output_drain_completed", StringComparison.Ordinal));
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_playback_completed", StringComparison.Ordinal));
        Assert.DoesNotContain(
            logger.Messages,
            message => message.Contains("Speech playback item skipped because queue generation changed", StringComparison.Ordinal)
                && message.Contains("ItemType: StopConfirmation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlaybackEmitsCanonicalSpeakingOnlyWhenAudioStarts()
    {
        var tts = new DelayedMetadataAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);

        await playback.EnqueueAsync(
            "delayed final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await tts.WaitUntilEnteredAsync(TimeSpan.FromSeconds(2)));
        Assert.DoesNotContain(sink.Events, uiState => uiState.BaseState == "speaking");

        tts.ReleaseAudio(milliseconds: 250);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        Assert.Contains(sink.Events, uiState =>
            uiState.BaseState == "speaking"
            && uiState.Reason == "audio_playback_started"
            && uiState.SpeechItemType == "final_answer"
            && uiState.AudiblePlaybackActive);
    }

    [Fact]
    public async Task FinalAnswerIntermediateChunkGapEmitsIdleTtsChunkGap()
    {
        var tts = new TwoChunkVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);

        await playback.EnqueueAsync(
            "two chunk final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        wave.DrainAll();

        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "idle"
            && uiState.Reason == "tts_chunk_gap"
            && uiState.SpeechItemType == "final_answer"
            && !uiState.AudiblePlaybackActive));
        Assert.DoesNotContain(sink.Events, uiState => uiState.Reason == "final_answer_completed");
    }

    [Fact]
    public async Task FinalAnswerFinalCompletionEmitsIdleFinalAnswerCompletedAndKeepsDistinctIdleReasons()
    {
        var tts = new TwoChunkVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);

        await playback.EnqueueAsync(
            "two chunk final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        wave.DrainAll();
        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "idle"
            && uiState.Reason == "tts_chunk_gap"
            && uiState.SpeechItemType == "final_answer"));

        tts.ReleaseSecondChunk(milliseconds: 250);

        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "speaking"
            && uiState.Reason == "audio_playback_started"
            && uiState.SpeechItemType == "final_answer",
            expectedCount: 2));
        wave.DrainAll();
        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "idle"
            && uiState.Reason == "final_answer_completed"
            && uiState.SpeechItemType == "final_answer"
            && !uiState.AudiblePlaybackActive));

        Assert.Contains(sink.Events, uiState =>
            uiState.BaseState == "idle" && uiState.Reason == "tts_chunk_gap");
        Assert.Contains(sink.Events, uiState =>
            uiState.BaseState == "idle" && uiState.Reason == "final_answer_completed");
    }

    [Fact]
    public async Task ProgressCompletionEmitsThinking()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);

        await playback.EnqueueAsync(
            "still working",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.Progress);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        wave.DrainAll();

        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "thinking"
            && uiState.Reason == "progress_playback_completed"
            && uiState.SpeechItemType == "progress"));
    }

    [Fact]
    public async Task StopConfirmationEmitsCanonicalSpeakingThenIdle()
    {
        var tts = new ImmediateAudioVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);

        await playback.EnqueueAsync(
            "Got it, I'll stop.",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.StopConfirmation);

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        wave.DrainAll();

        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "speaking"
            && uiState.Reason == "stop_confirmation_playback_started"
            && uiState.SpeechItemType == "stop_confirmation"));
        Assert.True(await sink.WaitUntilAsync(uiState =>
            uiState.BaseState == "idle"
            && uiState.Reason == "stop_confirmation_playback_completed"
            && uiState.SpeechItemType == "stop_confirmation"));
    }

    [Fact]
    public async Task NonStopPlayback_StillSkipsWhenQueueGenerationChanges()
    {
        var tts = new BlockingAfterAudioVoiceSynthesisService();
        var logger = new RecordingLogger<AssistantSpeechPlaybackService>();
        var wavePlayers = new Queue<RecordingWavePlayer>([
            new RecordingWavePlayer(),
            new RecordingWavePlayer()
        ]);
        var playback = CreatePlayback(
            tts,
            logger: logger,
            wavePlayerFactory: () => wavePlayers.Dequeue());
        var finalAnswerWave = wavePlayers.Peek();

        await playback.EnqueueAsync(
            "old final answer",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.FinalAnswer);

        Assert.True(await finalAnswerWave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        await playback.EnqueueAsync(
            "still working",
            "turn-1",
            (_, _) => Task.CompletedTask,
            speechCacheKey: null,
            isReplayableSpeech: null,
            CancellationToken.None,
            SpeechPlaybackItemType.Progress);

        Assert.Single(wavePlayers);
        var progressWave = wavePlayers.Peek();

        await playback.ClearQueueAsync();
        tts.Release();
        await Task.Delay(150);

        Assert.Equal(0, progressWave.PlayCount);
        Assert.Contains(
            logger.Messages,
            message => message.Contains("Speech playback item skipped because queue generation changed", StringComparison.Ordinal)
                && message.Contains("ItemType: Progress", StringComparison.Ordinal)
                && message.Contains("WasStopConfirmation: False", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StreamingFinalAnswer_StartsNextTtsBeforeFirstSegmentPlaybackCompletes()
    {
        var tts = new RecordingStreamingVoiceSynthesisService();
        var wave = new RecordingWavePlayer();
        var sink = new RecordingUiStateSink();
        var playback = CreatePlayback(tts, wave, broadcaster: sink.Broadcaster);
        var session = await playback.BeginStreamingFinalAnswerAsync(
            "turn-1",
            "turn-1",
            (_, _) => Task.CompletedTask,
            "question");

        await session.EnqueueTextSegmentAsync(StreamSegment(0, "First streamed sentence."));
        await session.EnqueueTextSegmentAsync(StreamSegment(1, "Second streamed sentence."));
        await session.CompleteInputAsync();

        Assert.True(await wave.WaitForPlayAsync(TimeSpan.FromSeconds(2)));
        Assert.True(await tts.WaitForSynthesisStartCountAsync(2, TimeSpan.FromSeconds(2)));
        Assert.Equal(0, wave.StopCount);
        Assert.True(await sink.WaitUntilAsync(uiState => uiState.Reason == "streaming_final_answer_playback_started"));
        Assert.DoesNotContain(sink.Events, uiState => uiState.Reason == "final_answer_completed");

        await session.CancelAsync("test_cleanup");
    }

    [Fact]
    public async Task StopConfirmationSpeechOutputPort_LogsEnqueueRequestAndUsesStopConfirmationItemType()
    {
        var playback = new RecordingAssistantSpeechPlaybackService();
        var logger = new RecordingLogger<AssistantSpeechInterruptionSpeechOutputPort>();
        var output = new AssistantSpeechInterruptionSpeechOutputPort(playback, logger);

        await output.SpeakInterruptionContentAsync(
            "turn-1",
            "correlation-1",
            "Got it, I'll stop.",
            "stop_confirmation");

        Assert.Equal(SpeechPlaybackItemType.StopConfirmation, playback.LastItemType);
        Assert.Equal("correlation-1", playback.LastCorrelationId);
        Assert.Contains(logger.Messages, message => message.Contains("stop_confirmation_speech_enqueue_requested", StringComparison.Ordinal));
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
        ILogger<AssistantSpeechPlaybackService>? logger = null,
        Func<IWavePlayer>? wavePlayerFactory = null,
        AssistantUiStateBroadcaster? broadcaster = null,
        IGpuWorkScheduler? gpuWorkScheduler = null,
        ITtsTextSanitizer? ttsTextSanitizer = null) =>
        CreatePlaybackCore(
            tts,
            wavePlayer,
            tracker,
            options,
            logger,
            wavePlayerFactory,
            broadcaster,
            gpuWorkScheduler,
            ttsTextSanitizer);

    private static AssistantSpeechPlaybackService CreatePlaybackCore(
        IVoiceSynthesisService tts,
        RecordingWavePlayer? wavePlayer,
        ILiveSpokenAnswerTrackingService? tracker,
        InterruptionHandlingOptions? options,
        ILogger<AssistantSpeechPlaybackService>? logger,
        Func<IWavePlayer>? wavePlayerFactory,
        AssistantUiStateBroadcaster? broadcaster,
        IGpuWorkScheduler? gpuWorkScheduler,
        ITtsTextSanitizer? ttsTextSanitizer) =>
        new(
            tts,
            new RecordingPlaybackReferenceTap(),
            new NoOpSpeakerDuckingService(),
            logger ?? NullLogger<AssistantSpeechPlaybackService>.Instance,
            tracker,
            Options.Create(options ?? new InterruptionHandlingOptions()),
            wavePlayerFactory ?? (() => wavePlayer ?? new RecordingWavePlayer()),
            broadcaster,
            gpuWorkScheduler,
            ttsTextSanitizer);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

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

    private sealed class RecordingTextVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? LastText { get; private set; }

        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            LastText = text;
            try
            {
                await onMetadataAsync(CreateMetadata(), cancellationToken);
                await onAudioAsync(CreateAudio(milliseconds: 100), cancellationToken);
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

    private sealed class RecordingStreamingVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource _secondSynthesisStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;

        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            var started = Interlocked.Increment(ref _started);
            if (started >= 2)
            {
                _secondSynthesisStarted.TrySetResult();
            }

            await onMetadataAsync(CreateMetadata(), cancellationToken);
            await onAudioAsync(CreateAudio(milliseconds: 300), cancellationToken);
        }

        public async Task<bool> WaitForSynthesisStartCountAsync(int count, TimeSpan timeout)
        {
            if (Volatile.Read(ref _started) >= count)
            {
                return true;
            }

            var completed = await Task.WhenAny(_secondSynthesisStarted.Task, Task.Delay(timeout));
            return completed == _secondSynthesisStarted.Task && Volatile.Read(ref _started) >= count;
        }
    }

    private sealed class RecordingGpuWorkScheduler : IGpuWorkScheduler
    {
        public bool HasPendingInterruptionSttValue { get; set; }

        public bool HasPendingInterruptionStt => HasPendingInterruptionSttValue;

        public Task<T> RunAsync<T>(
            string jobName,
            GpuWorkPriority priority,
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken) =>
            action(cancellationToken);

        public Task RunAsync(
            string jobName,
            GpuWorkPriority priority,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken) =>
            action(cancellationToken);
    }

    private static VoiceSynthesisStreamMetadata CreateMetadata() => new()
    {
        SampleRate = 48000,
        Channels = 1,
        Format = "s16le"
    };

    private static StreamingFinalAnswerTextSegment StreamSegment(int index, string text) => new()
    {
        TurnId = "turn-1",
        CorrelationId = "turn-1",
        GenerationId = 1,
        SegmentIndex = index,
        Text = text,
        EmittedAtUtc = DateTimeOffset.UtcNow,
        BoundaryKind = "test"
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

    private sealed class RecordingAssistantSpeechPlaybackService : IAssistantSpeechPlaybackService
    {
        public string? LastText { get; private set; }

        public string? LastCorrelationId { get; private set; }

        public SpeechPlaybackItemType? LastItemType { get; private set; }

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
            LastText = text;
            LastCorrelationId = correlationId;
            LastItemType = itemType;
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingUiStateSink
    {
        private readonly object _sync = new();
        private readonly List<AssistantUiStateEvent> _events = [];

        public RecordingUiStateSink()
        {
            Broadcaster = new AssistantUiStateBroadcaster(NullLogger<AssistantUiStateBroadcaster>.Instance);
            Broadcaster.StateChanged += (uiState, _, _) =>
            {
                lock (_sync)
                {
                    _events.Add(uiState);
                }

                return Task.CompletedTask;
            };
        }

        public AssistantUiStateBroadcaster Broadcaster { get; }

        public IReadOnlyList<AssistantUiStateEvent> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToArray();
                }
            }
        }

        public async Task<bool> WaitUntilAsync(
            Func<AssistantUiStateEvent, bool> predicate,
            int expectedCount = 1)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
            while (DateTimeOffset.UtcNow < deadline)
            {
                lock (_sync)
                {
                    if (_events.Count(predicate) >= expectedCount)
                    {
                        return true;
                    }
                }

                await Task.Delay(10);
            }

            return false;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_sync)
                {
                    return _messages.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_sync)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }

    private sealed class TwoChunkVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource<int> _releaseSecondChunk = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            return StreamSynthesizeChunksAsync(
                text,
                onMetadataAsync,
                (chunk, token) => onAudioAsync(chunk.Audio, token),
                cancellationToken);
        }

        public async Task StreamSynthesizeChunksAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<VoiceSynthesisAudioChunk, CancellationToken, Task> onAudioChunkAsync,
            CancellationToken cancellationToken)
        {
            try
            {
                await onMetadataAsync(CreateMetadata(), cancellationToken);
                await onAudioChunkAsync(
                    new VoiceSynthesisAudioChunk
                    {
                        Audio = CreateAudio(milliseconds: 250),
                        ChunkIndex = 1,
                        ChunkCount = 2
                    },
                    cancellationToken);

                var secondChunkMs = await _releaseSecondChunk.Task.WaitAsync(cancellationToken);
                await onAudioChunkAsync(
                    new VoiceSynthesisAudioChunk
                    {
                        Audio = CreateAudio(secondChunkMs),
                        ChunkIndex = 2,
                        ChunkCount = 2
                    },
                    cancellationToken);
            }
            finally
            {
                _completed.TrySetResult();
            }
        }

        public void ReleaseSecondChunk(int milliseconds) => _releaseSecondChunk.TrySetResult(milliseconds);

        public async Task<bool> WaitUntilCompletedAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_completed.Task, Task.Delay(timeout));
            return completed == _completed.Task;
        }
    }

    private sealed class DelayedMetadataAudioVoiceSynthesisService : IVoiceSynthesisService
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<int> _releaseAudio = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task StreamSynthesizeAsync(
            string text,
            Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
            Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
            CancellationToken cancellationToken)
        {
            await onMetadataAsync(CreateMetadata(), cancellationToken);
            _entered.TrySetResult();
            var milliseconds = await _releaseAudio.Task.WaitAsync(cancellationToken);
            await onAudioAsync(CreateAudio(milliseconds), cancellationToken);
        }

        public async Task<bool> WaitUntilEnteredAsync(TimeSpan timeout)
        {
            var completed = await Task.WhenAny(_entered.Task, Task.Delay(timeout));
            return completed == _entered.Task;
        }

        public void ReleaseAudio(int milliseconds) => _releaseAudio.TrySetResult(milliseconds);
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
