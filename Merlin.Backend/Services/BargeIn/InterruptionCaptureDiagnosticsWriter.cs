using System.Text.Json;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.BargeIn;

public sealed class InterruptionCaptureDiagnosticsWriter : IInterruptionCaptureDiagnosticsWriter
{
    private static long _fileSequence;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<InterruptionCaptureDiagnosticsWriter> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptionsMonitor<BargeInOptions> _options;

    public InterruptionCaptureDiagnosticsWriter(
        IOptionsMonitor<BargeInOptions> options,
        IHostEnvironment environment,
        ILogger<InterruptionCaptureDiagnosticsWriter> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public Task SaveAsync(
        InterruptionCaptureDiagnostic diagnostic,
        IReadOnlyList<BargeInAudioFrame> frames,
        IReadOnlyList<InterruptionCaptureFrameDiagnostic> frameDiagnostics,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.SaveDebugAudio || frames.Count == 0)
        {
            return Task.CompletedTask;
        }

        try
        {
            var directory = ResolvePath(options.DebugAudioPath, _environment.ContentRootPath);
            Directory.CreateDirectory(directory);

            var baseName = CreateBaseName(diagnostic);
            var wavPath = Path.Combine(directory, $"{baseName}_stt_input.wav");
            var jsonPath = Path.Combine(directory, $"{baseName}.json");
            var framesJsonlPath = Path.Combine(directory, $"{baseName}.frames.jsonl");

            var sampleRate = frames[0].SampleRate;
            var samples = FlattenFrames(frames, sampleRate, options);
            using (var stream = File.Create(wavPath))
            {
                WriteFloatMonoWav(stream, samples, sampleRate);
            }

            var metadata = diagnostic with
            {
                WavPath = wavPath,
                JsonPath = jsonPath,
                FramesJsonlPath = framesJsonlPath,
                SampleRate = sampleRate,
                SampleCount = samples.Length,
                AudioMs = sampleRate <= 0
                    ? 0
                    : (int)Math.Round(samples.Length * 1000.0 / sampleRate)
            };

            var json = JsonSerializer.Serialize(metadata, JsonOptions);
            File.WriteAllText(jsonPath, json);
            WriteFrameDiagnostics(framesJsonlPath, frameDiagnostics);

            _logger.LogInformation(
                "Interruption capture diagnostics saved. WavPath: {WavPath}. JsonPath: {JsonPath}. FramesJsonlPath: {FramesJsonlPath}. CaptureKind: {CaptureKind}. SttTranscript: {SttTranscript}.",
                wavPath,
                jsonPath,
                framesJsonlPath,
                diagnostic.CaptureKind,
                diagnostic.SttTranscript);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to save interruption capture diagnostics.");
        }

        return Task.CompletedTask;
    }

    private static void WriteFrameDiagnostics(
        string framesJsonlPath,
        IReadOnlyList<InterruptionCaptureFrameDiagnostic> frameDiagnostics)
    {
        using var writer = new StreamWriter(framesJsonlPath, append: false, System.Text.Encoding.UTF8);
        foreach (var frame in frameDiagnostics)
        {
            writer.WriteLine(JsonSerializer.Serialize(frame));
        }
    }

    private static string ResolvePath(string configuredPath, string contentRootPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? "Logs/InterruptionCaptures"
            : configuredPath.Trim();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(appData))
        {
            path = path.Replace("%APPDATA%", appData, StringComparison.OrdinalIgnoreCase);
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(contentRootPath, path));
    }

    private static string CreateBaseName(InterruptionCaptureDiagnostic diagnostic)
    {
        var timestamp = diagnostic.TimestampUtc.ToString("yyyyMMdd_HHmmss_fff");
        var correlation = SanitizeFilePart(diagnostic.CorrelationId ?? diagnostic.AssistantTurnId);
        var sequence = Interlocked.Increment(ref _fileSequence);
        return $"{timestamp}_{sequence:000000}_{correlation}_{SanitizeFilePart(diagnostic.CaptureKind)}";
    }

    private static string SanitizeFilePart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(character => invalid.Contains(character) ? '_' : character).ToArray();
        return new string(chars).Trim('_');
    }

    private static float[] FlattenFrames(
        IReadOnlyList<BargeInAudioFrame> frames,
        int sampleRate,
        BargeInOptions options)
    {
        var maxSamples = Math.Max(1, sampleRate * Math.Max(1, options.GatedSttMaxAudioMs) / 1000);
        return frames
            .Where(frame => frame.SampleRate == sampleRate)
            .SelectMany(frame => frame.Samples.ToArray())
            .Take(maxSamples)
            .ToArray();
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
