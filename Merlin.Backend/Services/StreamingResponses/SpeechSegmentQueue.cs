using System.Threading.Channels;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.StreamingResponses;

public interface ISpeechSegmentQueue
{
    Task<SpeechSegmentJob> EnqueueAsync(
        string text,
        SpeakableTextSegment sourceSegment,
        SpeechSegmentQueueContext context,
        CancellationToken cancellationToken);

    Task CompleteAsync(CancellationToken cancellationToken);

    Task CancelAsync(string reason, CancellationToken cancellationToken = default);

    IReadOnlyList<SpeechSegmentJob> Snapshot();
}

public sealed record SpeechSegmentQueueContext(
    string? CorrelationId,
    Func<AssistantVisualEvent, CancellationToken, Task> SendEventAsync,
    string? OriginalUserQuestion = null);

public sealed class SpeechSegmentQueue : ISpeechSegmentQueue
{
    private readonly IAssistantSpeechPlaybackService _playbackService;
    private readonly StreamingResponseOptions _options;
    private readonly ILogger<SpeechSegmentQueue> _logger;
    private readonly IStreamedTextDetokenizer _detokenizer;
    private readonly Channel<SpeechSegmentJob> _channel;
    private readonly CancellationTokenSource _queueCancellation = new();
    private readonly object _sync = new();
    private readonly List<SpeechSegmentJob> _jobs = [];
    private SpeechSegmentQueueContext? _context;
    private Task? _worker;
    private IStreamingFinalAnswerPlaybackSession? _playbackSession;

    public SpeechSegmentQueue(
        IAssistantSpeechPlaybackService playbackService,
        IOptions<StreamingResponseOptions> options,
        ILogger<SpeechSegmentQueue> logger,
        IStreamedTextDetokenizer? detokenizer = null)
    {
        _playbackService = playbackService;
        _options = options.Value;
        _logger = logger;
        _detokenizer = detokenizer ?? new StreamedTextDetokenizer();
        _channel = Channel.CreateBounded<SpeechSegmentJob>(new BoundedChannelOptions(Math.Max(1, _options.MaxPendingTtsSegments))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public async Task<SpeechSegmentJob> EnqueueAsync(
        string text,
        SpeakableTextSegment sourceSegment,
        SpeechSegmentQueueContext context,
        CancellationToken cancellationToken)
    {
        _context ??= context;
        EnsureWorker(context);
        var detokenized = _detokenizer.Detokenize(text);

        var job = new SpeechSegmentJob
        {
            Id = Guid.NewGuid(),
            SequenceNumber = sourceSegment.SequenceNumber,
            Text = detokenized.Text,
            State = SpeechSegmentState.Generated
        };

        lock (_sync)
        {
            _jobs.Add(job);
            job.State = SpeechSegmentState.QueuedForTts;
        }

        await _channel.Writer.WriteAsync(job, cancellationToken);
        return job;
    }

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        if (_worker is not null)
        {
            await _worker.WaitAsync(cancellationToken);
        }

        if (_playbackSession is not null)
        {
            await _playbackSession.CompleteInputAsync(cancellationToken);
        }
    }

    public async Task CancelAsync(string reason, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming speech segment queue cancelled. Reason: {Reason}", reason);
        await _queueCancellation.CancelAsync();
        _channel.Writer.TryComplete();
        lock (_sync)
        {
            foreach (var job in _jobs.Where(job => job.State is not SpeechSegmentState.Spoken and not SpeechSegmentState.Failed))
            {
                job.State = SpeechSegmentState.Cancelled;
            }
        }

        if (_context?.CorrelationId is { Length: > 0 } correlationId)
        {
            if (_playbackSession is not null)
            {
                await _playbackSession.CancelAsync(reason, cancellationToken);
            }

            await _playbackService.FlushFinalAnswerSpeechForTurnAsync(correlationId, reason, cancellationToken);
        }
        else
        {
            await _playbackService.ClearQueueAsync(cancellationToken);
        }
    }

    public IReadOnlyList<SpeechSegmentJob> Snapshot()
    {
        lock (_sync)
        {
            return _jobs
                .Select(job => new SpeechSegmentJob
                {
                    Id = job.Id,
                    SequenceNumber = job.SequenceNumber,
                    Text = job.Text,
                    State = job.State,
                    CreatedAt = job.CreatedAt,
                    PlaybackStartedAt = job.PlaybackStartedAt,
                    PlaybackCompletedAt = job.PlaybackCompletedAt
                })
                .ToArray();
        }
    }

    private void EnsureWorker(SpeechSegmentQueueContext context)
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = Task.Run(
            () => RunAsync(context, _queueCancellation.Token),
            CancellationToken.None);
    }

    private async Task RunAsync(SpeechSegmentQueueContext context, CancellationToken cancellationToken)
    {
        await foreach (var job in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                lock (_sync)
                {
                    job.State = SpeechSegmentState.SubmittedToPlayback;
                    job.PlaybackStartedAt = DateTimeOffset.UtcNow;
                }

                var session = await GetOrBeginPlaybackSessionAsync(context, cancellationToken);
                await session.EnqueueTextSegmentAsync(
                    new StreamingFinalAnswerTextSegment
                    {
                        TurnId = session.TurnId,
                        CorrelationId = session.CorrelationId,
                        GenerationId = session.GenerationId,
                        SegmentIndex = job.SequenceNumber,
                        Text = job.Text,
                        EmittedAtUtc = job.CreatedAt,
                        BoundaryKind = "streaming_response_segment"
                    },
                    cancellationToken);

                lock (_sync)
                {
                    if (job.State is not SpeechSegmentState.Cancelled)
                    {
                        job.State = SpeechSegmentState.AcceptedByPlaybackSession;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_sync)
                {
                    job.State = SpeechSegmentState.Cancelled;
                }
                throw;
            }
            catch (Exception exception)
            {
                lock (_sync)
                {
                    job.State = SpeechSegmentState.Failed;
                }

                _logger.LogWarning(exception, "Streaming speech segment failed. SequenceNumber: {SequenceNumber}", job.SequenceNumber);
            }
        }
    }

    private async Task<IStreamingFinalAnswerPlaybackSession> GetOrBeginPlaybackSessionAsync(
        SpeechSegmentQueueContext context,
        CancellationToken cancellationToken)
    {
        if (_playbackSession is not null)
        {
            return _playbackSession;
        }

        var turnId = string.IsNullOrWhiteSpace(context.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : context.CorrelationId.Trim();
        _playbackSession = await _playbackService.BeginStreamingFinalAnswerAsync(
            turnId,
            context.CorrelationId,
            context.SendEventAsync,
            context.OriginalUserQuestion,
            cancellationToken);
        return _playbackSession;
    }
}
