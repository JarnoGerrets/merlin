using Merlin.Backend.Configuration;

namespace Merlin.Backend.Services;

internal static class ChatterboxChunkPlanner
{
    private static readonly HashSet<string> CommonAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr",
        "mrs",
        "ms",
        "dr",
        "prof",
        "sr",
        "jr",
        "st",
        "vs",
        "etc",
        "e.g",
        "i.e",
        "a.m",
        "p.m"
    };

    public static IReadOnlyList<string> Plan(string text, TtsOptions options)
    {
        var normalized = string.Join(' ', (text ?? string.Empty).Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        if (!options.ChatterboxEnableInteractiveChunking)
        {
            return SplitWithSingleSize(normalized, Math.Max(80, options.ChatterboxMaxTextCharsPerChunk));
        }

        var firstTarget = ClampPositive(options.ChatterboxFirstChunkTargetChars, 70);
        var firstMax = Math.Max(firstTarget, ClampPositive(options.ChatterboxFirstChunkMaxChars, 120));
        var nextTarget = ClampPositive(options.ChatterboxNextChunkTargetChars, 180);
        var nextMax = Math.Max(nextTarget, ClampPositive(options.ChatterboxNextChunkMaxChars, 260));

        if (normalized.Length <= firstMax)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var remaining = normalized;
        var isFirst = true;
        while (remaining.Length > 0)
        {
            var target = isFirst ? firstTarget : nextTarget;
            var max = isFirst ? firstMax : nextMax;
            if (remaining.Length <= max)
            {
                chunks.Add(remaining.Trim());
                break;
            }

            var minimum = isFirst ? Math.Min(40, target) : Math.Min(90, target);
            var split = FindSplitIndex(remaining, target, max, minimum);
            var chunk = remaining[..split].Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            remaining = remaining[split..].Trim();
            isFirst = false;
        }

        return chunks;
    }

    private static IReadOnlyList<string> SplitWithSingleSize(string text, int maxChars)
    {
        var chunks = new List<string>();
        var remaining = text.Trim();
        while (remaining.Length > maxChars)
        {
            var split = FindSplitIndex(remaining, maxChars, maxChars, Math.Min(80, maxChars));
            chunks.Add(remaining[..split].Trim());
            remaining = remaining[split..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(remaining))
        {
            chunks.Add(remaining);
        }

        return chunks;
    }

    private static int FindSplitIndex(string text, int targetChars, int maxChars, int minimumChars)
    {
        var limit = Math.Min(maxChars, text.Length - 1);
        var target = Math.Min(targetChars, limit);
        var minimum = Math.Min(Math.Max(1, minimumChars), target);

        var sentence = FindBestBoundary(text, target, limit, minimum, IsSentenceBoundary);
        if (sentence > 0)
        {
            return sentence;
        }

        var soft = FindBestBoundary(text, target, limit, minimum, IsSoftBoundary);
        if (soft > 0)
        {
            return soft;
        }

        var space = FindBestBoundary(text, target, limit, minimum, static (value, index) => char.IsWhiteSpace(value[index]));
        return space > 0 ? space : limit;
    }

    private static int FindBestBoundary(string text, int target, int limit, int minimum, Func<string, int, bool> isBoundary)
    {
        var bestAfterTarget = -1;
        for (var index = target; index <= limit; index++)
        {
            if (isBoundary(text, index) && IsSafeSplit(text, index))
            {
                bestAfterTarget = SplitAfterBoundary(text, index);
                break;
            }
        }

        if (bestAfterTarget > 0)
        {
            return bestAfterTarget;
        }

        for (var index = target - 1; index >= minimum; index--)
        {
            if (isBoundary(text, index) && IsSafeSplit(text, index))
            {
                return SplitAfterBoundary(text, index);
            }
        }

        return -1;
    }

    private static bool IsSentenceBoundary(string text, int index)
    {
        var value = text[index];
        if (value is not ('.' or '!' or '?'))
        {
            return false;
        }

        if (value == '.' && IsDecimalPoint(text, index))
        {
            return false;
        }

        if (value == '.' && IsAbbreviation(text, index))
        {
            return false;
        }

        return index == text.Length - 1 || char.IsWhiteSpace(text[index + 1]);
    }

    private static bool IsSoftBoundary(string text, int index)
    {
        var value = text[index];
        if (value is not (',' or ';' or ':'))
        {
            return false;
        }

        if (value == ':' && index > 0 && index + 2 < text.Length && text[index + 1] == '/' && text[index + 2] == '/')
        {
            return false;
        }

        return index == text.Length - 1 || char.IsWhiteSpace(text[index + 1]);
    }

    private static int SplitAfterBoundary(string text, int index)
    {
        var split = index + 1;
        while (split < text.Length && char.IsWhiteSpace(text[split]))
        {
            split++;
        }

        return split;
    }

    private static bool IsSafeSplit(string text, int index)
    {
        var tokenStart = index;
        while (tokenStart > 0 && !char.IsWhiteSpace(text[tokenStart - 1]))
        {
            tokenStart--;
        }

        var tokenEnd = index;
        while (tokenEnd + 1 < text.Length && !char.IsWhiteSpace(text[tokenEnd + 1]))
        {
            tokenEnd++;
        }

        var token = text[tokenStart..(tokenEnd + 1)];
        return !LooksLikeUrlOrPath(token);
    }

    private static bool IsDecimalPoint(string text, int index)
    {
        return index > 0 &&
               index + 1 < text.Length &&
               char.IsDigit(text[index - 1]) &&
               char.IsDigit(text[index + 1]);
    }

    private static bool IsAbbreviation(string text, int dotIndex)
    {
        var start = dotIndex;
        while (start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] == '.'))
        {
            start--;
        }

        var token = text[start..dotIndex].Trim('.');
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return CommonAbbreviations.Contains(token) ||
               token.Length <= 3 && token.All(char.IsLetter) && token.Any(char.IsUpper);
    }

    private static bool LooksLikeUrlOrPath(string token)
    {
        return token.Contains("://", StringComparison.Ordinal) ||
               token.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
               token.Contains('\\', StringComparison.Ordinal) ||
               token.Contains('/', StringComparison.Ordinal);
    }

    private static int ClampPositive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }
}
