using System.Text.RegularExpressions;
using Merlin.VoiceTest.Models;

namespace Merlin.VoiceTest.Services;

public sealed class PhraseEvaluator
{
    private static readonly (string Expected, string Actual, string Label)[] Confusions =
    [
        ("beam", "bean", "beam vs bean"),
        ("sqlite", "sequel light", "SQLite variants"),
        ("sqlite", "sql lite", "SQLite variants"),
        ("deepinfra", "deep infra", "DeepInfra variants"),
        ("chatterbox", "chatter box", "Chatterbox variants"),
        ("cuda", "cooda", "CUDA variants"),
        ("codex cli", "code x c l i", "Codex CLI variants"),
        ("appdata", "app data", "AppData variants")
    ];

    public PhraseEvaluation Evaluate(TestPhrase phrase, string actualTranscript)
    {
        var expectedCandidates = new[] { phrase.ExpectedText }.Concat(phrase.AcceptableAlternatives);
        var normalizedActual = Normalize(actualTranscript);
        var normalizedExpected = Normalize(phrase.ExpectedText);
        var exact = expectedCandidates.Select(Normalize).Any(candidate => candidate == normalizedActual);
        var missing = phrase.ImportantTerms
            .Where(term => !ContainsTerm(normalizedActual, NormalizeTechnicalTerm(term)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var suspected = DetectConfusions(normalizedExpected, normalizedActual);

        return new PhraseEvaluation
        {
            ExactMatchAfterNormalization = exact,
            WordErrorRate = CalculateWordErrorRate(normalizedExpected, normalizedActual),
            CharacterErrorRate = CalculateCharacterErrorRate(normalizedExpected, normalizedActual),
            MissingImportantTerms = missing,
            SubstitutedImportantTerms = suspected.Select(s => s.Split(":", 2)[0]).Distinct().ToList(),
            SuspectedConfusionPairs = suspected,
            TranscriptLength = actualTranscript.Length,
            NormalizedExpected = normalizedExpected,
            NormalizedActual = normalizedActual
        };
    }

    public static string Normalize(string value)
    {
        var text = value.ToLowerInvariant();
        text = Regex.Replace(text, "\\bmedium\\s+dot\\s+e\\s*n\\b", "medium.en");
        text = Regex.Replace(text, "\\bv\\s+three\\b", "v3");
        text = Regex.Replace(text, "\\bbeam\\s+five\\b", "beam 5");
        text = Regex.Replace(text, "\\bfive\\b", "5");
        text = Regex.Replace(text, "[^a-z0-9.]+", " ");
        return Regex.Replace(text, "\\s+", " ").Trim();
    }

    private static string NormalizeTechnicalTerm(string value)
    {
        return Normalize(value).Replace(" ", "", StringComparison.Ordinal);
    }

    private static bool ContainsTerm(string normalizedActual, string normalizedTerm)
    {
        var compactActual = normalizedActual.Replace(" ", "", StringComparison.Ordinal);
        return compactActual.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase)
            || normalizedActual.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> DetectConfusions(string normalizedExpected, string normalizedActual)
    {
        var results = new List<string>();
        foreach (var (expected, actual, label) in Confusions)
        {
            if (normalizedExpected.Contains(expected, StringComparison.OrdinalIgnoreCase)
                && normalizedActual.Contains(actual, StringComparison.OrdinalIgnoreCase))
            {
                results.Add($"{label}: expected {expected}, got {actual}");
            }
        }

        if (normalizedActual.Length > 0 && normalizedExpected.Length > 0)
        {
            var expectedFirst = normalizedExpected.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(expectedFirst) && !normalizedActual.StartsWith(expectedFirst, StringComparison.Ordinal))
            {
                results.Add("clipped first word");
            }
        }

        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static double CalculateWordErrorRate(string expected, string actual)
    {
        var expectedWords = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var actualWords = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return expectedWords.Length == 0 ? 0 : Levenshtein(expectedWords, actualWords) / (double)expectedWords.Length;
    }

    private static double CalculateCharacterErrorRate(string expected, string actual)
    {
        return expected.Length == 0 ? 0 : Levenshtein(expected.ToCharArray(), actual.ToCharArray()) / (double)expected.Length;
    }

    private static int Levenshtein<T>(IReadOnlyList<T> source, IReadOnlyList<T> target)
        where T : notnull
    {
        var costs = new int[target.Count + 1];
        for (var j = 0; j <= target.Count; j++)
        {
            costs[j] = j;
        }

        for (var i = 1; i <= source.Count; i++)
        {
            var previous = costs[0];
            costs[0] = i;
            for (var j = 1; j <= target.Count; j++)
            {
                var old = costs[j];
                var cost = EqualityComparer<T>.Default.Equals(source[i - 1], target[j - 1]) ? 0 : 1;
                costs[j] = Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), previous + cost);
                previous = old;
            }
        }

        return costs[target.Count];
    }
}
