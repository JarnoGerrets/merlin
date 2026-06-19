using System.Text.Json;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Merlin.Backend.Services.BargeIn;

public sealed class SelfSpeechGateDiagnosticsWriter : ISelfSpeechGateDiagnosticsWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly string _contentRootPath;
    private readonly ILogger<SelfSpeechGateDiagnosticsWriter> _logger;
    private readonly object _syncRoot = new();
    private long _entryCount;

    public SelfSpeechGateDiagnosticsWriter(
        IHostEnvironment environment,
        ILogger<SelfSpeechGateDiagnosticsWriter> logger)
        : this(environment.ContentRootPath, logger)
    {
    }

    internal SelfSpeechGateDiagnosticsWriter(
        string contentRootPath,
        ILogger<SelfSpeechGateDiagnosticsWriter> logger)
    {
        _contentRootPath = string.IsNullOrWhiteSpace(contentRootPath)
            ? Directory.GetCurrentDirectory()
            : contentRootPath;
        _logger = logger;
    }

    public void Write(SelfSpeechGateDiagnosticEntry entry, BargeInOptions options)
    {
        var gateOptions = options.SelfSpeechSuppression;
        if (!gateOptions.DiagnosticsFileEnabled || !ShouldInclude(entry, gateOptions))
        {
            return;
        }

        var sampleEvery = Math.Max(1, gateOptions.DiagnosticsSampleEveryNFrames);
        var count = Interlocked.Increment(ref _entryCount);
        if (count % sampleEvery != 0)
        {
            return;
        }

        try
        {
            var path = ResolvePath(gateOptions.DiagnosticsFilePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(entry, SerializerOptions);
            lock (_syncRoot)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            _logger.LogWarning(exception, "Self-speech gate diagnostics write failed.");
        }
    }

    private string ResolvePath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? "Logs/SELF_SPEECH_GATE_DIAGNOSTICS.jsonl"
            : configuredPath;
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_contentRootPath, path));
    }

    private static bool ShouldInclude(SelfSpeechGateDiagnosticEntry entry, SelfSpeechSuppressionOptions options)
    {
        return entry.Decision switch
        {
            nameof(SelfSpeechDecision.Allow) => options.DiagnosticsIncludeAllowed,
            nameof(SelfSpeechDecision.SuppressAsSelfEcho) => options.DiagnosticsIncludeSuppressed,
            nameof(SelfSpeechDecision.Uncertain) => options.DiagnosticsIncludeUncertain,
            _ => true
        };
    }
}
