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
    private static readonly JsonSerializerOptions DiagnosticJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PythonVoiceService> _logger;
    private readonly VoiceOptions _options;
    private readonly SemaphoreSlim _workerLock = new(1, 1);
    private readonly object _workerErrorLock = new();
    private readonly Queue<string> _workerErrorTail = new();
    private Process? _workerProcess;
    private StreamWriter? _workerInput;
    private StreamReader? _workerOutput;
    private WorkerStartSnapshot? _workerStartSnapshot;

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
        var audioLength = 0L;

        try
        {
            await using (var fileStream = File.Create(inputPath))
            {
                await audioStream.CopyToAsync(fileStream, cancellationToken);
            }

            audioLength = new FileInfo(inputPath).Length;
            _logger.LogInformation(
                "Voice timing: STT upload saved. Path: {InputPath}. Bytes: {AudioBytes}. ElapsedMs: {ElapsedMs}.",
                inputPath,
                audioLength,
                stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogInformation(
                "Voice timing: STT start. Model: {Model}. Device: {Device}. ComputeType: {ComputeType}. BeamSize: {BeamSize}. VadSilenceMs: {VadSilenceMs}. PromptChars: {PromptChars}.",
                _options.WhisperModelSize,
                _options.WhisperDevice,
                _options.WhisperComputeType,
                _options.WhisperBeamSize,
                _options.WhisperVadMinSilenceDurationMs,
                _options.WhisperInitialPrompt.Length);

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
                    ["vad_min_silence_duration_ms"] = _options.WhisperVadMinSilenceDurationMs,
                    ["initial_prompt"] = _options.WhisperInitialPrompt
                },
                cancellationToken);

            var transcription = result.Deserialize<VoiceTranscriptionResponse>(JsonSerializerOptions);
            _logger.LogInformation(
                "Voice timing: STT complete. ElapsedMs: {ElapsedMs}. TranscriptChars: {TranscriptChars}.",
                stopwatch.Elapsed.TotalMilliseconds,
                transcription?.Text?.Length ?? 0);

            return transcription ?? new VoiceTranscriptionResponse();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            WriteFailureDiagnostics(inputPath, extension, audioLength, stopwatch.Elapsed, exception);
            throw;
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
                StopWorker(clearSnapshot: false);
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
        ClearWorkerErrorTail();
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.PythonExecutable,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = _environment.ContentRootPath
        };
        startInfo.Environment["PYTHONUNBUFFERED"] = "1";

        foreach (var argument in _options.PythonArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(scriptPath);

        _logger.LogInformation(
            "Starting Python STT worker. PythonExecutable: {PythonExecutable}. Arguments: {Arguments}. ScriptPath: {ScriptPath}. WorkingDirectory: {WorkingDirectory}.",
            startInfo.FileName,
            string.Join(" ", startInfo.ArgumentList.Select(QuoteArgument)),
            scriptPath,
            startInfo.WorkingDirectory);

        _workerProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Python STT process.");
        _workerInput = _workerProcess.StandardInput;
        _workerOutput = _workerProcess.StandardOutput;
        _workerStartSnapshot = new WorkerStartSnapshot(
            _workerProcess.Id,
            startInfo.FileName,
            [.. startInfo.ArgumentList],
            scriptPath,
            startInfo.WorkingDirectory,
            DateTimeOffset.UtcNow);
        _logger.LogInformation(
            "Python STT worker started. ProcessId: {ProcessId}.",
            _workerProcess.Id);
        _ = Task.Run(() => LogWorkerErrorsAsync(_workerProcess.StandardError));
    }

    private async Task<WorkerResponse> ReadWorkerResponseAsync(StreamReader output, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await output.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw BuildWorkerExitedException();
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
        try
        {
            while (await error.ReadLineAsync() is { } line)
            {
                RememberWorkerError(line);
                _logger.LogWarning("Python STT worker: {Line}", line);
            }
        }
        catch (ObjectDisposedException)
        {
            // The worker is being stopped by the host.
        }
    }

    private InvalidOperationException BuildWorkerExitedException()
    {
        var process = _workerProcess;
        var snapshot = _workerStartSnapshot;
        var exitCode = TryGetExitCode(process);
        var stderrTail = GetWorkerErrorTail();
        var message = new StringBuilder("Python STT worker exited unexpectedly.");
        if (snapshot is not null)
        {
            message.Append($" ProcessId: {snapshot.ProcessId}.");
            message.Append($" PythonExecutable: {snapshot.PythonExecutable}.");
            message.Append($" ScriptPath: {snapshot.ScriptPath}.");
        }

        if (exitCode is not null)
        {
            message.Append($" ExitCode: {exitCode.Value}.");
        }

        if (stderrTail.Count > 0)
        {
            message.Append(" Last stderr: ");
            message.Append(string.Join(" | ", stderrTail));
        }

        return new InvalidOperationException(message.ToString());
    }

    private void WriteFailureDiagnostics(
        string inputPath,
        string extension,
        long audioLength,
        TimeSpan elapsed,
        Exception exception)
    {
        try
        {
            var root = RepositoryRootPath();
            var diagnosticsDirectory = Path.Combine(root, "STT_FAILURE_DIAGNOSTICS");
            Directory.CreateDirectory(diagnosticsDirectory);

            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var preservedAudioPath = Path.Combine(diagnosticsDirectory, $"stt-failure-{stamp}{extension}");
            if (File.Exists(inputPath))
            {
                File.Copy(inputPath, preservedAudioPath, overwrite: true);
            }

            var snapshot = CreateFailureSnapshot(inputPath, preservedAudioPath, audioLength, elapsed, exception);
            var json = JsonSerializer.Serialize(snapshot, DiagnosticJsonOptions);
            var jsonPath = Path.Combine(diagnosticsDirectory, $"stt-failure-{stamp}.json");
            var markdownPath = Path.Combine(diagnosticsDirectory, "STT_FAILURE_LATEST.md");
            var latestJsonPath = Path.Combine(diagnosticsDirectory, "STT_FAILURE_LATEST.json");
            File.WriteAllText(jsonPath, json);
            File.WriteAllText(latestJsonPath, json);
            File.WriteAllText(markdownPath, RenderFailureMarkdown(snapshot));

            _logger.LogWarning(
                exception,
                "STT failure diagnostics written. Markdown: {MarkdownPath}. Json: {JsonPath}. PreservedAudio: {PreservedAudioPath}.",
                markdownPath,
                jsonPath,
                snapshot.PreservedAudioPath);
        }
        catch (Exception diagnosticException)
        {
            _logger.LogWarning(diagnosticException, "Failed to write STT failure diagnostics.");
        }
    }

    private SttFailureSnapshot CreateFailureSnapshot(
        string inputPath,
        string preservedAudioPath,
        long audioLength,
        TimeSpan elapsed,
        Exception exception)
    {
        var workerSnapshot = _workerStartSnapshot;
        var process = _workerProcess;
        return new SttFailureSnapshot
        {
            CapturedUtc = DateTimeOffset.UtcNow,
            ContentRootPath = _environment.ContentRootPath,
            OriginalInputPath = inputPath,
            PreservedAudioPath = File.Exists(preservedAudioPath) ? preservedAudioPath : string.Empty,
            AudioBytes = audioLength,
            ElapsedMs = elapsed.TotalMilliseconds,
            WhisperModelSize = _options.WhisperModelSize,
            WhisperDevice = _options.WhisperDevice,
            WhisperComputeType = _options.WhisperComputeType,
            WhisperLanguage = _options.WhisperLanguage,
            WhisperBeamSize = _options.WhisperBeamSize,
            WhisperVadMinSilenceDurationMs = _options.WhisperVadMinSilenceDurationMs,
            ConfiguredPythonExecutable = _options.PythonExecutable,
            ConfiguredPythonArguments = [.. _options.PythonArguments],
            WorkerProcessId = workerSnapshot?.ProcessId,
            WorkerStartedUtc = workerSnapshot?.StartedUtc,
            WorkerPythonExecutable = workerSnapshot?.PythonExecutable ?? string.Empty,
            WorkerArguments = workerSnapshot?.Arguments ?? [],
            WorkerScriptPath = workerSnapshot?.ScriptPath ?? string.Empty,
            WorkerWorkingDirectory = workerSnapshot?.WorkingDirectory ?? string.Empty,
            WorkerHasExited = TryGetHasExited(process),
            WorkerExitCode = TryGetExitCode(process),
            WorkerStderrTail = GetWorkerErrorTail(),
            Exception = exception.ToString()
        };
    }

    private static string RenderFailureMarkdown(SttFailureSnapshot snapshot)
    {
        using var writer = new StringWriter();
        writer.WriteLine("# STT Failure Diagnostics");
        writer.WriteLine();
        writer.WriteLine($"- **Captured UTC:** `{snapshot.CapturedUtc:O}`");
        writer.WriteLine($"- **Content Root:** `{snapshot.ContentRootPath}`");
        writer.WriteLine($"- **Original Input:** `{snapshot.OriginalInputPath}`");
        writer.WriteLine($"- **Preserved Audio:** `{snapshot.PreservedAudioPath}`");
        writer.WriteLine($"- **Audio Bytes:** `{snapshot.AudioBytes}`");
        writer.WriteLine($"- **Elapsed Ms:** `{snapshot.ElapsedMs:N1}`");
        writer.WriteLine($"- **Python Executable:** `{snapshot.WorkerPythonExecutable}`");
        writer.WriteLine($"- **Python Arguments:** `{string.Join(" ", snapshot.WorkerArguments.Select(QuoteArgument))}`");
        writer.WriteLine($"- **Script Path:** `{snapshot.WorkerScriptPath}`");
        writer.WriteLine($"- **Working Directory:** `{snapshot.WorkerWorkingDirectory}`");
        writer.WriteLine($"- **Process Id:** `{snapshot.WorkerProcessId}`");
        writer.WriteLine($"- **Worker Has Exited:** `{snapshot.WorkerHasExited}`");
        writer.WriteLine($"- **Worker Exit Code:** `{snapshot.WorkerExitCode}`");
        writer.WriteLine($"- **Whisper Model:** `{snapshot.WhisperModelSize}`");
        writer.WriteLine($"- **Whisper Device:** `{snapshot.WhisperDevice}`");
        writer.WriteLine($"- **Whisper Compute Type:** `{snapshot.WhisperComputeType}`");
        writer.WriteLine();
        writer.WriteLine("## Worker Stderr Tail");
        writer.WriteLine();
        if (snapshot.WorkerStderrTail.Count == 0)
        {
            writer.WriteLine("_No stderr captured._");
        }
        else
        {
            writer.WriteLine("```text");
            foreach (var line in snapshot.WorkerStderrTail)
            {
                writer.WriteLine(line);
            }
            writer.WriteLine("```");
        }

        writer.WriteLine();
        writer.WriteLine("## Exception");
        writer.WriteLine();
        writer.WriteLine("```text");
        writer.WriteLine(snapshot.Exception);
        writer.WriteLine("```");
        return writer.ToString();
    }

    private string RepositoryRootPath()
    {
        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        return string.Equals(Path.GetFileName(contentRoot), "Merlin.Backend", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(contentRoot, ".."))
            : contentRoot;
    }

    private void RememberWorkerError(string line)
    {
        lock (_workerErrorLock)
        {
            _workerErrorTail.Enqueue(line);
            while (_workerErrorTail.Count > 40)
            {
                _workerErrorTail.Dequeue();
            }
        }
    }

    private List<string> GetWorkerErrorTail()
    {
        lock (_workerErrorLock)
        {
            return [.. _workerErrorTail];
        }
    }

    private void ClearWorkerErrorTail()
    {
        lock (_workerErrorLock)
        {
            _workerErrorTail.Clear();
        }
    }

    private static bool? TryGetHasExited(Process? process)
    {
        try
        {
            return process?.HasExited;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetExitCode(Process? process)
    {
        try
        {
            return process is { HasExited: true } ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }

    private static string QuoteArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
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

    private void StopWorker(bool clearSnapshot = true)
    {
        var process = _workerProcess;
        _workerInput?.Dispose();
        _workerOutput?.Dispose();
        _workerInput = null;
        _workerOutput = null;
        _workerProcess = null;
        if (clearSnapshot)
        {
            _workerStartSnapshot = null;
        }

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

    private sealed record WorkerStartSnapshot(
        int ProcessId,
        string PythonExecutable,
        IReadOnlyList<string> Arguments,
        string ScriptPath,
        string WorkingDirectory,
        DateTimeOffset StartedUtc);

    private sealed class SttFailureSnapshot
    {
        public DateTimeOffset CapturedUtc { get; init; }

        public string ContentRootPath { get; init; } = string.Empty;

        public string OriginalInputPath { get; init; } = string.Empty;

        public string PreservedAudioPath { get; init; } = string.Empty;

        public long AudioBytes { get; init; }

        public double ElapsedMs { get; init; }

        public string WhisperModelSize { get; init; } = string.Empty;

        public string WhisperDevice { get; init; } = string.Empty;

        public string WhisperComputeType { get; init; } = string.Empty;

        public string WhisperLanguage { get; init; } = string.Empty;

        public int WhisperBeamSize { get; init; }

        public int WhisperVadMinSilenceDurationMs { get; init; }

        public string ConfiguredPythonExecutable { get; init; } = string.Empty;

        public IReadOnlyList<string> ConfiguredPythonArguments { get; init; } = [];

        public int? WorkerProcessId { get; init; }

        public DateTimeOffset? WorkerStartedUtc { get; init; }

        public string WorkerPythonExecutable { get; init; } = string.Empty;

        public IReadOnlyList<string> WorkerArguments { get; init; } = [];

        public string WorkerScriptPath { get; init; } = string.Empty;

        public string WorkerWorkingDirectory { get; init; } = string.Empty;

        public bool? WorkerHasExited { get; init; }

        public int? WorkerExitCode { get; init; }

        public IReadOnlyList<string> WorkerStderrTail { get; init; } = [];

        public string Exception { get; init; } = string.Empty;
    }
}
