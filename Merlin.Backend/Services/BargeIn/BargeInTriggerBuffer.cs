using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInTriggerBuffer : IBargeInTriggerBuffer
{
    private readonly object _syncRoot = new();
    private readonly Queue<BargeInAudioFrame> _frames = new();
    private string _lastResetReason = "created";
    private string? _bufferOwnerAssistantTurnId;

    public void AddFrame(BargeInAudioFrame frame)
    {
        lock (_syncRoot)
        {
            _frames.Enqueue(CloneFrame(frame));
            Trim(TimeSpan.FromSeconds(30));
        }
    }

    public IReadOnlyList<BargeInAudioFrame> CaptureTriggeredWindow(BargeInAudioFrame triggerFrame, BargeInOptions options)
    {
        var maxCaptureMs = BargeInCaptureTiming.GetMaxCaptureMs(options);
        return CaptureTriggeredWindow(
            triggerFrame,
            options,
            triggerFrame.Timestamp + TimeSpan.FromMilliseconds(maxCaptureMs));
    }

    public IReadOnlyList<BargeInAudioFrame> CaptureTriggeredWindow(
        BargeInAudioFrame triggerFrame,
        BargeInOptions options,
        DateTimeOffset capturedUntil)
    {
        return CaptureTriggeredWindowWithDiagnostics(triggerFrame, options, capturedUntil, null).Frames;
    }

    public BargeInTriggeredCapture CaptureTriggeredWindowWithDiagnostics(
        BargeInAudioFrame triggerFrame,
        BargeInOptions options,
        DateTimeOffset capturedUntil,
        string? currentAssistantTurnId)
    {
        lock (_syncRoot)
        {
            var trigger = CloneFrame(triggerFrame);
            var requestedPreRollMs = Math.Max(0, options.TriggerPreRollMs);
            var earliest = trigger.Timestamp - TimeSpan.FromMilliseconds(requestedPreRollMs);
            var maxLatest = trigger.Timestamp + TimeSpan.FromMilliseconds(BargeInCaptureTiming.GetMaxCaptureMs(options));
            var latest = capturedUntil < maxLatest
                ? capturedUntil
                : maxLatest;
            var oldest = _frames.Count == 0 ? (DateTimeOffset?)null : _frames.Peek().Timestamp;
            var captured = _frames
                .Where(frame => frame.Timestamp >= earliest && frame.Timestamp <= latest)
                .OrderBy(frame => frame.Timestamp)
                .ToList();
            if (!captured.Any(frame => frame.Timestamp == trigger.Timestamp))
            {
                captured.Add(trigger);
                captured = captured.OrderBy(frame => frame.Timestamp).ToList();
            }

            var firstCaptured = captured.Count == 0 ? (DateTimeOffset?)null : captured[0].Timestamp;
            var actualAvailable = oldest is null || oldest.Value > trigger.Timestamp
                ? 0
                : (int)Math.Min(
                    requestedPreRollMs,
                    Math.Max(0, Math.Round((trigger.Timestamp - oldest.Value).TotalMilliseconds)));
            var actualIncluded = firstCaptured is null || firstCaptured.Value > trigger.Timestamp
                ? 0
                : (int)Math.Min(
                    requestedPreRollMs,
                    Math.Max(0, Math.Round((trigger.Timestamp - firstCaptured.Value).TotalMilliseconds)));

            return new BargeInTriggeredCapture
            {
                Frames = captured,
                RequestedPreRollMs = requestedPreRollMs,
                ActualPreRollMsAvailable = actualAvailable,
                ActualPreRollMsIncluded = actualIncluded,
                PreRollFramesIncluded = captured.Count(frame => frame.Timestamp < trigger.Timestamp),
                OldestBufferedFrameAgeMs = oldest is null
                    ? null
                    : (int)Math.Max(0, Math.Round((trigger.Timestamp - oldest.Value).TotalMilliseconds)),
                BufferResetReason = _lastResetReason,
                BufferOwnerAssistantTurnId = _bufferOwnerAssistantTurnId,
                CurrentAssistantTurnId = currentAssistantTurnId
            };
        }
    }

    public void Reset(string reason = "manual", string? assistantTurnId = null)
    {
        lock (_syncRoot)
        {
            _frames.Clear();
            _lastResetReason = reason;
            _bufferOwnerAssistantTurnId = assistantTurnId;
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
