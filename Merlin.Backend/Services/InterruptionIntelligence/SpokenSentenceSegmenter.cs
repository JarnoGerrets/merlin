using System.Text.RegularExpressions;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed partial class SpokenSentenceSegmenter
{
    public SpokenSentenceSegmentation Segment(string? text)
    {
        var normalized = NormalizeSpacing(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new SpokenSentenceSegmentation([], string.Empty);
        }

        var completed = new List<string>();
        var currentStart = 0;
        foreach (Match match in SentenceTerminatorRegex().Matches(normalized))
        {
            var sentence = normalized[currentStart..match.Index].Trim();
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                completed.Add($"{sentence}{match.Value}");
            }

            currentStart = match.Index + match.Length;
        }

        var partial = currentStart >= normalized.Length
            ? string.Empty
            : normalized[currentStart..].Trim();
        return new SpokenSentenceSegmentation(completed, partial);
    }

    public static string NormalizeSpacing(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(text.Trim(), " ");
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("[.!?]")]
    private static partial Regex SentenceTerminatorRegex();
}

public sealed record SpokenSentenceSegmentation(
    IReadOnlyList<string> CompletedSentences,
    string CurrentPartialSentence)
{
    public string LastCompletedSentence => CompletedSentences.Count == 0
        ? string.Empty
        : CompletedSentences[^1];
}
