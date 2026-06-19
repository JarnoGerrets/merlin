using NAudio.Wave;

namespace Merlin.Backend.Services.BargeIn;

public sealed class PlaybackReferenceWaveProvider : IWaveProvider
{
    private readonly IWaveProvider _inner;
    private readonly IPlaybackReferenceTap _playbackReferenceTap;
    private readonly string? _correlationId;

    public PlaybackReferenceWaveProvider(
        IWaveProvider inner,
        IPlaybackReferenceTap playbackReferenceTap,
        string? correlationId)
    {
        _inner = inner;
        _playbackReferenceTap = playbackReferenceTap;
        _correlationId = correlationId;
    }

    public WaveFormat WaveFormat => _inner.WaveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _inner.Read(buffer, offset, count);
        if (bytesRead > 0)
        {
            _playbackReferenceTap.PushConsumedPcm16Reference(
                buffer.AsMemory(offset, bytesRead),
                WaveFormat.SampleRate,
                WaveFormat.Channels,
                _correlationId);
        }

        return bytesRead;
    }
}
