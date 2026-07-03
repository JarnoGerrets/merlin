using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.StreamingResponses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpeechSegmentQueueTests
{
    public static TheoryData<string, string> TtsArtifactCases => new()
    {
        { "it 's ready.", "it's ready." },
        { "ask ed about it.", "asked about it." },
        { "st irs slowly.", "stirs slowly." },
        { "t ending the garden.", "tending the garden." },
        { "cult ivating habits.", "cultivating habits." },
        { "stream ing text.", "streaming text." }
    };

    [Fact]
    public async Task EnqueueAsync_PreservesPlaybackOrder()
    {
        var playback = new RecordingPlaybackService();
        var queue = CreateQueue(playback);

        await queue.EnqueueAsync("First.", Segment(0), Context(), CancellationToken.None);
        await queue.EnqueueAsync("Second.", Segment(1), Context(), CancellationToken.None);
        await queue.EnqueueAsync("Third.", Segment(2), Context(), CancellationToken.None);
        await queue.CompleteAsync(CancellationToken.None);

        Assert.Single(playback.Sessions);
        Assert.Equal(["First.", "Second.", "Third."], playback.Sessions[0].Segments.Select(segment => segment.Text));
        Assert.All(queue.Snapshot(), job => Assert.Equal(SpeechSegmentState.AcceptedByPlaybackSession, job.State));
    }

    [Fact]
    public async Task CancelAsync_ClearsPendingFinalAnswerSpeech()
    {
        var playback = new RecordingPlaybackService();
        var queue = CreateQueue(playback);

        await queue.EnqueueAsync("First.", Segment(0), Context(), CancellationToken.None);
        await queue.CancelAsync("test_cancel", CancellationToken.None);

        Assert.Equal("turn-1", playback.FlushedTurnId);
        Assert.Single(playback.Sessions);
        Assert.Equal("test_cancel", playback.Sessions[0].CancelReason);
        Assert.Contains(queue.Snapshot(), job => job.State is SpeechSegmentState.Cancelled or SpeechSegmentState.AcceptedByPlaybackSession);
    }

    [Theory]
    [MemberData(nameof(TtsArtifactCases))]
    public async Task EnqueueAsync_DetokenizesTextBeforePlayback(string raw, string expected)
    {
        var playback = new RecordingPlaybackService();
        var queue = CreateQueue(playback);

        await queue.EnqueueAsync(raw, Segment(0), Context(), CancellationToken.None);
        await queue.CompleteAsync(CancellationToken.None);

        var segment = Assert.Single(playback.Sessions[0].Segments);
        Assert.Equal(expected, segment.Text);
        Assert.DoesNotContain("it 's", segment.Text);
        Assert.DoesNotContain("ask ed", segment.Text);
        Assert.DoesNotContain("st irs", segment.Text);
        Assert.DoesNotContain("t ending", segment.Text);
        Assert.DoesNotContain("cult ivating", segment.Text);
        Assert.DoesNotContain("stream ing", segment.Text);
    }

    private static SpeechSegmentQueue CreateQueue(IAssistantSpeechPlaybackService playback)
    {
        return new SpeechSegmentQueue(
            playback,
            Options.Create(new StreamingResponseOptions { MaxPendingTtsSegments = 8 }),
            NullLogger<SpeechSegmentQueue>.Instance);
    }

    private static SpeakableTextSegment Segment(int sequenceNumber)
    {
        return new SpeakableTextSegment($"Segment {sequenceNumber}.", sequenceNumber);
    }

    private static SpeechSegmentQueueContext Context()
    {
        return new SpeechSegmentQueueContext("turn-1", (_, _) => Task.CompletedTask);
    }

    private sealed class RecordingPlaybackService : IAssistantSpeechPlaybackService
    {
        public List<RecordingStreamingSession> Sessions { get; } = [];

        public string? FlushedTurnId { get; private set; }

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

        public Task StopCurrentAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ClearQueueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IStreamingFinalAnswerPlaybackSession> BeginStreamingFinalAnswerAsync(
            string turnId,
            string? correlationId,
            Func<AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? originalUserQuestion = null,
            CancellationToken cancellationToken = default)
        {
            var session = new RecordingStreamingSession(turnId, correlationId ?? turnId);
            Sessions.Add(session);
            return Task.FromResult<IStreamingFinalAnswerPlaybackSession>(session);
        }

        public Task FlushFinalAnswerSpeechForTurnAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            FlushedTurnId = turnId;
            return Task.CompletedTask;
        }

        public sealed class RecordingStreamingSession : IStreamingFinalAnswerPlaybackSession
        {
            public RecordingStreamingSession(string turnId, string correlationId)
            {
                TurnId = turnId;
                CorrelationId = correlationId;
            }

            public string SessionId { get; } = Guid.NewGuid().ToString("N");

            public string TurnId { get; }

            public string CorrelationId { get; }

            public long GenerationId { get; } = 1;

            public List<StreamingFinalAnswerTextSegment> Segments { get; } = [];

            public string? CancelReason { get; private set; }

            public Task EnqueueTextSegmentAsync(
                StreamingFinalAnswerTextSegment segment,
                CancellationToken cancellationToken = default)
            {
                Segments.Add(segment);
                return Task.CompletedTask;
            }

            public Task CompleteInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task CancelAsync(string reason, CancellationToken cancellationToken = default)
            {
                CancelReason = reason;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
