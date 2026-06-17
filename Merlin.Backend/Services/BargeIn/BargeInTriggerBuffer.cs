using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInTriggerBuffer : IBargeInTriggerBuffer
{
    private readonly object _syncRoot = new();
    private readonly Queue<BargeInAudioFrame> _frames = new();

    public void AddFrame(BargeInAudioFrame frame)
    {
        lock (_syncRoot)
        {
            _frames.Enqueue(CloneFrame(frame));
            Trim(TimeSpan.FromSeconds(5));
        }
    }

    public IReadOnlyList<BargeInAudioFrame> CaptureTriggeredWindow(BargeInAudioFrame triggerFrame, BargeInOptions options)
    {
        lock (_syncRoot)
        {
            var trigger = CloneFrame(triggerFrame);
            var earliest = trigger.Timestamp - TimeSpan.FromMilliseconds(Math.Max(0, options.TriggerPreRollMs));
            var latest = trigger.Timestamp + TimeSpan.FromMilliseconds(Math.Max(options.TriggerCaptureMs, options.TriggerMaxCaptureMs));
            var captured = _frames
                .Where(frame => frame.Timestamp >= earliest && frame.Timestamp <= latest)
                .Append(trigger)
                .OrderBy(frame => frame.Timestamp)
                .ToList();

            return captured;
        }
    }

    public void Reset()
    {
        lock (_syncRoot)
        {
            _frames.Clear();
        }
    }

    private void Trim(TimeSpan maxAge)
    {
        if (_frames.Count == 0)
        {
            return;
        }

        var cutoff = _frames.Last().Timestamp - maxAge;
        while (_frames.Count > 0 && _frames.Peek().Timestamp < cutoff)
        {
            _frames.Dequeue();
        }
    }

    private static BargeInAudioFrame CloneFrame(BargeInAudioFrame frame)
    {
        return frame with { Samples = frame.Samples.ToArray() };
    }
}
