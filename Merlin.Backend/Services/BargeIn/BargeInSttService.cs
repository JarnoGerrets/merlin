using Merlin.Backend.Configuration;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services.BargeIn;

public sealed class BargeInSttService : IBargeInSttService
{
    private readonly ILogger<BargeInSttService> _logger;
    private readonly IVoiceTranscriptionService _voiceTranscriptionService;

    public BargeInSttService(
        IVoiceTranscriptionService voiceTranscriptionService,
        ILogger<BargeInSttService> logger)
    {
        _voiceTranscriptionService = voiceTranscriptionService;
        _logger = logger;
    }

    public async Task<BargeInSttResult> TranscribeTriggerAsync(
        IReadOnlyList<BargeInAudioFrame> frames,
        BargeInOptions options,
        CancellationToken cancellationToken)
    {
        if (frames.Count == 0)
        {
            return new BargeInSttResult
            {
                Transcript = string.Empty,
                AudioDuration = TimeSpan.Zero
            };
        }

        var sampleRate = frames[0].SampleRate;
        var maxSamples = Math.Max(1, sampleRate * Math.Max(1, options.GatedSttMaxAudioMs) / 1000);
        var samples = frames
            .Where(frame => frame.SampleRate == sampleRate)
            .SelectMany(frame => frame.Samples.ToArray())
            .Take(maxSamples)
            .ToArray();
        var duration = TimeSpan.FromSeconds(samples.Length / (double)sampleRate);

        await using var wavStream = new MemoryStream();
        WriteFloatMonoWav(wavStream, samples, sampleRate);
        wavStream.Position = 0;

        _logger.LogInformation(
            "Gated STT started. AudioMs: {AudioMs}. Samples: {Samples}. Model: {Model}. Device: {Device}. BeamSize: {BeamSize}.",
            duration.TotalMilliseconds,
            samples.Length,
            options.GatedSttModel,
            options.GatedSttDevice,
            options.GatedSttBeamSize);

        VoiceTranscriptionResponse transcription = await _voiceTranscriptionService.TranscribeAsync(
            wavStream,
            ".wav",
            cancellationToken);

        return new BargeInSttResult
        {
            Transcript = transcription.Text?.Trim() ?? string.Empty,
            AudioDuration = duration
        };
    }

    private static void WriteFloatMonoWav(Stream stream, IReadOnlyList<float> samples, int sampleRate)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        var pcmBytes = samples.Count * sizeof(short);
        var byteRate = sampleRate * sizeof(short);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcmBytes);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcmBytes);

        foreach (var sample in samples)
        {
            var pcm = (short)Math.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
            writer.Write(pcm);
        }

        writer.Flush();
    }
}
