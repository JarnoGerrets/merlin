using System.Text.RegularExpressions;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public static partial class ConversationalInterruptionTextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text
            .Trim()
            .ToLowerInvariant()
            .Replace('’', '\'')
            .Replace('‘', '\'');

        normalized = WhitespaceRegex().Replace(normalized, " ");
        normalized = TrailingSentencePunctuationRegex().Replace(normalized, string.Empty);
        return normalized.Trim();
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("[.!?]+$")]
    private static partial Regex TrailingSentencePunctuationRegex();
}
