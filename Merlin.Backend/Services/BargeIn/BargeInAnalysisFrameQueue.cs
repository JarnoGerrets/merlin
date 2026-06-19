namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInAnalysisFrameQueue
{
    private readonly object _syncRoot = new();
    private readonly Queue<BargeInAudioFrame> _frames = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly int _capacity;
    private long _droppedFrames;

    public BargeInAnalysisFrameQueue(int capacity)
    {
        _capacity = Math.Max(1, capacity);
    }

    public long DroppedFrames
    {
        get
        {
            lock (_syncRoot)
            {
                return _droppedFrames;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _frames.Count;
            }
        }
    }

    public BargeInAudioFrame Enqueue(BargeInAudioFrame frame)
    {
        lock (_syncRoot)
        {
            if (_frames.Count >= _capacity)
            {
                _frames.Dequeue();
                _droppedFrames++;
            }

            var queued = frame with
            {
                AnalysisQueueDepth = _frames.Count + 1,
                AnalysisFramesDropped = _droppedFrames
            };
            _frames.Enqueue(queued);
            _available.Release();
            return queued;
        }
    }

    public async Task<BargeInAudioFrame?> DequeueAsync(CancellationToken cancellationToken)
    {
        await _available.WaitAsync(cancellationToken);
        lock (_syncRoot)
        {
            return _frames.Count == 0 ? null : _frames.Dequeue();
        }
    }
}
