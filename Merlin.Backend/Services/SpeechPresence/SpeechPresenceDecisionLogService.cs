using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.SpeechPresence;

public sealed class SpeechPresenceDecisionLogService : BackgroundService, ISpeechPresenceDecisionLogSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<SpeechPresenceDecisionLogService> _logger;
    private readonly IOptionsMonitor<SpeechPresenceOptions> _options;
    private readonly Channel<string> _entries;
    private long _droppedEntries;
    private long _seenEntries;

    public SpeechPresenceDecisionLogService(
        IOptionsMonitor<SpeechPresenceOptions> options,
        ILogger<SpeechPresenceDecisionLogService> logger)
    {
        _options = options;
        _logger = logger;
        var capacity = Math.Max(1, options.CurrentValue.DecisionLogMaxQueueEntries);
        _entries = Channel.CreateBounded<string>(
            new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            });
    }

    public void TryLogOfficialDecision(SpeechPresenceOfficialDecision decision)
    {
        TryLog(
            "SpeechPresenceOfficialDecision",
            decision.FrameId,
            decision.TimestampUtc,
            decision.SourcePath,
            decision.IsAuthoritative,
            decision.Result);
    }

    public void TryLogBranchObservation(SpeechPresenceBranchObservation observation)
    {
        TryLog(
            "SpeechPresenceBranchObservation",
            observation.FrameId,
            observation.TimestampUtc,
            observation.SourcePath,
            observation.IsAuthoritative,
            observation.Result);
    }

    public void TryLogManualSpeechStartMarker(SpeechPresenceManualMarker marker)
    {
        var options = _options.CurrentValue;
        if (!options.LogDecisions)
        {
            return;
        }

        if (!options.LogDecisionsToFile)
        {
            return;
        }

        var entry = new SpeechPresenceManualMarkerLogEntry
        {
            EventName = "ManualSpeechStartMarker",
            TimestampUtc = marker.TimestampUtc,
            MarkerType = marker.MarkerType,
            Source = marker.Source,
            ClientTimestampUtc = marker.ClientTimestampUtc,
            Note = marker.Note,
            DroppedEntriesBeforeThisEntry = Interlocked.Read(ref _droppedEntries)
        };
        TryWriteSerializedEntry(entry);
    }

    private void TryLog(
        string eventName,
        long frameId,
        DateTimeOffset timestampUtc,
        string sourcePath,
        bool isAuthoritative,
        SpeechPresenceResult result)
    {
        var options = _options.CurrentValue;
        if (!options.LogDecisions)
        {
            return;
        }

        if (options.LogDecisionsToLogger)
        {
            var evidenceForLogger = result.Evidence;
            _logger.LogInformation(
                "{EventName}. FrameId: {FrameId}. State: {State}. Confidence: {Confidence:N2}. ShouldYieldPlayback: {ShouldYieldPlayback}. Reason: {Reason}. SourcePath: {SourcePath}. IsAuthoritative: {IsAuthoritative}. AssistantPlaybackActive: {AssistantPlaybackActive}. RawMicRms: {RawMicRms:N4}. EchoReducedRms: {EchoReducedRms:N4}. PlaybackReferenceRms: {PlaybackReferenceRms:N4}. VadConfidence: {VadConfidence:N2}. Correlation: {Correlation}.",
                eventName,
                frameId,
                result.State,
                result.Confidence,
                result.ShouldYieldPlayback,
                result.Reason,
                sourcePath,
                isAuthoritative,
                evidenceForLogger.AssistantPlaybackActive,
                evidenceForLogger.RawMicRms,
                evidenceForLogger.EchoReducedRms,
                evidenceForLogger.PlaybackReferenceRms,
                evidenceForLogger.VadConfidence,
                evidenceForLogger.PlaybackCorrelationScore);
        }

        if (!options.LogDecisionsToFile)
        {
            return;
        }

        var seen = Interlocked.Increment(ref _seenEntries);
        var sampleEvery = Math.Max(1, options.DecisionLogSampleEveryNFrames);
        if (sampleEvery > 1 && seen % sampleEvery != 0)
        {
            return;
        }

        var evidence = result.Evidence;
        var entry = new SpeechPresenceDecisionLogEntry
        {
            EventName = eventName,
            TimestampUtc = timestampUtc,
            FrameId = frameId,
            State = result.State.ToString(),
            Confidence = result.Confidence,
            IsUserSpeaking = result.IsUserSpeaking,
            ShouldYieldPlayback = result.ShouldYieldPlayback,
            Reason = result.Reason,
            AssistantPlaybackActive = evidence.AssistantPlaybackActive,
            RawMicRms = evidence.RawMicRms,
            RawMicPeak = evidence.RawMicPeak,
            EchoReducedRms = evidence.EchoReducedRms,
            EchoReducedPeak = evidence.EchoReducedPeak,
            PlaybackReferenceRms = evidence.PlaybackReferenceRms,
            PlaybackReferencePeak = evidence.PlaybackReferencePeak,
            VadConfidence = evidence.VadConfidence,
            VadSpeechDetected = evidence.VadSpeechDetected,
            PlaybackCorrelationScore = evidence.PlaybackCorrelationScore,
            StrongSelfEchoEvidence = evidence.StrongSelfEchoEvidence,
            UserSpeechScoreLegacy = evidence.UserSpeechScoreLegacy,
            SourcePath = sourcePath,
            IsAuthoritative = isAuthoritative,
            DroppedEntriesBeforeThisEntry = Interlocked.Read(ref _droppedEntries)
        };

        TryWriteSerializedEntry(entry);
    }

    private void TryWriteSerializedEntry<TEntry>(TEntry entry)
    {
        if (!_entries.Writer.TryWrite(JsonSerializer.Serialize(entry, JsonOptions)))
        {
            Interlocked.Increment(ref _droppedEntries);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<string>(256);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(GetFlushIntervalMs()));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_entries.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);
                    if (batch.Count >= 256)
                    {
                        await FlushAsync(batch, stoppingToken);
                    }
                }

                await timer.WaitForNextTickAsync(stoppingToken);
                await FlushAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            while (_entries.Reader.TryRead(out var entry))
            {
                batch.Add(entry);
            }

            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(
        List<string> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var options = _options.CurrentValue;
        if (!options.LogDecisions || !options.LogDecisionsToFile)
        {
            batch.Clear();
            return;
        }

        try
        {
            var path = ResolveDecisionLogPath(options.DecisionLogFilePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var builder = new StringBuilder(batch.Count * 320);
            foreach (var entry in batch)
            {
                builder.AppendLine(entry);
            }

            await File.AppendAllTextAsync(path, builder.ToString(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "SpeechPresenceDecisionFileWriteFailed. Path: {Path}", options.DecisionLogFilePath);
        }
        finally
        {
            batch.Clear();
        }
    }

    private int GetFlushIntervalMs()
    {
        return Math.Clamp(_options.CurrentValue.DecisionLogFlushIntervalMs, 50, 5000);
    }

    private static string ResolveDecisionLogPath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? "speech-presence.tmp.log"
            : configuredPath;

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }
}
