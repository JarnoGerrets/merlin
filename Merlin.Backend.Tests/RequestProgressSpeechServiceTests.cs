using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.Acknowledgement;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class RequestProgressSpeechServiceTests
{
    [Fact]
    public async Task Start_WhenDeepInfraDelayExceedsThreshold_SchedulesProgressUpdate()
    {
        var playback = new RecordingSpeechPlaybackService();
        var service = CreateService(playback, firstProgressAfterMs: 20);
        var handle = service.Start(Request(firstProgressAfterMs: 20), CancellationToken.None);

        await Task.Delay(60);
        await handle.StopAsync();

        Assert.Contains(playback.Requests, request =>
            request.Text.Contains("putting it together", StringComparison.OrdinalIgnoreCase)
            || request.Text.Contains("care with this one", StringComparison.OrdinalIgnoreCase)
            || request.Text.Contains("working through", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Start_WhenMainResponseReadyBeforeThreshold_DoesNotSpeakProgressUpdate()
    {
        var playback = new RecordingSpeechPlaybackService();
        var service = CreateService(playback, firstProgressAfterMs: 100);
        var handle = service.Start(Request(firstProgressAfterMs: 100), CancellationToken.None);

        await Task.Delay(20);
        handle.MarkMainResponseReady();
        await Task.Delay(130);
        await handle.StopAsync();

        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task Start_WhenFinalAnswerReady_CancelsPendingProgressUpdate()
    {
        var playback = new RecordingSpeechPlaybackService();
        var service = CreateService(playback, firstProgressAfterMs: 100);
        var handle = service.Start(Request(firstProgressAfterMs: 100), CancellationToken.None);

        handle.MarkMainResponseReady();
        await Task.Delay(130);
        await handle.StopAsync();

        Assert.Empty(playback.Requests);
    }

    [Fact]
    public async Task Start_CapsProgressUpdates()
    {
        var playback = new RecordingSpeechPlaybackService();
        var service = CreateService(playback, firstProgressAfterMs: 10, secondProgressAfterMs: 20, longWaitProgressAfterMs: 30, maxUpdates: 2);
        var handle = service.Start(Request(maxProgressUpdates: 2, firstProgressAfterMs: 10, secondProgressAfterMs: 20, longWaitProgressAfterMs: 30), CancellationToken.None);

        await Task.Delay(80);
        await handle.StopAsync();

        Assert.Equal(2, playback.Requests.Count);
    }

    [Fact]
    public async Task Start_UsesStateAwareProgressPhrase()
    {
        var playback = new RecordingSpeechPlaybackService();
        var service = CreateService(playback, firstProgressAfterMs: 10);
        var handle = service.Start(Request(progressState: RequestProgressState.WaitingOnMemory, firstProgressAfterMs: 10), CancellationToken.None);

        await Task.Delay(40);
        await handle.StopAsync();

        Assert.Contains(playback.Requests, request => request.Text.Contains("memory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Start_EnqueuesProgressAsCancelOnlyBeforePlayback()
    {
        var playback = new RecordingSpeechPlaybackService();
        var service = CreateService(playback, firstProgressAfterMs: 10);
        var handle = service.Start(Request(firstProgressAfterMs: 10), CancellationToken.None);

        await Task.Delay(40);
        await handle.StopAsync();

        Assert.NotEmpty(playback.Requests);
        Assert.All(playback.Requests, request =>
        {
            Assert.Equal(SpeechPlaybackItemType.Progress, request.ItemType);
            Assert.True(request.CancelOnlyBeforePlayback);
        });
    }

    private static RequestProgressSpeechService CreateService(
        RecordingSpeechPlaybackService playback,
        int firstProgressAfterMs,
        int secondProgressAfterMs = 1000,
        int longWaitProgressAfterMs = 2000,
        int maxUpdates = 3)
    {
        var options = Options.Create(new AcknowledgementSpeechOptions
        {
            FirstProgressAfterMs = firstProgressAfterMs,
            SecondProgressAfterMs = secondProgressAfterMs,
            LongWaitProgressAfterMs = longWaitProgressAfterMs,
            MaxProgressUpdates = maxUpdates,
            PhraseCooldownSeconds = 0
        });
        return new RequestProgressSpeechService(
            new AcknowledgementPhraseLibrary(options),
            playback,
            options,
            NullLogger<RequestProgressSpeechService>.Instance);
    }

    private static RequestProgressSpeechRequest Request(
        int maxProgressUpdates = 3,
        RequestProgressState progressState = RequestProgressState.WaitingOnDeepInfra,
        int firstProgressAfterMs = 20,
        int secondProgressAfterMs = 40,
        int longWaitProgressAfterMs = 60)
    {
        return new RequestProgressSpeechRequest
        {
            RequestId = "request-1",
            CorrelationId = "correlation-1",
            CommandReceivedAtUtc = DateTimeOffset.UtcNow,
            SendEventAsync = (_, _) => Task.CompletedTask,
            Decision = new AcknowledgementDecision
            {
                ShouldSpeakInitialAcknowledgement = true,
                PhraseId = "general_reasoning_01",
                PhraseText = "Good question, sir. Let me gather my thoughts.",
                InitialCategory = AcknowledgementCategory.GeneralReasoning,
                FirstProgressAfter = TimeSpan.FromMilliseconds(firstProgressAfterMs),
                SecondProgressAfter = TimeSpan.FromMilliseconds(secondProgressAfterMs),
                LongWaitProgressAfter = TimeSpan.FromMilliseconds(longWaitProgressAfterMs),
                MaxProgressUpdates = maxProgressUpdates,
                ProgressState = progressState,
                Reason = "test"
            }
        };
    }

    private sealed class RecordingSpeechPlaybackService : IAssistantSpeechPlaybackService
    {
        public List<SpeechRequest> Requests { get; } = [];

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
            Requests.Add(new SpeechRequest(text, correlationId, speechCacheKey, itemType, cancelOnlyBeforePlayback));
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record SpeechRequest(
        string Text,
        string? CorrelationId,
        string? CacheKey,
        SpeechPlaybackItemType ItemType,
        bool CancelOnlyBeforePlayback);
}
