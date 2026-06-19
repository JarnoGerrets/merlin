using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public sealed class ContinuousMicAudioBuffer : IContinuousMicAudioBuffer
{
    private readonly object _syncRoot = new();
    private readonly Queue<BargeInAudioFrame> _frames = new();
    private long _nextSequenceNumber;
    private long _droppedFrames;

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

    public int BufferMilliseconds
    {
        get
        {
            lock (_syncRoot)
            {
                if (_frames.Count == 0)
                {
                    return 0;
                }

                var first = _frames.Peek();
                var last = _frames.Last();
                return (int)Math.Max(0, Math.Round((last.Timestamp - first.Timestamp).TotalMilliseconds + GetDurationMs(last)));
            }
        }
    }

    public BargeInAudioFrame Append(BargeInAudioFrame frame, BargeInOptions options)
    {
        lock (_syncRoot)
        {
            var sequenceNumber = ++_nextSequenceNumber;
            var captured = frame with
            {
                Samples = frame.Samples.ToArray(),
                SequenceNumber = sequenceNumber,
                DurationMs = frame.DurationMs > 0 ? frame.DurationMs : GetDurationMs(frame)
            };
            _frames.Enqueue(captured);
            Trim(options);
            return captured;
        }
    }

    public ContinuousMicAudioRange GetAudioRange(
        DateTimeOffset triggerTimestamp,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        int requestedPreRollMs,
        BargeInOptions options)
    {
        lock (_syncRoot)
        {
            var frames = _frames
                .Where(frame => frame.Timestamp >= startUtc && frame.Timestamp <= endUtc)
                .Select(CloneFrame)
                .ToList();
            var oldest = _frames.Count == 0 ? (DateTimeOffset?)null : _frames.Peek().Timestamp;
            var firstIncluded = frames.Count == 0 ? (DateTimeOffset?)null : frames[0].Timestamp;
            var actualAvailable = oldest is null || oldest.Value > triggerTimestamp
                ? 0
                : (int)Math.Min(
                    requestedPreRollMs,
                    Math.Max(0, Math.Round((triggerTimestamp - oldest.Value).TotalMilliseconds)));
            var actualIncluded = firstIncluded is null || firstIncluded.Value > triggerTimestamp
                ? 0
                : (int)Math.Min(
                    requestedPreRollMs,
                    Math.Max(0, Math.Round((triggerTimestamp - firstIncluded.Value).TotalMilliseconds)));

            var gapCount = 0;
            var maxGapMs = 0;
            for (var index = 1; index < frames.Count; index++)
            {
                var previous = frames[index - 1];
                var expected = GetDurationMs(previous);
                var gap = (int)Math.Round((frames[index].Timestamp - previous.Timestamp).TotalMilliseconds);
                var excess = gap - expected;
                if (excess > Math.Max(20, expected * 2))
                {
                    gapCount++;
                    maxGapMs = Math.Max(maxGapMs, gap);
                }
            }

            return new ContinuousMicAudioRange
            {
                Frames = frames,
                RequestedPreRollMs = requestedPreRollMs,
                ActualPreRollMsAvailable = actualAvailable,
                ActualPreRollMsIncluded = actualIncluded,
                PreRollFramesIncluded = frames.Count(frame => frame.Timestamp < triggerTimestamp),
                OldestBufferedFrameAgeMs = oldest is null
                    ? null
                    : (int)Math.Max(0, Math.Round((triggerTimestamp - oldest.Value).TotalMilliseconds)),
                ContinuousRecorderBufferMs = BufferMilliseconds,
                ContinuousFramesDropped = _droppedFrames,
                FrameGapCount = gapCount,
                MaxCaptureFrameGapMs = maxGapMs
            };
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _frames.Clear();
            _nextSequenceNumber = 0;
            _droppedFrames = 0;
        }
    }

    private void Trim(BargeInOptions options)
    {
        var maxBufferMs = Math.Max(1000, options.ContinuousMicBufferMs);
        while (_frames.Count > 0)
        {
            var newest = _frames.Last();
            var oldest = _frames.Peek();
            var ageMs = (newest.Timestamp - oldest.Timestamp).TotalMilliseconds;
            if (ageMs <= maxBufferMs)
            {
                return;
            }

            _frames.Dequeue();
            _droppedFrames++;
        }
    }

    private static BargeInAudioFrame CloneFrame(BargeInAudioFrame frame)
    {
        return frame with { Samples = frame.Samples.ToArray() };
    }

    private static int GetDurationMs(BargeInAudioFrame frame)
    {
        return frame.SampleRate <= 0
            ? 0
            : Math.Max(1, (int)Math.Round(frame.Samples.Length * 1000.0 / frame.SampleRate));
    }
}
