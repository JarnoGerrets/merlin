using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class PythonVoiceService : IVoiceTranscriptionService, IDisposable
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PythonVoiceService> _logger;
    private readonly VoiceOptions _options;
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private Process? _workerProcess;
    private StreamWriter? _workerInput;
    private StreamReader? _workerOutput;

    public PythonVoiceService(
        IOptions<VoiceOptions> options,
        IWebHostEnvironment environment,
        ILogger<PythonVoiceService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<VoiceTranscriptionResponse> TranscribeAsync(
        Stream audioStream,
        string fileExtension,
        CancellationToken cancellationToken)
    {
        var extension = NormalizeAudioExtension(fileExtension);
        var inputPath = Path.Combine(Path.GetTempPath(), $"merlin-stt-{Guid.NewGuid():N}{extension}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using (var fileStream = File.Create(inputPath))
            {
                await audioStream.CopyToAsync(fileStream, cancellationToken);
            }

            var audioLength = new FileInfo(inputPath).Length;
            _logger.LogInformation(
                "Voice timing: STT upload saved. Path: {InputPath}. Bytes: {AudioBytes}. ElapsedMs: {ElapsedMs}.",
                inputPath,
                audioLength,
                stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Voice timing: STT start. Model: {Model}. Device: {Device}. ComputeType: {ComputeType}. BeamSize: {BeamSize}. VadSilenceMs: {VadSilenceMs}.",
                _options.WhisperModelSize,
                _options.WhisperDevice,
                _options.WhisperComputeType,
                _options.WhisperBeamSize,
                _options.WhisperVadMinSilenceDurationMs);

            var result = await SendWorkerCommandAsync(
                new Dictionary<string, object?>
                {
                    ["command"] = "transcribe",
                    ["input"] = inputPath,
                    ["model_size"] = _options.WhisperModelSize,
                    ["device"] = _options.WhisperDevice,
                    ["compute_type"] = _options.WhisperComputeType,
                    ["language"] = _options.WhisperLanguage,
                    ["beam_size"] = _options.WhisperBeamSize,
                    ["vad_min_silence_duration_ms"] = _options.WhisperVadMinSilenceDurationMs
                },
                cancellationToken);

            var transcription = result.Deserialize<VoiceTranscriptionResponse>(JsonSerializerOptions);
            _logger.LogInformation(
                "Voice timing: STT complete. ElapsedMs: {ElapsedMs}. TranscriptChars: {TranscriptChars}.",
                stopwatch.Elapsed.TotalMilliseconds,
                transcription?.Text?.Length ?? 0);

            return transcription ?? new VoiceTranscriptionResponse();
        }
        finally
        {
            TryDelete(inputPath);
        }
    }

    private string GetScriptPath(string scriptName)
    {
        var contentRootScript = Path.Combine(_environment.ContentRootPath, "VoiceScripts", scriptName);
        if (File.Exists(contentRootScript))
        {
            return contentRootScript;
        }

        return Path.Combine(AppContext.BaseDirectory, "VoiceScripts", scriptName);
    }

    private async Task<JsonElement> SendWorkerCommandAsync(
        IReadOnlyDictionary<string, object?> command,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(10, _options.ProcessTimeoutSeconds));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await _workerLock.WaitAsync(timeoutCts.Token);
        try
        {
            StartWorkerIfNeeded();

            var input = _workerInput ?? throw new InvalidOperationException("Python STT worker input is unavailable.");
            var output = _workerOutput ?? throw new InvalidOperationException("Python STT worker output is unavailable.");
            var line = JsonSerializer.Serialize(command, JsonSerializerOptions);
            var commandName = command.TryGetValue("command", out var configuredCommand)
                ? Convert.ToString(configuredCommand, System.Globalization.CultureInfo.InvariantCulture)
                : "unknown";
            var stopwatch = Stopwatch.StartNew();

            await input.WriteLineAsync(line.AsMemory(), timeoutCts.Token);
            await input.FlushAsync(timeoutCts.Token);

            var response = await ReadWorkerResponseAsync(output, timeoutCts.Token);
            if (response.Ok != true)
            {
                throw new InvalidOperationException($"Python STT worker failed: {response.Error}");
            }

            stopwatch.Stop();
            if (response.Payload.ValueKind == JsonValueKind.Object
                && response.Payload.TryGetProperty("elapsed_ms", out var workerElapsed)
                && workerElapsed.TryGetDouble(out var workerElapsedMs))
            {
                _logger.LogInformation(
                    "Python STT worker command completed. Command: {Command}. WorkerElapsedMs: {WorkerElapsedMs}. TotalElapsedMs: {TotalElapsedMs}.",
                    commandName,
                    workerElapsedMs,
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            return response.Payload;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            StopWorker();
            throw new TimeoutException($"Python STT worker timed out after {timeout.TotalSeconds:N0} seconds.");
        }
        catch
        {
            if (_workerProcess is not { HasExited: false })
            {
                StopWorker();
            }

            throw;
        }
        finally
        {
            _workerLock.Release();
        }
    }

    private void StartWorkerIfNeeded()
    {
        if (_workerProcess is { HasExited: false } && _workerInput is not null && _workerOutput is not null)
        {
            return;
        }

        StopWorker();
        var scriptPath = GetScriptPath("voice_worker.py");
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.PythonExecutable,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in _options.PythonArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(scriptPath);

        _workerProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Python STT process.");
        _workerInput = _workerProcess.StandardInput;
        _workerOutput = _workerProcess.StandardOutput;
        _ = Task.Run(() => LogWorkerErrorsAsync(_workerProcess.StandardError));
    }

    private async Task<WorkerResponse> ReadWorkerResponseAsync(StreamReader output, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await output.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new InvalidOperationException("Python STT worker exited unexpectedly.");
            }

            try
            {
                var response = JsonSerializer.Deserialize<WorkerResponse>(line, JsonSerializerOptions);
                if (response?.Ok is not null)
                {
                    return response;
                }
            }
            catch (JsonException)
            {
                _logger.LogDebug("Ignoring non-protocol output from Python STT worker: {Line}", line);
            }
        }
    }

    private async Task LogWorkerErrorsAsync(StreamReader error)
    {
        while (await error.ReadLineAsync() is { } line)
        {
            _logger.LogWarning("Python STT worker: {Line}", line);
        }
    }

    private static string NormalizeAudioExtension(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ".wav";
        }

        var extension = value.StartsWith('.') ? value : $".{value}";
        return extension.ToLowerInvariant() switch
        {
            ".wav" or ".mp3" or ".m4a" or ".ogg" or ".webm" or ".flac" => extension.ToLowerInvariant(),
            _ => ".wav"
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary files are best-effort cleanup.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process may have exited between the timeout and kill attempt.
        }
    }

    private void StopWorker()
    {
        var process = _workerProcess;
        _workerInput?.Dispose();
        _workerOutput?.Dispose();
        _workerInput = null;
        _workerOutput = null;
        _workerProcess = null;

        if (process is not null)
        {
            TryKill(process);
            process.Dispose();
        }
    }

    public void Dispose()
    {
        StopWorker();
        _workerLock.Dispose();
    }

    private sealed class WorkerResponse
    {
        [JsonPropertyName("ok")]
        public bool? Ok { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; set; }
    }
}
