using System.Text.RegularExpressions;
using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services.BargeIn;

public sealed partial class InterruptionClassifier : IInterruptionClassifier
{
    private static readonly string[] Backchannels = ["yes", "yeah", "yep", "mhm", "mm hmm", "okay", "ok", "right", "uh huh", "makes sense"];
    private static readonly string[] PoliteHardStopPrefixes =
    [
        "please ",
        "can you please ",
        "could you please ",
        "would you please ",
        "can you ",
        "could you ",
        "would you ",
        "hey ",
        "uh ",
        "um ",
        "no ",
        "nope "
    ];

    public InterruptionClassificationResult Classify(InterruptionClassificationInput input, BargeInOptions options)
    {
        var normalized = Normalize(input.NormalizedTranscript);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Result(InterruptionType.None, 0.0, "Empty gated STT transcript.");
        }

        var withoutWakeWord = RemoveWakeWord(normalized, options);
        var hardStopCandidate = NormalizeHardStopCandidate(withoutWakeWord);
        if (CanUseNaturalHardStop(input)
            && MatchesHardStop(hardStopCandidate, options.HardStopPhrases))
        {
            return Result(InterruptionType.HardStop, 0.95, "Matched natural hard-stop phrase while assistant was speaking.");
        }

        if (RequiresWakeWord(input, options))
        {
            return Result(InterruptionType.NoiseOrEcho, 0.2, "Wake word was required but not present.");
        }

        if (IsBackchannel(withoutWakeWord))
        {
            return Result(InterruptionType.Backchannel, 0.9, "Backchannel phrase should not cancel assistant speech.");
        }

        if (MatchesHardStop(hardStopCandidate, options.HardStopPhrases))
        {
            return Result(InterruptionType.HardStop, 0.95, "Matched hard-stop phrase.");
        }

        if (MatchesConfiguredPhrase(withoutWakeWord, options.PausePhrases, exactShortPhrase: true))
        {
            return Result(InterruptionType.Pause, 0.86, "Matched pause phrase.");
        }

        var correction = TryExtractCorrection(withoutWakeWord, options.CorrectionPhrases);
        if (correction is not null)
        {
            return new InterruptionClassificationResult
            {
                Type = InterruptionType.Correction,
                Confidence = 0.9,
                Reason = "Matched correction phrase.",
                CorrectedUserMessage = correction
            };
        }

        if (LooksLikeClarification(withoutWakeWord, options.ClarificationQuestionPrefixes))
        {
            return Result(InterruptionType.ClarificationQuestion, 0.82, "Matched clarification question.");
        }

        if (LooksLikeSideComment(withoutWakeWord))
        {
            return Result(InterruptionType.SideComment, Math.Min(Math.Max(input.VadConfidence, 0.7), 0.85), "Transcript treated as side comment.");
        }

        return Result(InterruptionType.None, Math.Min(input.VadConfidence, 0.5), "Transcript did not match interruption classes.");
    }

    public static string Normalize(string value)
    {
        var lower = value.ToLowerInvariant().Replace('’', '\'');
        lower = NonWordOrSpaceRegex().Replace(lower, " ");
        lower = WhitespaceRegex().Replace(lower, " ").Trim();
        return lower;
    }

    private static bool RequiresWakeWord(InterruptionClassificationInput input, BargeInOptions options)
    {
        var naturalSoftAllowed = options.AllowNaturalSoftBargeInWhenAecVerified && !input.IsAecDegraded;
        return (input.IsAecDegraded || (options.RequireWakeWordForFirstVersion && !naturalSoftAllowed)) && !input.WasWakeWordPresent;
    }

    private static bool IsBackchannel(string normalized)
    {
        return Backchannels.Any(backchannel => string.Equals(normalized, backchannel, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesConfiguredPhrase(string normalized, IEnumerable<string> phrases, bool exactShortPhrase)
    {
        foreach (var phrase in phrases.Select(Normalize).Where(static phrase => phrase.Length > 0))
        {
            if (string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!exactShortPhrase && normalized.StartsWith($"{phrase} ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesHardStop(string normalized, IEnumerable<string> phrases)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (MatchesConfiguredPhrase(normalized, phrases, exactShortPhrase: true))
        {
            return true;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 4
            && (words.Contains("stop", StringComparer.OrdinalIgnoreCase)
                || words.Contains("abort", StringComparer.OrdinalIgnoreCase)
                || words.Contains("cancel", StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeHardStopCandidate(string normalized)
    {
        var candidate = normalized.Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in PoliteHardStopPrefixes)
            {
                if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                candidate = candidate[prefix.Length..].Trim();
                changed = true;
                break;
            }
        }

        foreach (var suffix in new[] { " please", " thanks", " thank you" })
        {
            if (candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate[..^suffix.Length].Trim();
                break;
            }
        }

        return candidate;
    }

    private static bool CanUseNaturalHardStop(InterruptionClassificationInput input)
    {
        return !string.IsNullOrWhiteSpace(input.CurrentSpeechType)
            && !string.Equals(input.CurrentSpeechType, "Idle", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(input.CurrentSpeechType, "None", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractCorrection(string normalized, IEnumerable<string> phrases)
    {
        foreach (var phrase in phrases.Select(Normalize).OrderByDescending(static phrase => phrase.Length))
        {
            if (string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            if (normalized.StartsWith($"{phrase} ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }

        return null;
    }

    private static bool LooksLikeClarification(string normalized, IEnumerable<string> prefixes)
    {
        if (normalized.Contains("what do you mean", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("what does that mean", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return prefixes
            .Select(Normalize)
            .Any(prefix => string.Equals(normalized, prefix, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith($"{prefix} ", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeSideComment(string normalized)
    {
        if (normalized.Length < 3)
        {
            return false;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 8)
        {
            return false;
        }

        return true;
    }

    private static string RemoveWakeWord(string normalized, BargeInOptions options)
    {
        foreach (var wakeWord in options.WakeWords.Select(Normalize))
        {
            if (string.Equals(normalized, wakeWord, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (normalized.StartsWith($"{wakeWord} ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized[wakeWord.Length..].Trim();
            }
        }

        return normalized;
    }

    private static InterruptionClassificationResult Result(InterruptionType type, double confidence, string reason)
    {
        return new InterruptionClassificationResult
        {
            Type = type,
            Confidence = confidence,
            Reason = reason
        };
    }

    [GeneratedRegex("[^a-z0-9' ]+")]
    private static partial Regex NonWordOrSpaceRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
