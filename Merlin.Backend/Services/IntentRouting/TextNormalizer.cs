using System.Text.RegularExpressions;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class TextNormalizer
{
    private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex PunctuationPattern = new(@"[?!.,;:""]+", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> Contractions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["what's"] = "what is",
            ["whats"] = "what is",
            ["we're"] = "we are",
            ["i'm"] = "i am",
            ["im"] = "i am",
            ["you're"] = "you are",
            ["cant"] = "cannot",
            ["can't"] = "cannot",
            ["dont"] = "do not",
            ["don't"] = "do not"
        };

    public NormalizedInput Normalize(string? text)
    {
        var original = text ?? string.Empty;
        var normalized = original.Trim().ToLowerInvariant();

        foreach (var contraction in Contractions)
        {
            normalized = ReplaceWholePhrase(normalized, contraction.Key, contraction.Value);
        }

        normalized = normalized
            .Replace("time-zone", "timezone", StringComparison.OrdinalIgnoreCase)
            .Replace("time zone", "timezone", StringComparison.OrdinalIgnoreCase);

        normalized = ReplaceWholePhrase(normalized, "tz", "timezone");
        normalized = PunctuationPattern.Replace(normalized, " ");
        normalized = WhitespacePattern.Replace(normalized, " ").Trim();

        return new NormalizedInput(original, normalized);
    }

    private static string ReplaceWholePhrase(string value, string phrase, string replacement)
    {
        return Regex.Replace(
            value,
            $@"(?<![a-z0-9]){Regex.Escape(phrase)}(?![a-z0-9])",
            replacement,
            RegexOptions.IgnoreCase);
    }
}
