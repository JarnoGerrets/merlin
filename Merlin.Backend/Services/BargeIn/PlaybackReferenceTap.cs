using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class PlaybackReferenceTap : IPlaybackReferenceTap
{
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly object _syncRoot = new();
    private float[] _latestSamples = new float[48000 * 2];
    private int _writeIndex;
    private int _availableSamples;

    public PlaybackReferenceTap(IBargeInDiagnosticsLogger diagnostics, IOptionsMonitor<BargeInOptions> options)
    {
        _diagnostics = diagnostics;
        _options = options;
    }

    public event EventHandler<BargeInSpeechContext>? SpeechStarted;

    public event EventHandler<BargeInSpeechContext>? SpeechStopped;

    public void NotifySpeechStarted(BargeInSpeechContext context)
    {
        lock (_syncRoot)
        {
            Array.Clear(_latestSamples);
            _writeIndex = 0;
            _availableSamples = 0;
        }

        SpeechStarted?.Invoke(this, context);
    }

    public void NotifySpeechStopped(BargeInSpeechContext context)
    {
        SpeechStopped?.Invoke(this, context);
    }

    public void PushPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId)
    {
        if (pcm.Length < 2 || sampleRate <= 0 || channels <= 0)
        {
            return;
        }

        var mono = BargeInAudioFrameConverter.ConvertPcm16ToMonoFloat(pcm.Span, channels);
        if (mono.Length == 0)
        {
            return;
        }

        var targetSampleRate = GetTargetSampleRate();
        var converted = BargeInAudioFrameConverter.ResampleMono(mono, sampleRate, targetSampleRate);
        lock (_syncRoot)
        {
            EnsureCapacity(Math.Max(_latestSamples.Length, targetSampleRate * 2));
            foreach (var sample in converted)
            {
                _latestSamples[_writeIndex] = sample;
                _writeIndex = (_writeIndex + 1) % _latestSamples.Length;
                _availableSamples = Math.Min(_availableSamples + 1, _latestSamples.Length);
            }
        }

        _diagnostics.PlaybackReferenceFrameReceived(correlationId, converted.Length);
    }

    public ReadOnlyMemory<float> GetLatestReferenceFrame(int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return ReadOnlyMemory<float>.Empty;
        }

        var output = new float[sampleCount];
        lock (_syncRoot)
        {
            var count = Math.Min(sampleCount, _availableSamples);
            var start = (_writeIndex - count + _latestSamples.Length) % _latestSamples.Length;
            for (var index = 0; index < count; index++)
            {
                output[sampleCount - count + index] = _latestSamples[(start + index) % _latestSamples.Length];
            }
        }

        return output;
    }

    private void EnsureCapacity(int capacity)
    {
        if (_latestSamples.Length >= capacity)
        {
            return;
        }

        _latestSamples = new float[capacity];
        _writeIndex = 0;
        _availableSamples = 0;
    }

    private int GetTargetSampleRate()
    {
        var sampleRate = _options.CurrentValue.AecSampleRate;
        return sampleRate is 8000 or 16000 or 32000 or 48000 ? sampleRate : 48000;
    }
}
