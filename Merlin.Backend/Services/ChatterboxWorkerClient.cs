using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class ChatterboxWorkerClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ChatterboxWorkerClient> _logger;
    private readonly TtsOptions _options;
    private readonly ChatterboxTimingLogService _timingLog;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private bool _loaded;
    private string _selectedDevice = string.Empty;

    public ChatterboxWorkerClient(
        IOptions<TtsOptions> options,
        IWebHostEnvironment environment,
        ChatterboxTimingLogService timingLog,
        ILogger<ChatterboxWorkerClient> logger)
    {
        _options = options.Value;
        _environment = environment;
        _timingLog = timingLog;
        _logger = logger;
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_loaded && _process is { HasExited: false })
            {
                return;
            }

            await StartProcessAsync(cancellationToken);
            var stopwatch = Stopwatch.StartNew();
            var response = await SendCommandAsync(new
            {
                command = "load",
                model = _options.ChatterboxModel,
                device = _options.ChatterboxDevice
            }, cancellationToken);

            if (!response.Ok)
            {
                if (IsCudaOutOfMemory(response.Error) && !string.Equals(_options.ChatterboxDevice, "cpu", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Chatterbox CUDA load failed due to out-of-memory. Retrying on CPU. Error: {Error}", response.Error);
                    _timingLog.RecordWorkerLoad(new ChatterboxTimingLogService.WorkerLoadTiming
                    {
                        Model = _options.ChatterboxModel,
                        RequestedDevice = _options.ChatterboxDevice,
                        SelectedDevice = _options.ChatterboxDevice,
                        Ok = false,
                        WorkerLoadMs = response.LoadMs,
                        TotalLoadMs = stopwatch.Elapsed.TotalMilliseconds,
                        SampleRate = response.SampleRate,
                        Error = response.Error
                    });
                    await RestartProcessAsync(cancellationToken);
                    response = await SendCommandAsync(new
                    {
                        command = "load",
                        model = _options.ChatterboxModel,
                        device = "cpu"
                    }, cancellationToken);
                }
            }

            if (!response.Ok)
            {
                stopwatch.Stop();
                _timingLog.RecordWorkerLoad(new ChatterboxTimingLogService.WorkerLoadTiming
                {
                    Model = response.Model ?? _options.ChatterboxModel,
                    RequestedDevice = _options.ChatterboxDevice,
                    SelectedDevice = response.Device ?? _options.ChatterboxDevice,
                    Ok = false,
                    WorkerLoadMs = response.LoadMs,
                    TotalLoadMs = stopwatch.Elapsed.TotalMilliseconds,
                    SampleRate = response.SampleRate,
                    Error = response.Error
                });
                throw new InvalidOperationException($"Chatterbox worker load failed: {response.Error}");
            }

            _loaded = true;
            _selectedDevice = response.Device ?? _options.ChatterboxDevice;
            stopwatch.Stop();
            _timingLog.RecordWorkerLoad(new ChatterboxTimingLogService.WorkerLoadTiming
            {
                Model = response.Model ?? _options.ChatterboxModel,
                RequestedDevice = _options.ChatterboxDevice,
                SelectedDevice = _selectedDevice,
                Ok = true,
                WorkerLoadMs = response.LoadMs,
                TotalLoadMs = stopwatch.Elapsed.TotalMilliseconds,
                SampleRate = response.SampleRate
            });
            _logger.LogInformation(
                "Chatterbox model loaded. Model: {Model}. Device: {Device}. WorkerLoadMs: {WorkerLoadMs}. TotalLoadMs: {TotalLoadMs}. SampleRate: {SampleRate}.",
                response.Model ?? _options.ChatterboxModel,
                _selectedDevice,
                response.LoadMs,
                stopwatch.Elapsed.TotalMilliseconds,
                response.SampleRate);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ChatterboxSynthesisResult> SynthesizeAsync(
        string text,
        string referenceVoicePath,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_loaded || _process is null || _process.HasExited)
            {
                throw new InvalidOperationException("Chatterbox worker is not loaded.");
            }

            var response = await SendCommandAsync(new
            {
                command = "synthesize",
                text,
                reference_voice_path = referenceVoicePath,
                exaggeration = _options.ChatterboxExaggeration,
                cfg_weight = _options.ChatterboxCfgWeight,
                temperature = _options.ChatterboxTemperature,
                repetition_penalty = _options.ChatterboxRepetitionPenalty,
                top_p = _options.ChatterboxTopP,
                min_p = _options.ChatterboxMinP
            }, cancellationToken);

            if (!response.Ok)
            {
                throw new InvalidOperationException($"Chatterbox synthesis failed: {response.Error}");
            }

            return new ChatterboxSynthesisResult
            {
                SampleRate = response.SampleRate,
                Channels = response.Channels <= 0 ? 1 : response.Channels,
                Format = string.IsNullOrWhiteSpace(response.Format) ? "s16le" : response.Format,
                Audio = Convert.FromBase64String(response.AudioBase64 ?? string.Empty),
                DurationSeconds = response.DurationSeconds,
                GenerationMs = response.GenerationMs,
                ConditioningMs = response.ConditioningMs
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_process is { HasExited: false })
            {
                try
                {
                    await SendCommandAsync(new { command = "shutdown" }, CancellationToken.None);
                }
                catch
                {
                }

                try
                {
                    _process.Kill(entireProcessTree: true);
                }
                catch
                {
                }
            }

            _process?.Dispose();
            _process = null;
            _loaded = false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StartProcessAsync(CancellationToken cancellationToken)
    {
        await StopProcessAsync();
        var scriptPath = ResolvePath(_options.ChatterboxWorkerScriptPath);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Chatterbox worker script was not found.", scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ChatterboxPythonExecutable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _environment.ContentRootPath
        };
        startInfo.ArgumentList.Add(scriptPath);

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Chatterbox worker process.");

        _ = Task.Run(() => DrainErrorAsync(_process), cancellationToken);
    }

    private async Task RestartProcessAsync(CancellationToken cancellationToken)
    {
        await StopProcessAsync();
        await StartProcessAsync(cancellationToken);
    }

    private async Task StopProcessAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        await Task.CompletedTask;
        _process.Dispose();
        _process = null;
        _loaded = false;
    }

    private async Task<WorkerResponse> SendCommandAsync(object command, CancellationToken cancellationToken)
    {
        if (_process is null || _process.HasExited)
        {
            throw new InvalidOperationException("Chatterbox worker process is not running.");
        }

        var json = JsonSerializer.Serialize(command, JsonOptions);
        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);

        while (!_process.HasExited)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                _logger.LogInformation("Chatterbox worker stdout: {Line}", line);
                continue;
            }

            try
            {
                return JsonSerializer.Deserialize<WorkerResponse>(line, JsonOptions)
                    ?? throw new InvalidOperationException("Chatterbox worker returned invalid JSON.");
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Chatterbox worker returned malformed JSON: {Line}", line);
            }
        }

        throw new InvalidOperationException("Chatterbox worker returned no JSON response.");
    }

    private async Task DrainErrorAsync(Process process)
    {
        while (!process.HasExited)
        {
            var line = await process.StandardError.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            _logger.LogInformation("Chatterbox worker: {Line}", line);
        }
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path, _environment.ContentRootPath);
    }

    private static bool IsCudaOutOfMemory(string? error)
    {
        return !string.IsNullOrWhiteSpace(error) &&
               (error.Contains("out of memory", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("cuda", StringComparison.OrdinalIgnoreCase) && error.Contains("memory", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class WorkerResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("sample_rate")]
        public int SampleRate { get; init; }

        [JsonPropertyName("channels")]
        public int Channels { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("audio_base64")]
        public string? AudioBase64 { get; init; }

        [JsonPropertyName("duration_seconds")]
        public double DurationSeconds { get; init; }

        [JsonPropertyName("generation_ms")]
        public double GenerationMs { get; init; }

        [JsonPropertyName("conditioning_ms")]
        public double ConditioningMs { get; init; }

        [JsonPropertyName("load_ms")]
        public double LoadMs { get; init; }

        [JsonPropertyName("device")]
        public string? Device { get; init; }

        [JsonPropertyName("model")]
        public string? Model { get; init; }
    }
}
