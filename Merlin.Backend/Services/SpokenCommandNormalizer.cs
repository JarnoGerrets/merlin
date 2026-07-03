namespace Merlin.Backend.Services;

public sealed record SpokenCommandNormalizationResult(
    string OriginalText,
    string CommandText,
    bool HadInvocation,
    bool WasWakeOnly);

public static class SpokenCommandNormalizer
{
    private static readonly HashSet<string> InvocationFillers = new(StringComparer.Ordinal)
    {
        "hey",
        "hi",
        "hello",
        "okay",
        "ok",
        "yo"
    };

    private static readonly string[][] PolitePrefixes =
    [
        ["can", "you", "please"],
        ["could", "you", "please"],
        ["would", "you", "please"],
        ["can", "you"],
        ["could", "you"],
        ["would", "you"],
        ["please"]
    ];

    private static readonly HashSet<string> CommandStarters = new(StringComparer.Ordinal)
    {
        "cancel",
        "close",
        "disable",
        "done",
        "edit",
        "enable",
        "exit",
        "gesture",
        "hide",
        "i",
        "let",
        "open",
        "show",
        "start",
        "stop",
        "what",
        "when",
        "where",
        "who",
        "why",
        "how"
    };

    public static SpokenCommandNormalizationResult Normalize(string? text)
    {
        var originalText = text ?? string.Empty;
        var words = Tokenize(originalText);
        var hadInvocation = StripInvocation(words);
        StripPolitePrefixes(words);
        StripTrailingPlease(words);

        var commandText = string.Join(' ', words);
        return new SpokenCommandNormalizationResult(
            originalText,
            commandText,
            hadInvocation,
            hadInvocation && string.IsNullOrWhiteSpace(commandText));
    }

    private static List<string> Tokenize(string text)
    {
        var normalized = text
            .Trim()
            .ToLowerInvariant();
        normalized = new string(normalized
            .Select(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character == '\''
                ? character
                : ' ')
            .ToArray());
        var tokens = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        return NormalizeFirstPersonContractions(tokens);
    }

    private static List<string> NormalizeFirstPersonContractions(IReadOnlyList<string> tokens)
    {
        var normalized = new List<string>(tokens.Count);
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            if (token is "i'm" or "im")
            {
                normalized.Add("i");
                normalized.Add("am");
                continue;
            }

            if (string.Equals(token, "i", StringComparison.Ordinal)
                && index + 1 < tokens.Count
                && string.Equals(tokens[index + 1], "m", StringComparison.Ordinal))
            {
                normalized.Add("i");
                normalized.Add("am");
                index++;
                continue;
            }

            if (string.Equals(token, "i", StringComparison.Ordinal)
                && index + 2 < tokens.Count
                && string.Equals(tokens[index + 1], "'", StringComparison.Ordinal)
                && string.Equals(tokens[index + 2], "m", StringComparison.Ordinal))
            {
                normalized.Add("i");
                normalized.Add("am");
                index += 2;
                continue;
            }

            normalized.Add(token);
        }

        return normalized;
    }

    private static bool StripInvocation(List<string> words)
    {
        if (words.Count == 0)
        {
            return false;
        }

        if (string.Equals(words[0], "merlin", StringComparison.Ordinal))
        {
            if (!CanStripInvocation(words, 1))
            {
                return false;
            }

            words.RemoveAt(0);
            return true;
        }

        if (words.Count >= 2
            && InvocationFillers.Contains(words[0])
            && string.Equals(words[1], "merlin", StringComparison.Ordinal))
        {
            if (!CanStripInvocation(words, 2))
            {
                return false;
            }

            words.RemoveRange(0, 2);
            return true;
        }

        return false;
    }

    private static bool CanStripInvocation(IReadOnlyList<string> words, int commandStartIndex)
    {
        if (words.Count == commandStartIndex)
        {
            return true;
        }

        if (StartsWithPolitePrefix(words, commandStartIndex))
        {
            return true;
        }

        return CommandStarters.Contains(words[commandStartIndex]);
    }

    private static void StripPolitePrefixes(List<string> words)
    {
        var removedAny = true;
        while (removedAny)
        {
            removedAny = false;
            foreach (var prefix in PolitePrefixes)
            {
                if (!StartsWith(words, prefix))
                {
                    continue;
                }

                words.RemoveRange(0, prefix.Length);
                removedAny = true;
                break;
            }
        }
    }

    private static void StripTrailingPlease(List<string> words)
    {
        while (words.Count > 0 && string.Equals(words[^1], "please", StringComparison.Ordinal))
        {
            words.RemoveAt(words.Count - 1);
        }
    }

    private static bool StartsWithPolitePrefix(IReadOnlyList<string> words, int startIndex)
    {
        return PolitePrefixes.Any(prefix => StartsWith(words, prefix, startIndex));
    }

    private static bool StartsWith(IReadOnlyList<string> words, IReadOnlyList<string> prefix, int startIndex = 0)
    {
        if (words.Count - startIndex < prefix.Count)
        {
            return false;
        }

        for (var index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(words[startIndex + index], prefix[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
