using System.Text.Json;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class ChatterboxTimingLogService : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ChatterboxTimingLogService> _logger;
    private readonly TtsOptions _options;
    private readonly object _gate = new();
    private readonly List<EndpointTiming> _endpointTimings = [];
    private readonly List<RouterTiming> _routerTimings = [];
    private readonly List<WorkerLoadTiming> _workerLoadTimings = [];
    private readonly List<SynthesisTiming> _synthesisTimings = [];
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private DateTimeOffset? _stoppedUtc;

    public ChatterboxTimingLogService(
        IWebHostEnvironment environment,
        IOptions<TtsOptions> options,
        ILogger<ChatterboxTimingLogService> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stoppedUtc = DateTimeOffset.UtcNow;
        WriteReports();
        return Task.CompletedTask;
    }

    public void RecordEndpoint(EndpointTiming timing)
    {
        lock (_gate)
        {
            _endpointTimings.Add(timing);
        }
    }

    public void RecordRouter(RouterTiming timing)
    {
        lock (_gate)
        {
            _routerTimings.Add(timing);
        }
    }

    public void RecordWorkerLoad(WorkerLoadTiming timing)
    {
        lock (_gate)
        {
            _workerLoadTimings.Add(timing);
        }
    }

    public void RecordSynthesis(SynthesisTiming timing)
    {
        lock (_gate)
        {
            _synthesisTimings.Add(timing);
        }
    }

    private void WriteReports()
    {
        try
        {
            var snapshot = CreateSnapshot();
            var root = RepositoryRootPath();
            Directory.CreateDirectory(root);

            var markdownPath = Path.Combine(root, "CHATTERBOX_TIMING_LOG.md");
            var jsonPath = Path.Combine(root, "CHATTERBOX_TIMING_LOG.json");

            File.WriteAllText(markdownPath, RenderMarkdown(snapshot));
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(snapshot, JsonOptions));

            _logger.LogInformation(
                "Chatterbox timing log written. Markdown: {MarkdownPath}. Json: {JsonPath}.",
                markdownPath,
                jsonPath);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to write Chatterbox timing log.");
        }
    }

    private TimingSnapshot CreateSnapshot()
    {
        lock (_gate)
        {
            return new TimingSnapshot
            {
                StartedUtc = _startedUtc,
                StoppedUtc = _stoppedUtc,
                ContentRootPath = _environment.ContentRootPath,
                Provider = _options.Provider,
                FallbackProvider = _options.FallbackProvider,
                ChatterboxModel = _options.ChatterboxModel,
                ChatterboxRequestedDevice = _options.ChatterboxDevice,
                ChatterboxPythonExecutable = _options.ChatterboxPythonExecutable,
                ChatterboxWorkerScriptPath = _options.ChatterboxWorkerScriptPath,
                ChatterboxKeepWarm = _options.ChatterboxKeepWarm,
                ChatterboxPhraseCacheEnabled = _options.ChatterboxEnablePhraseCache,
                EndpointTimings = [.. _endpointTimings],
                RouterTimings = [.. _routerTimings],
                WorkerLoadTimings = [.. _workerLoadTimings],
                SynthesisTimings = [.. _synthesisTimings]
            };
        }
    }

    private string RepositoryRootPath()
    {
        var contentRoot = Path.GetFullPath(_environment.ContentRootPath);
        return string.Equals(Path.GetFileName(contentRoot), "Merlin.Backend", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.Combine(contentRoot, ".."))
            : contentRoot;
    }

    private static string RenderMarkdown(TimingSnapshot snapshot)
    {
        using var writer = new StringWriter();
        writer.WriteLine("# Chatterbox Timing Log");
        writer.WriteLine();
        writer.WriteLine($"- **Started UTC:** `{snapshot.StartedUtc:O}`");
        writer.WriteLine($"- **Stopped UTC:** `{snapshot.StoppedUtc:O}`");
        writer.WriteLine($"- **Content Root:** `{snapshot.ContentRootPath}`");
        writer.WriteLine($"- **Provider:** `{snapshot.Provider}`");
        writer.WriteLine($"- **Fallback Provider:** `{snapshot.FallbackProvider}`");
        writer.WriteLine($"- **Chatterbox Model:** `{snapshot.ChatterboxModel}`");
        writer.WriteLine($"- **Requested Device:** `{snapshot.ChatterboxRequestedDevice}`");
        writer.WriteLine($"- **Python Executable:** `{snapshot.ChatterboxPythonExecutable}`");
        writer.WriteLine($"- **Worker Script:** `{snapshot.ChatterboxWorkerScriptPath}`");
        writer.WriteLine($"- **Keep Warm:** `{snapshot.ChatterboxKeepWarm}`");
        writer.WriteLine($"- **Phrase Cache Enabled:** `{snapshot.ChatterboxPhraseCacheEnabled}`");
        writer.WriteLine();

        WriteWorkerLoads(writer, snapshot.WorkerLoadTimings);
        WriteEndpointTimings(writer, snapshot.EndpointTimings);
        WriteRouterTimings(writer, snapshot.RouterTimings);
        WriteSynthesisTimings(writer, snapshot.SynthesisTimings);

        writer.WriteLine("## Raw JSON");
        writer.WriteLine();
        writer.WriteLine("```json");
        writer.WriteLine(JsonSerializer.Serialize(snapshot, JsonOptions));
        writer.WriteLine("```");

        return writer.ToString();
    }

    private static void WriteWorkerLoads(TextWriter writer, IReadOnlyList<WorkerLoadTiming> timings)
    {
        writer.WriteLine("## Worker Loads");
        writer.WriteLine();
        if (timings.Count == 0)
        {
            writer.WriteLine("No Chatterbox worker load attempts were recorded.");
            writer.WriteLine();
            return;
        }

        foreach (var timing in timings)
        {
            writer.WriteLine($"- `{timing.TimestampUtc:O}` Model `{timing.Model}`, requested `{timing.RequestedDevice}`, selected `{timing.SelectedDevice}`, ok `{timing.Ok}`, worker load `{timing.WorkerLoadMs:F1}ms`, total load `{timing.TotalLoadMs:F1}ms`, sample rate `{timing.SampleRate}`");
            if (!string.IsNullOrWhiteSpace(timing.Error))
            {
                writer.WriteLine($"  Error: `{timing.Error}`");
            }
        }

        writer.WriteLine();
    }

    private static void WriteEndpointTimings(TextWriter writer, IReadOnlyList<EndpointTiming> timings)
    {
        writer.WriteLine("## Endpoint Timings");
        writer.WriteLine();
        if (timings.Count == 0)
        {
            writer.WriteLine("No synthesize endpoint calls were recorded.");
            writer.WriteLine();
            return;
        }

        foreach (var timing in timings)
        {
            writer.WriteLine($"- `{timing.TimestampUtc:O}` `{timing.Endpoint}` chars `{timing.Chars}`, ok `{timing.Ok}`, elapsed `{timing.ElapsedMs:F1}ms`, response started `{timing.ResponseStarted}`");
            if (!string.IsNullOrWhiteSpace(timing.Error))
            {
                writer.WriteLine($"  Error: `{timing.Error}`");
            }
        }

        writer.WriteLine();
    }

    private static void WriteRouterTimings(TextWriter writer, IReadOnlyList<RouterTiming> timings)
    {
        writer.WriteLine("## Router Events");
        writer.WriteLine();
        if (timings.Count == 0)
        {
            writer.WriteLine("No TTS router events were recorded.");
            writer.WriteLine();
            return;
        }

        foreach (var timing in timings)
        {
            writer.WriteLine($"- `{timing.TimestampUtc:O}` configured `{timing.ConfiguredProvider}`, selected `{timing.SelectedProvider}`, fallback `{timing.UsedFallback}`, chars `{timing.Chars}`, elapsed `{timing.ElapsedMs:F1}ms`, reason `{timing.Reason}`");
        }

        writer.WriteLine();
    }

    private static void WriteSynthesisTimings(TextWriter writer, IReadOnlyList<SynthesisTiming> timings)
    {
        writer.WriteLine("## Chatterbox Synthesis");
        writer.WriteLine();
        if (timings.Count == 0)
        {
            writer.WriteLine("No Chatterbox synthesis calls were recorded.");
            writer.WriteLine();
            return;
        }

        foreach (var timing in timings)
        {
            writer.WriteLine($"### `{timing.TimestampUtc:O}` `{timing.RequestId}`");
            writer.WriteLine();
            writer.WriteLine($"- **OK:** `{timing.Ok}`");
            writer.WriteLine($"- **Chars:** `{timing.Chars}`");
            writer.WriteLine($"- **Chunks:** `{timing.ChunkCount}`");
            writer.WriteLine($"- **Requested Device:** `{timing.RequestedDevice}`");
            writer.WriteLine($"- **Total Wall Ms:** `{timing.TotalWallMs:F1}`");
            writer.WriteLine($"- **Total Worker Generation Ms:** `{timing.TotalGenerationMs:F1}`");
            writer.WriteLine($"- **Total Conditioning Ms:** `{timing.TotalConditioningMs:F1}`");
            writer.WriteLine($"- **Audio Duration Seconds:** `{timing.AudioDurationSeconds:F3}`");
            writer.WriteLine($"- **Realtime Factor:** `{timing.RealtimeFactor:F3}`");
            writer.WriteLine($"- **Bytes:** `{timing.Bytes}`");
            writer.WriteLine($"- **Cache Hits:** `{timing.CacheHits}`");
            writer.WriteLine($"- **Cache Misses:** `{timing.CacheMisses}`");
            writer.WriteLine($"- **Text Preview:** `{timing.TextPreview}`");
            if (!string.IsNullOrWhiteSpace(timing.Error))
            {
                writer.WriteLine($"- **Error:** `{timing.Error}`");
            }

            writer.WriteLine();
            writer.WriteLine("| Chunk | Chars | Cache | Generation ms | Conditioning ms | Chunk wall ms | Audio sec | RTF | Bytes |");
            writer.WriteLine("| ---: | ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (var chunk in timing.Chunks)
            {
                writer.WriteLine($"| {chunk.Index}/{chunk.Count} | {chunk.Chars} | {chunk.CacheStatus} | {chunk.GenerationMs:F1} | {chunk.ConditioningMs:F1} | {chunk.TotalChunkMs:F1} | {chunk.DurationSeconds:F3} | {chunk.RealtimeFactor:F3} | {chunk.Bytes} |");
            }

            writer.WriteLine();
        }
    }

    public sealed class EndpointTiming
    {
        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
        public string Endpoint { get; init; } = string.Empty;
        public int Chars { get; init; }
        public double ElapsedMs { get; init; }
        public bool ResponseStarted { get; init; }
        public bool Ok { get; init; }
        public string? Error { get; init; }
    }

    public sealed class RouterTiming
    {
        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
        public string ConfiguredProvider { get; init; } = string.Empty;
        public string SelectedProvider { get; init; } = string.Empty;
        public bool UsedFallback { get; init; }
        public string Reason { get; init; } = string.Empty;
        public int Chars { get; init; }
        public double ElapsedMs { get; init; }
    }

    public sealed class WorkerLoadTiming
    {
        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
        public string Model { get; init; } = string.Empty;
        public string RequestedDevice { get; init; } = string.Empty;
        public string SelectedDevice { get; init; } = string.Empty;
        public bool Ok { get; init; }
        public double WorkerLoadMs { get; init; }
        public double TotalLoadMs { get; init; }
        public int SampleRate { get; init; }
        public string? Error { get; init; }
    }

    public sealed class SynthesisTiming
    {
        public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
        public string RequestId { get; init; } = string.Empty;
        public int Chars { get; init; }
        public int ChunkCount { get; init; }
        public string RequestedDevice { get; init; } = string.Empty;
        public double TotalWallMs { get; init; }
        public double TotalGenerationMs { get; init; }
        public double TotalConditioningMs { get; init; }
        public double AudioDurationSeconds { get; init; }
        public double RealtimeFactor { get; init; }
        public int Bytes { get; init; }
        public int CacheHits { get; init; }
        public int CacheMisses { get; init; }
        public string TextPreview { get; init; } = string.Empty;
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public IReadOnlyList<ChunkTiming> Chunks { get; init; } = [];
    }

    public sealed class ChunkTiming
    {
        public int Index { get; init; }
        public int Count { get; init; }
        public int Chars { get; init; }
        public string CacheStatus { get; init; } = "miss";
        public double GenerationMs { get; init; }
        public double ConditioningMs { get; init; }
        public double TotalChunkMs { get; init; }
        public int Bytes { get; init; }
        public double DurationSeconds { get; init; }
        public double RealtimeFactor { get; init; }
        public string TextPreview { get; init; } = string.Empty;
    }

    public sealed class TimingSnapshot
    {
        public DateTimeOffset StartedUtc { get; init; }
        public DateTimeOffset? StoppedUtc { get; init; }
        public string ContentRootPath { get; init; } = string.Empty;
        public string Provider { get; init; } = string.Empty;
        public string FallbackProvider { get; init; } = string.Empty;
        public string ChatterboxModel { get; init; } = string.Empty;
        public string ChatterboxRequestedDevice { get; init; } = string.Empty;
        public string ChatterboxPythonExecutable { get; init; } = string.Empty;
        public string ChatterboxWorkerScriptPath { get; init; } = string.Empty;
        public bool ChatterboxKeepWarm { get; init; }
        public bool ChatterboxPhraseCacheEnabled { get; init; }
        public IReadOnlyList<EndpointTiming> EndpointTimings { get; init; } = [];
        public IReadOnlyList<RouterTiming> RouterTimings { get; init; } = [];
        public IReadOnlyList<WorkerLoadTiming> WorkerLoadTimings { get; init; } = [];
        public IReadOnlyList<SynthesisTiming> SynthesisTimings { get; init; } = [];
    }
}
