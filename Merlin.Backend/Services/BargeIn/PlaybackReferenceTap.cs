using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class PlaybackReferenceTap : IPlaybackReferenceTap, IAssistantPlaybackMonitor
{
    private readonly IBargeInDiagnosticsLogger _diagnostics;
    private readonly IOptionsMonitor<BargeInOptions> _options;
    private readonly object _syncRoot = new();
    private float[] _latestSamples = new float[48000 * 2];
    private int _writeIndex;
    private int _availableSamples;
    private bool _isPlaybackActive;
    private DateTimeOffset? _playbackStartedAt;
    private double _currentPlaybackEnergy;
    private double _recentPlaybackEnergy;
    private long _consumedSamplesTotal;
    private int _lastOutputReadSamples;
    private double _lastOutputReadDurationMs;
    private DateTimeOffset? _lastOutputReadAtUtc;

    public PlaybackReferenceTap(IBargeInDiagnosticsLogger diagnostics, IOptionsMonitor<BargeInOptions> options)
    {
        _diagnostics = diagnostics;
        _options = options;
    }

    public event EventHandler<BargeInSpeechContext>? SpeechStarted;

    public event EventHandler<BargeInSpeechContext>? SpeechStopped;

    public bool IsPlaybackActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _isPlaybackActive;
            }
        }
    }

    public DateTimeOffset? PlaybackStartedAt
    {
        get
        {
            lock (_syncRoot)
            {
                return _playbackStartedAt;
            }
        }
    }

    public double CurrentPlaybackEnergy
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentPlaybackEnergy;
            }
        }
    }

    public double RecentPlaybackEnergy
    {
        get
        {
            lock (_syncRoot)
            {
                return _recentPlaybackEnergy;
            }
        }
    }

    public void NotifySpeechStarted(BargeInSpeechContext context)
    {
        lock (_syncRoot)
        {
            _isPlaybackActive = true;
            _playbackStartedAt = DateTimeOffset.UtcNow;
        }

        SpeechStarted?.Invoke(this, context);
    }

    public void NotifySpeechStopped(BargeInSpeechContext context)
    {
        lock (_syncRoot)
        {
            _isPlaybackActive = false;
            _playbackStartedAt = null;
            _currentPlaybackEnergy = 0.0;
            _recentPlaybackEnergy = 0.0;
        }

        SpeechStopped?.Invoke(this, context);
    }

    public void PushPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId)
    {
        PushConsumedPcm16Reference(pcm, sampleRate, channels, correlationId);
    }

    public void PushConsumedPcm16Reference(ReadOnlyMemory<byte> pcm, int sampleRate, int channels, string? correlationId)
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
        var energy = AudioEnergyCalculator.CalculateRms(converted);
        var now = DateTimeOffset.UtcNow;
        lock (_syncRoot)
        {
            EnsureCapacity(Math.Max(_latestSamples.Length, targetSampleRate * 2));
            foreach (var sample in converted)
            {
                _latestSamples[_writeIndex] = sample;
                _writeIndex = (_writeIndex + 1) % _latestSamples.Length;
                _availableSamples = Math.Min(_availableSamples + 1, _latestSamples.Length);
            }

            _currentPlaybackEnergy = energy;
            _recentPlaybackEnergy = _recentPlaybackEnergy <= 0.0
                ? energy
                : (_recentPlaybackEnergy * 0.75) + (energy * 0.25);
            _consumedSamplesTotal += converted.Length;
            _lastOutputReadSamples = converted.Length;
            _lastOutputReadDurationMs = targetSampleRate <= 0 ? 0.0 : converted.Length * 1000.0 / targetSampleRate;
            _lastOutputReadAtUtc = now;
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

    public bool TryGetReferenceWindow(int delayMs, int sampleCount, Span<float> destination)
    {
        if (delayMs < 0 || sampleCount <= 0 || destination.Length < sampleCount)
        {
            return false;
        }

        lock (_syncRoot)
        {
            var sampleRate = GetTargetSampleRate();
            var delaySamples = Math.Max(0, (int)Math.Round(sampleRate * delayMs / 1000.0));
            if (_availableSamples < sampleCount + delaySamples)
            {
                return false;
            }

            var endIndex = Mod(_writeIndex - delaySamples, _latestSamples.Length);
            var startIndex = Mod(endIndex - sampleCount, _latestSamples.Length);
            for (var index = 0; index < sampleCount; index++)
            {
                destination[index] = _latestSamples[(startIndex + index) % _latestSamples.Length];
            }

            return true;
        }
    }

    public PlaybackReferenceDebugSnapshot GetDebugSnapshot()
    {
        lock (_syncRoot)
        {
            var sampleRate = GetTargetSampleRate();
            return new PlaybackReferenceDebugSnapshot
            {
                IsPlaybackActive = _isPlaybackActive,
                SampleRate = sampleRate,
                BufferedSamples = _availableSamples,
                CapacitySamples = _latestSamples.Length,
                BufferedMilliseconds = sampleRate <= 0 ? 0.0 : _availableSamples * 1000.0 / sampleRate,
                CurrentPlaybackEnergy = _currentPlaybackEnergy,
                RecentPlaybackEnergy = _recentPlaybackEnergy,
                WritePosition = _writeIndex,
                PlaybackStartedAt = _playbackStartedAt,
                PlaybackReferenceSource = _consumedSamplesTotal > 0 ? "output_read" : "none",
                PlaybackReferenceIsConsumptionAligned = true,
                PlaybackConsumedSamplesTotal = _consumedSamplesTotal,
                ReferenceBufferedMilliseconds = sampleRate <= 0 ? 0.0 : _availableSamples * 1000.0 / sampleRate,
                ReferenceNewestAgeMilliseconds = _lastOutputReadAtUtc is null ? null : (DateTimeOffset.UtcNow - _lastOutputReadAtUtc.Value).TotalMilliseconds,
                ReferenceOldestAgeMilliseconds = _lastOutputReadAtUtc is null ? null : (DateTimeOffset.UtcNow - _lastOutputReadAtUtc.Value).TotalMilliseconds + (_availableSamples * 1000.0 / sampleRate),
                LastOutputReadSamples = _lastOutputReadSamples,
                LastOutputReadDurationMilliseconds = _lastOutputReadDurationMs,
                LastOutputReadAtUtc = _lastOutputReadAtUtc
            };
        }
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

    private static int Mod(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
