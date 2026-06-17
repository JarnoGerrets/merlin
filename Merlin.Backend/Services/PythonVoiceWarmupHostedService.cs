using System.Diagnostics;

namespace Merlin.Backend.Services;

public sealed class PythonVoiceWarmupHostedService : IHostedService
{
    private readonly PythonVoiceService _voiceService;
    private readonly ILogger<PythonVoiceWarmupHostedService> _logger;

    public PythonVoiceWarmupHostedService(
        PythonVoiceService voiceService,
        ILogger<PythonVoiceWarmupHostedService> logger)
    {
        _voiceService = voiceService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Python STT warmup started.");
            await using var audio = CreateSilentWav();
            var result = await _voiceService.TranscribeAsync(audio, ".wav", cancellationToken);
            _logger.LogInformation(
                "Python STT warmup complete. ElapsedMs: {ElapsedMs}. TranscriptChars: {TranscriptChars}.",
                stopwatch.Elapsed.TotalMilliseconds,
                result.Text?.Length ?? 0);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Python STT warmup failed. STT will load lazily.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static MemoryStream CreateSilentWav()
    {
        const int sampleRate = 16000;
        const short channels = 1;
        const short bitsPerSample = 16;
        const int durationMs = 250;
        var sampleCount = sampleRate * durationMs / 1000;
        var dataBytes = sampleCount * channels * bitsPerSample / 8;
        var stream = new MemoryStream(capacity: 44 + dataBytes);
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataBytes);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write(bitsPerSample);
            writer.Write("data"u8);
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
        }

        stream.Position = 0;
        return stream;
    }
}
