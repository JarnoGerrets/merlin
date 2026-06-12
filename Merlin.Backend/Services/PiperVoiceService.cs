using System.Diagnostics;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class PiperVoiceService : IVoiceSynthesisService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PiperVoiceService> _logger;
    private readonly PiperOptions _options;

    public PiperVoiceService(
        IOptions<PiperOptions> options,
        IWebHostEnvironment environment,
        ILogger<PiperVoiceService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task StreamSynthesizeAsync(
        string text,
        Func<VoiceSynthesisStreamMetadata, CancellationToken, Task> onMetadataAsync,
        Func<ReadOnlyMemory<byte>, CancellationToken, Task> onAudioAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var executablePath = ResolvePath(_options.ExecutablePath);
        var modelPath = ResolvePath(_options.ModelPath);
        var configPath = ResolvePath(_options.ConfigPath);
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("Piper executable was not found.", executablePath);
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Piper model was not found.", modelPath);
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Piper model config was not found.", configPath);
        }

        var stopwatch = Stopwatch.StartNew();
        var firstPcm = true;
        var totalBytes = 0;

        _logger.LogInformation(
            "Voice timing: Piper TTS request start. Chars: {Chars}. VoiceModel: {Model}.",
            text.Length,
            Path.GetFileName(modelPath));

        await onMetadataAsync(
            new VoiceSynthesisStreamMetadata
            {
                SampleRate = _options.SampleRate,
                Channels = _options.Channels,
                Format = _options.Format
            },
            cancellationToken);

        _logger.LogInformation(
            "Voice timing: Piper TTS metadata ready. SampleRate: {SampleRate}. Channels: {Channels}. Format: {Format}. ElapsedMs: {ElapsedMs}.",
            _options.SampleRate,
            _options.Channels,
            _options.Format,
            stopwatch.Elapsed.TotalMilliseconds);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? _environment.ContentRootPath
        };
        startInfo.ArgumentList.Add("--model");
        startInfo.ArgumentList.Add(modelPath);
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(configPath);
        startInfo.ArgumentList.Add("--output_raw");
        startInfo.ArgumentList.Add("--sentence_silence");
        startInfo.ArgumentList.Add(_options.SentenceSilenceSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--quiet");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Piper process.");

        await process.StandardInput.WriteLineAsync(text.AsMemory(), cancellationToken);
        process.StandardInput.Close();

        var buffer = new byte[8192];
        while (true)
        {
            var read = await process.StandardOutput.BaseStream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                break;
            }

            if (firstPcm)
            {
                firstPcm = false;
                _logger.LogInformation(
                    "Voice timing: Piper TTS first PCM chunk generated. Bytes: {Bytes}. ElapsedMs: {ElapsedMs}.",
                    read,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            totalBytes += read;
            var writeStopwatch = Stopwatch.StartNew();
            await onAudioAsync(buffer.AsMemory(0, read), cancellationToken);
            writeStopwatch.Stop();

            if (totalBytes == read)
            {
                _logger.LogInformation(
                    "Voice timing: Piper TTS first PCM chunk flushed to HTTP. Bytes: {Bytes}. ElapsedMs: {ElapsedMs}. HttpWriteFlushMs: {HttpWriteFlushMs}.",
                    read,
                    stopwatch.Elapsed.TotalMilliseconds,
                    writeStopwatch.Elapsed.TotalMilliseconds);
            }
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"Piper failed with exit code {process.ExitCode}: {error}");
        }

        var silenceBytes = CreateSilenceTail();
        if (silenceBytes.Length > 0)
        {
            var writeStopwatch = Stopwatch.StartNew();
            await onAudioAsync(silenceBytes, cancellationToken);
            writeStopwatch.Stop();
            totalBytes += silenceBytes.Length;

            _logger.LogInformation(
                "Voice timing: Piper TTS end silence flushed. Bytes: {Bytes}. SilenceMs: {SilenceMs}. ElapsedMs: {ElapsedMs}. HttpWriteFlushMs: {HttpWriteFlushMs}.",
                silenceBytes.Length,
                _options.EndSilenceMs,
                stopwatch.Elapsed.TotalMilliseconds,
                writeStopwatch.Elapsed.TotalMilliseconds);
        }

        _logger.LogInformation(
            "Voice timing: Piper TTS stream complete. Chars: {Chars}. Bytes: {Bytes}. ElapsedMs: {ElapsedMs}.",
            text.Length,
            totalBytes,
            stopwatch.Elapsed.TotalMilliseconds);
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, _environment.ContentRootPath);
    }

    private byte[] CreateSilenceTail()
    {
        var silenceMs = Math.Clamp(_options.EndSilenceMs, 0, 1000);
        if (silenceMs <= 0 || _options.SampleRate <= 0 || _options.Channels <= 0)
        {
            return [];
        }

        var frameCount = _options.SampleRate * silenceMs / 1000;
        return new byte[frameCount * _options.Channels * sizeof(short)];
    }
}
