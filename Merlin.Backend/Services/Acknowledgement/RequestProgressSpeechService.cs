using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Acknowledgement;

public sealed class RequestProgressSpeechService : IRequestProgressSpeechService
{
    private readonly IAcknowledgementPhraseLibrary _phraseLibrary;
    private readonly IAssistantSpeechPlaybackService _speechPlaybackService;
    private readonly ILogger<RequestProgressSpeechService> _logger;
    private readonly AcknowledgementSpeechOptions _options;

    public RequestProgressSpeechService(
        IAcknowledgementPhraseLibrary phraseLibrary,
        IAssistantSpeechPlaybackService speechPlaybackService,
        IOptions<AcknowledgementSpeechOptions> options,
        ILogger<RequestProgressSpeechService> logger)
    {
        _phraseLibrary = phraseLibrary;
        _speechPlaybackService = speechPlaybackService;
        _options = options.Value;
        _logger = logger;
    }

    public IRequestProgressSpeechHandle Start(
        RequestProgressSpeechRequest request,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || request.Decision.MaxProgressUpdates <= 0)
        {
            return NoOpRequestProgressSpeechHandle.Instance;
        }

        var handle = new RequestProgressSpeechHandle(
            request,
            _phraseLibrary,
            _speechPlaybackService,
            _logger,
            _options,
            cancellationToken);
        handle.Start();
        return handle;
    }

    private sealed class RequestProgressSpeechHandle : IRequestProgressSpeechHandle
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly RequestProgressSpeechRequest _request;
        private readonly IAcknowledgementPhraseLibrary _phraseLibrary;
        private readonly IAssistantSpeechPlaybackService _speechPlaybackService;
        private readonly ILogger _logger;
        private readonly AcknowledgementSpeechOptions _options;
        private readonly TaskCompletionSource _mainResponseReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? _monitorTask;
        private int _ready;

        public RequestProgressSpeechHandle(
            RequestProgressSpeechRequest request,
            IAcknowledgementPhraseLibrary phraseLibrary,
            IAssistantSpeechPlaybackService speechPlaybackService,
            ILogger logger,
            AcknowledgementSpeechOptions options,
            CancellationToken cancellationToken)
        {
            _request = request;
            _phraseLibrary = phraseLibrary;
            _speechPlaybackService = speechPlaybackService;
            _logger = logger;
            _options = options;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        public void Start()
        {
            _logger.LogInformation(
                "Progress monitor started. RequestId: {RequestId}. CorrelationId: {CorrelationId}. State: {State}. MaxUpdates: {MaxUpdates}.",
                _request.RequestId,
                _request.CorrelationId,
                _request.Decision.ProgressState,
                _request.Decision.MaxProgressUpdates);
            _monitorTask = Task.Run(RunAsync, CancellationToken.None);
        }

        public void MarkMainResponseReady()
        {
            if (Interlocked.Exchange(ref _ready, 1) == 1)
            {
                return;
            }

            var elapsed = DateTimeOffset.UtcNow - _request.CommandReceivedAtUtc;
            _logger.LogInformation(
                "Main response ready: cancelling pending progress updates. RequestId: {RequestId}. CorrelationId: {CorrelationId}. ElapsedMs: {ElapsedMs}.",
                _request.RequestId,
                _request.CorrelationId,
                elapsed.TotalMilliseconds);
            _mainResponseReady.TrySetResult();
            _cancellation.Cancel();
        }

        public async Task StopAsync()
        {
            MarkMainResponseReady();
            if (_monitorTask is null)
            {
                return;
            }

            try
            {
                await _monitorTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _cancellation.Dispose();
        }

        private async Task RunAsync()
        {
            var thresholds = new[]
            {
                _request.Decision.FirstProgressAfter,
                _request.Decision.SecondProgressAfter,
                _request.Decision.LongWaitProgressAfter
            };
            var maxUpdates = Math.Min(_request.Decision.MaxProgressUpdates, thresholds.Length);

            var previousThreshold = TimeSpan.Zero;
            for (var index = 0; index < maxUpdates; index++)
            {
                var threshold = thresholds[index];
                var delay = threshold > previousThreshold
                    ? threshold - previousThreshold
                    : TimeSpan.FromMilliseconds(1);
                previousThreshold = threshold;
                _logger.LogInformation(
                    "Progress update scheduled. RequestId: {RequestId}. CorrelationId: {CorrelationId}. Index: {Index}. AfterMs: {AfterMs}.",
                    _request.RequestId,
                    _request.CorrelationId,
                    index + 1,
                    threshold.TotalMilliseconds);

                var readyTask = _mainResponseReady.Task;
                var delayTask = Task.Delay(delay, _cancellation.Token);
                var completed = await Task.WhenAny(readyTask, delayTask);
                if (completed == readyTask || _cancellation.IsCancellationRequested)
                {
                    LogCancelled(index + 1);
                    return;
                }

                if (Volatile.Read(ref _ready) == 1)
                {
                    LogCancelled(index + 1);
                    return;
                }

                await SpeakProgressAsync(index + 1, _cancellation.Token);
            }
        }

        private async Task SpeakProgressAsync(int index, CancellationToken cancellationToken)
        {
            try
            {
                var cooldown = TimeSpan.FromSeconds(Math.Max(0, _options.PhraseCooldownSeconds));
                var phrase = _phraseLibrary.SelectProgress(_request.Decision.ProgressState, DateTimeOffset.UtcNow, cooldown);
                var elapsed = DateTimeOffset.UtcNow - _request.CommandReceivedAtUtc;
                _logger.LogInformation(
                    "Progress update spoken. RequestId: {RequestId}. CorrelationId: {CorrelationId}. Index: {Index}. State: {State}. PhraseId: {PhraseId}. ElapsedMs: {ElapsedMs}.",
                    _request.RequestId,
                    _request.CorrelationId,
                    index,
                    _request.Decision.ProgressState,
                    phrase.Id,
                    elapsed.TotalMilliseconds);

                await _speechPlaybackService.EnqueueAsync(
                    phrase.Text,
                    _request.CorrelationId,
                    _request.SendEventAsync,
                    phrase.Id,
                    true,
                    cancellationToken,
                    SpeechPlaybackItemType.Progress,
                    cancelOnlyBeforePlayback: true);
            }
            catch (OperationCanceledException)
            {
                LogCancelled(index, "Pending progress update cancelled before playback.");
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Progress update playback failed. RequestId: {RequestId}. CorrelationId: {CorrelationId}. Index: {Index}.",
                    _request.RequestId,
                    _request.CorrelationId,
                    index);
            }
        }

        private void LogCancelled(int index, string message = "Progress update cancelled because main response is ready.")
        {
            var elapsed = DateTimeOffset.UtcNow - _request.CommandReceivedAtUtc;
            _logger.LogInformation(
                "{Message} RequestId: {RequestId}. CorrelationId: {CorrelationId}. Index: {Index}. ElapsedMs: {ElapsedMs}.",
                message,
                _request.RequestId,
                _request.CorrelationId,
                index,
                elapsed.TotalMilliseconds);
        }
    }

    private sealed class NoOpRequestProgressSpeechHandle : IRequestProgressSpeechHandle
    {
        public static NoOpRequestProgressSpeechHandle Instance { get; } = new();

        public void MarkMainResponseReady()
        {
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
