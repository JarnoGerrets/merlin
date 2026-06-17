using System.Globalization;
using System.Text;
using System.Text.Json;
using Merlin.VoiceTest.Models;

namespace Merlin.VoiceTest.Services;

public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task WriteAsync(TestSessionResult session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(session.ReportDirectory);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "session_results.json"), JsonSerializer.Serialize(session, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "phrase_results.csv"), BuildPhraseCsv(session), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "audio_diagnostics.csv"), BuildAudioCsv(session), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "session_summary.md"), BuildSummary(session), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "confusion_report.md"), BuildConfusionReport(session), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "normalizer_suggestions.md"), BuildNormalizerSuggestions(session), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "agent_action_items.md"), BuildAgentActionItems(session), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(session.ReportDirectory, "chatgpt_review_packet.md"), BuildChatGptPacket(session), cancellationToken);
    }

    private static string BuildPhraseCsv(TestSessionResult session)
    {
        var writer = new StringBuilder();
        writer.AppendLine("phraseId,category,expectedText,actualTranscript,userRating,exactMatch,wordErrorRate,charErrorRate,missingImportantTerms,suspectedConfusions,transcriptionLatencyMs,durationMs,rmsLevel,peakLevel,clippingDetected,audioFilePath");
        foreach (var attempt in session.Attempts)
        {
            var phrase = FindPhrase(session, attempt.PhraseId);
            writer.AppendCsvLine(
                attempt.PhraseId,
                phrase?.Category ?? "",
                attempt.ExpectedText,
                attempt.ActualTranscript,
                attempt.UserRating,
                attempt.Evaluation.ExactMatchAfterNormalization,
                attempt.Evaluation.WordErrorRate,
                attempt.Evaluation.CharacterErrorRate,
                string.Join("; ", attempt.Evaluation.MissingImportantTerms),
                string.Join("; ", attempt.Evaluation.SuspectedConfusionPairs),
                attempt.Transcription.LatencyMs,
                attempt.AudioDiagnostics.DurationMs,
                attempt.AudioDiagnostics.RmsLevel,
                attempt.AudioDiagnostics.PeakLevel,
                attempt.AudioDiagnostics.ClippingDetected,
                attempt.AudioDiagnostics.AudioFilePath);
        }

        return writer.ToString();
    }

    private static string BuildAudioCsv(TestSessionResult session)
    {
        var writer = new StringBuilder();
        writer.AppendLine("phraseId,attemptNumber,durationMs,sampleRate,channelCount,rmsLevel,peakLevel,clippingDetected,signalTooQuiet,signalTooLoud,silenceBeforeSpeechMs,silenceAfterSpeechMs,speechDurationMs,audioFilePath");
        foreach (var attempt in session.Attempts)
        {
            var d = attempt.AudioDiagnostics;
            writer.AppendCsvLine(
                attempt.PhraseId,
                attempt.AttemptNumber,
                d.DurationMs,
                d.SampleRate,
                d.ChannelCount,
                d.RmsLevel,
                d.PeakLevel,
                d.ClippingDetected,
                d.SignalTooQuiet,
                d.SignalTooLoud,
                d.SilenceBeforeSpeechMs,
                d.SilenceAfterSpeechMs,
                d.SpeechDurationMs,
                d.AudioFilePath);
        }

        return writer.ToString();
    }

    private static string BuildSummary(TestSessionResult session)
    {
        var attempts = session.Attempts;
        var exact = attempts.Count(a => a.Evaluation.ExactMatchAfterNormalization);
        var avgLatency = Average(attempts.Select(a => a.Transcription.LatencyMs));
        var avgDuration = Average(attempts.Select(a => a.AudioDiagnostics.DurationMs));
        var worst = attempts.OrderByDescending(a => a.Evaluation.WordErrorRate + a.Evaluation.CharacterErrorRate).Take(10).ToList();
        var commonErrors = attempts
            .SelectMany(a => a.Evaluation.SuspectedConfusionPairs.Concat(AudioFlags(a)))
            .GroupBy(x => x)
            .OrderByDescending(g => g.Count())
            .Take(10);
        var importantTerms = attempts.SelectMany(a => FindPhrase(session, a.PhraseId)?.ImportantTerms ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var missingImportant = attempts.SelectMany(a => a.Evaluation.MissingImportantTerms).Count();
        var importantAccuracy = importantTerms.Count == 0 || attempts.Count == 0
            ? 1
            : Math.Max(0, 1 - missingImportant / (double)Math.Max(1, importantTerms.Count * attempts.Count));

        var writer = new StringBuilder();
        writer.AppendLine("# Merlin VoiceTest Session Summary");
        writer.AppendLine();
        writer.AppendLine($"- Date/time: {session.StartedAt:O} - {session.FinishedAt:O}");
        writer.AppendLine($"- Machine/user: {session.Environment.MachineName}/{session.Environment.UserName}");
        writer.AppendLine($"- OS/.NET: {session.Environment.OsVersion}; .NET {session.Environment.DotNetVersion}");
        writer.AppendLine($"- STT config: model={session.Config.Model}, device={session.Config.DeviceType}, compute={session.Config.ComputeType}, beam={session.Config.BeamSize}, language={session.Config.Language}, task={session.Config.Task}, temperature={session.Config.Temperature}");
        writer.AppendLine($"- Total phrases: {session.Phrases.Count}");
        writer.AppendLine($"- Attempts: {attempts.Count}");
        writer.AppendLine($"- Exact match count: {exact}");
        writer.AppendLine($"- Correct ratings: {attempts.Count(a => a.UserRating == "correct")}");
        writer.AppendLine($"- Minor mistake ratings: {attempts.Count(a => a.UserRating == "minor mistake")}");
        writer.AppendLine($"- Wrong ratings: {attempts.Count(a => a.UserRating == "wrong")}");
        writer.AppendLine($"- Skipped count: {session.SkippedPhraseIds.Count}");
        writer.AppendLine($"- Average transcription latency: {avgLatency:N0} ms");
        writer.AppendLine($"- Average audio duration: {avgDuration:N0} ms");
        writer.AppendLine($"- Important-term accuracy estimate: {importantAccuracy:P1}");
        writer.AppendLine();
        writer.AppendLine("## Most Common Error Types");
        writer.AppendLine();
        foreach (var error in commonErrors)
        {
            writer.AppendLine($"- {error.Key}: {error.Count()}");
        }

        writer.AppendLine();
        writer.AppendLine("## Worst Phrases");
        writer.AppendLine();
        foreach (var attempt in worst)
        {
            writer.AppendLine($"- {attempt.PhraseId}: WER {attempt.Evaluation.WordErrorRate:N2}, CER {attempt.Evaluation.CharacterErrorRate:N2}, expected `{attempt.ExpectedText}`, actual `{attempt.ActualTranscript}`");
        }

        writer.AppendLine();
        writer.AppendLine("## Recommended Next Steps");
        writer.AppendLine();
        writer.AppendLine("- Review confusion_report.md for whether failures cluster around audio/VAD, prompt vocabulary, or normalizer candidates.");
        writer.AppendLine("- If first or last words are clipped, tune VAD pre-roll and end silence before changing the STT model.");
        writer.AppendLine("- If technical terms are consistently substituted with clean audio, test a Merlin-specific initial_prompt and scoped transcript normalizer.");
        writer.AppendLine("- Keep production behavior unchanged until these reports show repeatable evidence.");
        return writer.ToString();
    }

    private static string BuildConfusionReport(TestSessionResult session)
    {
        var groups = session.Attempts
            .SelectMany(a => a.Evaluation.SuspectedConfusionPairs.Concat(AudioFlags(a)), (a, flag) => new { Attempt = a, Flag = flag })
            .GroupBy(x => x.Flag)
            .OrderByDescending(g => g.Count());
        var writer = new StringBuilder();
        writer.AppendLine("# Confusion Report");
        writer.AppendLine();
        foreach (var group in groups)
        {
            writer.AppendLine($"## {group.Key}");
            writer.AppendLine();
            writer.AppendLine($"Likely fix area: {FixArea(group.Key)}");
            writer.AppendLine();
            foreach (var item in group)
            {
                writer.AppendLine($"- {item.Attempt.PhraseId}: expected `{item.Attempt.ExpectedText}`, actual `{item.Attempt.ActualTranscript}`");
            }

            writer.AppendLine();
        }

        if (!groups.Any())
        {
            writer.AppendLine("No suspected confusion groups were detected.");
        }

        return writer.ToString();
    }

    private static string BuildNormalizerSuggestions(TestSessionResult session)
    {
        var changed = session.Attempts.Where(a => a.NormalizerPreview.Changed).ToList();
        var writer = new StringBuilder();
        writer.AppendLine("# Normalizer Suggestions");
        writer.AppendLine();
        writer.AppendLine("Normalizer preview only. Not applied to production Merlin.");
        writer.AppendLine();
        foreach (var attempt in changed)
        {
            writer.AppendLine($"## {attempt.PhraseId}");
            writer.AppendLine();
            writer.AppendLine($"Raw transcript: `{attempt.NormalizerPreview.RawTranscript}`");
            writer.AppendLine($"Preview normalized transcript: `{attempt.NormalizerPreview.PreviewTranscript}`");
            writer.AppendLine("Reasons:");
            foreach (var reason in attempt.NormalizerPreview.Reasons)
            {
                writer.AppendLine($"- {reason}");
            }

            writer.AppendLine($"Evidence: expected `{attempt.ExpectedText}`");
            writer.AppendLine("Risk: Low to medium; implement only as scoped suggestions until regression tests prove safety.");
            writer.AppendLine();
        }

        if (changed.Count == 0)
        {
            writer.AppendLine("No normalizer preview changes were suggested in this session.");
        }

        return writer.ToString();
    }

    private static string BuildAgentActionItems(TestSessionResult session)
    {
        var writer = new StringBuilder();
        writer.AppendLine("# Agent Action Items");
        writer.AppendLine();
        writer.AppendLine("## Highest-Impact Fixes");
        writer.AppendLine();
        writer.AppendLine("- Add STT diagnostics around production capture/transcription timing and audio metrics.");
        writer.AppendLine("- Add a Merlin technical vocabulary initial_prompt if clean-audio substitutions repeat.");
        writer.AppendLine("- Add a scoped post-STT transcript normalizer only after this harness produces evidence.");
        writer.AppendLine("- Tune VAD pre-roll/end silence if reports show clipped first words, clipped endings, or long silence.");
        writer.AppendLine("- Add confidence-based clarification for dangerous commands whose transcript changed important terms.");
        writer.AppendLine("- Add golden phrase regression tests using the worst phrases from this session.");
        writer.AppendLine();
        writer.AppendLine("## Likely Production Files");
        writer.AppendLine();
        writer.AppendLine("- Merlin.Backend/Services/PythonVoiceService.cs");
        writer.AppendLine("- Merlin.Backend/Configuration/VoiceOptions.cs");
        writer.AppendLine("- Merlin.Backend/VoiceScripts/voice_worker.py");
        writer.AppendLine("- Merlin.Backend/Services/SpeechCommandNormalizer.cs");
        writer.AppendLine("- Merlin.Backend.Tests/SpeechCommandNormalizerTests.cs");
        writer.AppendLine();
        writer.AppendLine("## What Not To Change");
        writer.AppendLine();
        writer.AppendLine("- Do not replace the existing STT implementation based on one session.");
        writer.AppendLine("- Do not route DeepInfra, memory, TTS, or database changes through this investigation.");
        writer.AppendLine("- Do not silently apply aggressive normalizations to production commands.");
        return writer.ToString();
    }

    private static string BuildChatGptPacket(TestSessionResult session)
    {
        var worst = session.Attempts.OrderByDescending(a => a.Evaluation.WordErrorRate + a.Evaluation.CharacterErrorRate).Take(10);
        var writer = new StringBuilder();
        writer.AppendLine("# ChatGPT Review Packet");
        writer.AppendLine();
        writer.AppendLine($"Session `{session.SessionId}` tested {session.Phrases.Count} phrases with {session.Attempts.Count} attempts.");
        writer.AppendLine($"STT config: model={session.Config.Model}, device={session.Config.DeviceType}, compute={session.Config.ComputeType}, beam={session.Config.BeamSize}, language={session.Config.Language}, initial_prompt length={session.Config.InitialPrompt.Length}.");
        writer.AppendLine($"Reports: `{session.ReportDirectory}`");
        writer.AppendLine($"Recordings: `{session.RecordingDirectory}`");
        writer.AppendLine();
        writer.AppendLine("## Top 10 Worst Phrases");
        writer.AppendLine();
        writer.AppendLine("| Phrase | Expected | Actual | Rating | WER | Confusions |");
        writer.AppendLine("|---|---|---|---|---:|---|");
        foreach (var attempt in worst)
        {
            writer.AppendLine($"| {EscapeMd(attempt.PhraseId)} | {EscapeMd(attempt.ExpectedText)} | {EscapeMd(attempt.ActualTranscript)} | {EscapeMd(attempt.UserRating)} | {attempt.Evaluation.WordErrorRate:N2} | {EscapeMd(string.Join("; ", attempt.Evaluation.SuspectedConfusionPairs))} |");
        }

        writer.AppendLine();
        writer.AppendLine("## Suspected Causes");
        writer.AppendLine();
        foreach (var cause in session.Attempts.SelectMany(a => a.Evaluation.SuspectedConfusionPairs.Concat(AudioFlags(a))).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(10))
        {
            writer.AppendLine($"- {cause.Key}: {cause.Count()}");
        }

        writer.AppendLine();
        writer.AppendLine("## Questions To Ask Next");
        writer.AppendLine();
        writer.AppendLine("- Are substitutions clustered around known Merlin vocabulary despite healthy audio levels?");
        writer.AppendLine("- Do failed phrases show clipped starts or endings?");
        writer.AppendLine("- Would a narrower initial_prompt reduce these terms without creating false corrections?");
        writer.AppendLine("- Which normalizer suggestions are safe enough to become test-covered production suggestions?");
        return writer.ToString();
    }

    private static IEnumerable<string> AudioFlags(TestAttempt attempt)
    {
        if (attempt.AudioDiagnostics.SignalTooQuiet) yield return "too quiet";
        if (attempt.AudioDiagnostics.PossibleClipping) yield return "possible clipping";
        if (attempt.AudioDiagnostics.ClippingDetected) yield return "clipping detected";
        if (attempt.AudioDiagnostics.SilenceBeforeSpeechMs > 1200) yield return "long silence/noise";
        if (attempt.AudioDiagnostics.SilenceAfterSpeechMs < 150) yield return "clipped ending";
    }

    private static string FixArea(string key)
    {
        if (key.Contains("quiet", StringComparison.OrdinalIgnoreCase)
            || key.Contains("clipping", StringComparison.OrdinalIgnoreCase))
        {
            return "microphone/audio";
        }

        if (key.Contains("clipped", StringComparison.OrdinalIgnoreCase)
            || key.Contains("silence", StringComparison.OrdinalIgnoreCase))
        {
            return "audio/VAD";
        }

        if (key.Contains("variants", StringComparison.OrdinalIgnoreCase)
            || key.Contains("beam", StringComparison.OrdinalIgnoreCase))
        {
            return "initial_prompt or transcript normalizer";
        }

        return "model/beam settings";
    }

    private static TestPhrase? FindPhrase(TestSessionResult session, string phraseId)
    {
        return session.Phrases.FirstOrDefault(p => p.Id == phraseId);
    }

    private static double Average(IEnumerable<double> values)
    {
        var list = values.Where(v => !double.IsNaN(v)).ToList();
        return list.Count == 0 ? 0 : list.Average();
    }

    private static string EscapeMd(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}

public static class CsvWriterExtensions
{
    public static void AppendCsvLine(this StringBuilder writer, params object?[] values)
    {
        writer.AppendLine(string.Join(",", values.Select(Escape)));
    }

    private static string Escape(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
