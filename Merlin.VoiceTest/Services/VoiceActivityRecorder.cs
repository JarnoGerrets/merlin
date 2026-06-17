using Merlin.VoiceTest.Models;
using NAudio.Wave;

namespace Merlin.VoiceTest.Services;

public sealed class VoiceActivityRecorder
{
    private readonly AudioCaptureService _fixedRecorder;

    public VoiceActivityRecorder(AudioCaptureService fixedRecorder)
    {
        _fixedRecorder = fixedRecorder;
    }

    public async Task<AudioDiagnostics> RecordAsync(
        string outputPath,
        TestPhrase phrase,
        VoiceTestOptions options,
        CancellationToken cancellationToken)
    {
        if (!options.Mode.Equals("vad", StringComparison.OrdinalIgnoreCase))
        {
            var seconds = phrase.RecommendedRecordingSeconds > 0
                ? phrase.RecommendedRecordingSeconds
                : options.RecordingSeconds;
            await _fixedRecorder.RecordFixedWindowAsync(outputPath, TimeSpan.FromSeconds(seconds), options, cancellationToken);
            var fixedDiagnostics = AudioDiagnosticsService.Analyze(outputPath);
            fixedDiagnostics.VadReason = "Fixed-window mode.";
            return fixedDiagnostics;
        }

        return await RecordVadAsync(outputPath, options, cancellationToken);
    }

    private async Task<AudioDiagnostics> RecordVadAsync(
        string outputPath,
        VoiceTestOptions options,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var deviceNumber = _fixedRecorder.ResolveDeviceNumber(options.Device);
        var format = new WaveFormat(options.TargetSampleRate, 16, options.Channels);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = DateTimeOffset.UtcNow;
        var speechStarted = false;
        var speechStartMs = 0.0;
        var speechEndMs = 0.0;
        var lastSpeechMs = 0.0;
        var speechAccumulatedMs = 0.0;
        var preRollBytes = Math.Max(0, options.PreRollMs) * format.AverageBytesPerSecond / 1000;
        var preRoll = new Queue<byte[]>();
        var preRollLength = 0;
        var stopReason = "Max utterance reached.";

        using var waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = format,
            BufferMilliseconds = 50
        };
        await using var writer = new WaveFileWriter(outputPath, format);
        waveIn.DataAvailable += (_, args) =>
        {
            var elapsedMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds;
            var buffer = args.Buffer.Take(args.BytesRecorded).ToArray();
            var rms = CalculateRms16(buffer, args.BytesRecorded);
            var bufferMs = args.BytesRecorded * 1000.0 / format.AverageBytesPerSecond;

            if (!speechStarted)
            {
                preRoll.Enqueue(buffer);
                preRollLength += buffer.Length;
                while (preRollLength > preRollBytes && preRoll.TryDequeue(out var removed))
                {
                    preRollLength -= removed.Length;
                }

                if (rms >= options.VadStartRmsThreshold)
                {
                    speechStarted = true;
                    speechStartMs = Math.Max(0, elapsedMs - options.PreRollMs);
                    foreach (var chunk in preRoll)
                    {
                        writer.Write(chunk, 0, chunk.Length);
                    }

                    preRoll.Clear();
                    preRollLength = 0;
                    writer.Write(buffer, 0, args.BytesRecorded);
                    lastSpeechMs = elapsedMs;
                    speechAccumulatedMs += bufferMs;
                }
            }
            else
            {
                writer.Write(buffer, 0, args.BytesRecorded);
                if (rms >= options.VadEndRmsThreshold)
                {
                    lastSpeechMs = elapsedMs;
                    speechAccumulatedMs += bufferMs;
                }

                if (speechAccumulatedMs >= options.MinSpeechMs
                    && elapsedMs - lastSpeechMs >= options.EndSilenceMs)
                {
                    speechEndMs = lastSpeechMs;
                    stopReason = "End silence reached.";
                    waveIn.StopRecording();
                }
            }

            if (elapsedMs >= options.MaxUtteranceMs)
            {
                if (speechStarted)
                {
                    speechEndMs = lastSpeechMs;
                }

                waveIn.StopRecording();
            }
        };
        waveIn.RecordingStopped += (_, args) =>
        {
            if (args.Exception is not null)
            {
                tcs.TrySetException(args.Exception);
            }
            else
            {
                tcs.TrySetResult();
            }
        };

        waveIn.StartRecording();
        await tcs.Task.WaitAsync(cancellationToken);

        var diagnostics = AudioDiagnosticsService.Analyze(outputPath);
        diagnostics.VadTriggered = speechStarted;
        diagnostics.VadReason = speechStarted ? stopReason : "No speech crossed threshold.";
        diagnostics.VadSpeechStartMs = speechStarted ? speechStartMs : null;
        diagnostics.VadSpeechEndMs = speechEndMs > 0 ? speechEndMs : null;
        diagnostics.EndSilenceMs = options.EndSilenceMs;
        return diagnostics;
    }

    private static double CalculateRms16(byte[] buffer, int bytesRecorded)
    {
        if (bytesRecorded < 2)
        {
            return 0;
        }

        double sumSquares = 0;
        var samples = bytesRecorded / 2;
        for (var i = 0; i < samples; i++)
        {
            var sample = BitConverter.ToInt16(buffer, i * 2) / 32768.0;
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / samples);
    }
}
