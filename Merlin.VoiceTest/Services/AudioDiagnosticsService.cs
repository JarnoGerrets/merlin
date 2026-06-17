using Merlin.VoiceTest.Models;
using NAudio.Wave;

namespace Merlin.VoiceTest.Services;

public static class AudioDiagnosticsService
{
    public static AudioDiagnostics Analyze(string wavPath)
    {
        using var reader = new WaveFileReader(wavPath);
        var sampleProvider = reader.ToSampleProvider();
        var buffer = new float[reader.WaveFormat.SampleRate * Math.Max(1, reader.WaveFormat.Channels)];
        long sampleCount = 0;
        double sumSquares = 0;
        double peak = 0;
        var firstSpeechSample = -1L;
        var lastSpeechSample = -1L;
        var threshold = 0.012;
        int read;

        while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var value = Math.Abs(buffer[i]);
                sumSquares += value * value;
                peak = Math.Max(peak, value);
                if (value >= threshold)
                {
                    firstSpeechSample = firstSpeechSample < 0 ? sampleCount : firstSpeechSample;
                    lastSpeechSample = sampleCount;
                }

                sampleCount++;
            }
        }

        var samplesPerMs = reader.WaveFormat.SampleRate * reader.WaveFormat.Channels / 1000.0;
        var durationMs = reader.TotalTime.TotalMilliseconds;
        var rms = sampleCount == 0 ? 0 : Math.Sqrt(sumSquares / sampleCount);
        double? silenceBefore = firstSpeechSample >= 0 ? firstSpeechSample / samplesPerMs : null;
        double? silenceAfter = lastSpeechSample >= 0 ? Math.Max(0, durationMs - (lastSpeechSample / samplesPerMs)) : null;
        double? speechDuration = firstSpeechSample >= 0 && lastSpeechSample >= firstSpeechSample
            ? (lastSpeechSample - firstSpeechSample) / samplesPerMs
            : null;

        return new AudioDiagnostics
        {
            DurationMs = durationMs,
            SampleRate = reader.WaveFormat.SampleRate,
            ChannelCount = reader.WaveFormat.Channels,
            RmsLevel = rms,
            PeakLevel = peak,
            ClippingDetected = peak >= 0.98,
            SignalTooQuiet = rms < 0.01,
            SignalTooLoud = rms > 0.35 || peak > 0.94,
            PossibleClipping = peak >= 0.94,
            SilenceBeforeSpeechMs = silenceBefore,
            SilenceAfterSpeechMs = silenceAfter,
            SpeechDurationMs = speechDuration,
            AudioFilePath = wavPath,
            WavFileSize = new FileInfo(wavPath).Length
        };
    }
}
