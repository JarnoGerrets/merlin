using System.Text.RegularExpressions;

namespace Merlin.Backend.Core.Memory.Search;

public static class ProjectIdentifierNormalizer
{
    private static readonly Regex IdentifierRegex = new(
        @"\b(?<prefix>pr|p|v)[\s-]*(?<number>\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NormalizedIdentifierRegex = new(
        @"^(?<prefix>pr|p|v)(?<number>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return IdentifierRegex.Replace(text, match =>
            $"{match.Groups["prefix"].Value.ToLowerInvariant()}{match.Groups["number"].Value}");
    }

    public static IReadOnlyList<string> ExtractIdentifiers(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return IdentifierRegex.Matches(text)
            .Select(match => $"{match.Groups["prefix"].Value.ToLowerInvariant()}{match.Groups["number"].Value}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsCompactIdentifier(string term) =>
        !string.IsNullOrWhiteSpace(term) && NormalizedIdentifierRegex.IsMatch(term.Trim());

    public static IReadOnlyList<string> SearchVariants(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var normalized = NormalizeText(term).Trim().ToLowerInvariant();
        var identifiers = ExtractIdentifiers(normalized);
        if (identifiers.Count > 0 && !identifiers.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return identifiers
                .SelectMany(SearchVariants)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var match = NormalizedIdentifierRegex.Match(normalized);
        if (!match.Success)
        {
            return [term];
        }

        var prefix = match.Groups["prefix"].Value;
        var number = match.Groups["number"].Value;
        return
        [
            $"{prefix}{number}",
            $"{prefix} {number}",
            $"{prefix}-{number}"
        ];
    }
}
