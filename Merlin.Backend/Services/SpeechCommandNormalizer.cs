using System.Text.RegularExpressions;

namespace Merlin.Backend.Services;

public sealed partial class SpeechCommandNormalizer
{
    private static readonly HashSet<string> KnownTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        "com",
        "net",
        "org",
        "nl",
        "be",
        "de",
        "fr",
        "uk",
        "co",
        "io",
        "ai",
        "dev",
        "app",
        "gg",
        "tv",
        "me",
        "edu",
        "gov"
    };

    public string Normalize(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return string.Empty;
        }

        var normalized = transcript.Trim();
        normalized = NormalizeCommonCommandTerms(normalized);
        normalized = NormalizeSpokenDomainTokens(normalized);
        normalized = NormalizeWhitespace(normalized);
        normalized = NormalizePunctuationSpacing(normalized);
        return normalized;
    }

    private static string NormalizeCommonCommandTerms(string value)
    {
        var normalized = value;
        foreach (var replacement in new (string Pattern, string Replacement)[]
        {
            (@"\bv\s*s\s*code\b", "vscode"),
            (@"\bvisual studio code\b", "vscode"),
            (@"\bpower shell\b", "powershell"),
            (@"\bc sharp\b", "csharp"),
            (@"\bg p t\b", "gpt"),
            (@"\bmerlyn\b", "merlin")
        })
        {
            normalized = Regex.Replace(
                normalized,
                replacement.Pattern,
                replacement.Replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return normalized;
    }

    private static string NormalizeSpokenDomainTokens(string value)
    {
        var tokens = TokenRegex()
            .Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .ToList();
        if (tokens.Count == 0)
        {
            return value;
        }

        var output = new List<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (TryReadDomain(tokens, index, out var domain, out var consumed))
            {
                output.Add(domain);
                index += consumed - 1;
                continue;
            }

            output.Add(tokens[index]);
        }

        return string.Join(' ', output);
    }

    private static bool TryReadDomain(
        IReadOnlyList<string> tokens,
        int start,
        out string domain,
        out int consumed)
    {
        domain = string.Empty;
        consumed = 0;

        if (start + 2 >= tokens.Count || !IsDomainLabel(tokens[start]) || !IsDotToken(tokens[start + 1]))
        {
            return false;
        }

        var labels = new List<string> { tokens[start] };
        var index = start + 1;
        while (index + 1 < tokens.Count && IsDotToken(tokens[index]))
        {
            if (!TryReadSpokenDomainLabel(tokens, index + 1, out var label, out var labelConsumed))
            {
                break;
            }

            labels.Add(label);
            index += 1 + labelConsumed;
        }

        if (labels.Count < 2 || !KnownTlds.Contains(labels[^1]))
        {
            return false;
        }

        domain = string.Join('.', labels);
        consumed = index - start;
        return true;
    }

    private static string NormalizeTldToken(string token)
    {
        return token switch
        {
            "commercial" => "com",
            "company" => "com",
            "come" => "com",
            "calm" => "com",
            "network" => "net",
            "organization" => "org",
            "organ" => "org",
            "nederland" => "nl",
            "netherlands" => "nl",
            "andl" => "nl",
            "and l" => "nl",
            "inl" => "nl",
            "in l" => "nl",
            "n l" => "nl",
            "co" => "co",
            "seeo" => "co",
            "seo" => "co",
            "see oh" => "co",
            "c o" => "co",
            "uk" => "uk",
            "okay" => "uk",
            "you kay" => "uk",
            "u k" => "uk",
            _ => token
        };
    }

    private static bool TryReadSpokenDomainLabel(
        IReadOnlyList<string> tokens,
        int start,
        out string label,
        out int consumed)
    {
        label = string.Empty;
        consumed = 0;

        if (start >= tokens.Count)
        {
            return false;
        }

        if (start + 1 < tokens.Count)
        {
            var twoTokenLabel = NormalizeTldToken($"{tokens[start]} {tokens[start + 1]}");
            if (!string.Equals(twoTokenLabel, $"{tokens[start]} {tokens[start + 1]}", StringComparison.Ordinal)
                && IsDomainLabel(twoTokenLabel))
            {
                label = twoTokenLabel;
                consumed = 2;
                return true;
            }
        }

        var oneTokenLabel = NormalizeTldToken(tokens[start]);
        if (!IsDomainLabel(oneTokenLabel))
        {
            return false;
        }

        label = oneTokenLabel;
        consumed = 1;
        return true;
    }

    private static bool IsDotToken(string token)
    {
        return token is "." or "dot" or "point" or "period";
    }

    private static bool IsDomainLabel(string token)
    {
        return token.Length is > 0 and <= 63
            && token.All(character => char.IsLetterOrDigit(character) || character == '-')
            && !token.StartsWith("-", StringComparison.Ordinal)
            && !token.EndsWith("-", StringComparison.Ordinal);
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizePunctuationSpacing(string value)
    {
        return PunctuationSpacingRegex().Replace(value, "$1");
    }

    [GeneratedRegex(@"[a-z0-9-]+|[^\s]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"\s+([.?!,;:])", RegexOptions.CultureInvariant)]
    private static partial Regex PunctuationSpacingRegex();
}
