using System.Text.RegularExpressions;

namespace Merlin.Backend.Services;

public interface ITtsTextSanitizer
{
    TtsTextSanitizationResult Sanitize(string text);
}

public sealed record TtsTextSanitizationResult(
    string Text,
    int RawChars,
    int SanitizedChars,
    bool MarkdownRemoved,
    int ListMarkersConverted,
    int CodeBlocksRemoved,
    int UrlsRemoved);

public sealed partial class TtsTextSanitizer : ITtsTextSanitizer
{
    public TtsTextSanitizationResult Sanitize(string text)
    {
        var raw = text ?? string.Empty;
        var working = raw.ReplaceLineEndings("\n");
        var markdownRemoved = false;
        var listMarkersConverted = 0;
        var codeBlocksRemoved = 0;
        var urlsRemoved = 0;

        working = CodeBlockRegex().Replace(working, match =>
        {
            codeBlocksRemoved++;
            markdownRemoved = true;
            return "\n";
        });

        working = MarkdownLinkRegex().Replace(working, match =>
        {
            urlsRemoved++;
            markdownRemoved = true;
            return match.Groups["title"].Value;
        });

        working = RawUrlRegex().Replace(working, match =>
        {
            urlsRemoved++;
            return string.Empty;
        });

        working = InlineCodeRegex().Replace(working, match =>
        {
            markdownRemoved = true;
            return match.Groups["code"].Value;
        });

        working = BoldRegex().Replace(working, match =>
        {
            markdownRemoved = true;
            return match.Groups["content"].Value;
        });

        working = ItalicRegex().Replace(working, match =>
        {
            markdownRemoved = true;
            return match.Groups["content"].Value;
        });

        var sanitizedLines = new List<string>();
        foreach (var rawLine in working.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                markdownRemoved = true;
                line = heading.Groups["heading"].Value.Trim();
            }

            var bullet = BulletRegex().Match(line);
            if (bullet.Success)
            {
                listMarkersConverted++;
                markdownRemoved = true;
                line = bullet.Groups["item"].Value.Trim();
            }
            else
            {
                var numbered = NumberedLineRegex().Match(line);
                if (numbered.Success)
                {
                    listMarkersConverted++;
                    markdownRemoved = true;
                    line = $"{OrdinalWord(numbered.Groups["number"].Value)}, {LowercaseFirst(numbered.Groups["item"].Value.Trim())}";
                }
            }

            sanitizedLines.Add(EnsureSentence(line));
        }

        working = string.Join(' ', sanitizedLines);

        working = HeadingParentheticalRegex().Replace(working, match =>
        {
            var title = match.Groups["title"].Value.Trim();
            var detail = LowercaseFirst(match.Groups["detail"].Value.Trim());
            return $"{title}: {detail}.";
        });

        working = InlineMalformedNumberedListRegex().Replace(working, match =>
        {
            listMarkersConverted++;
            markdownRemoved = true;
            return $"{match.Groups["prefix"].Value}{OrdinalWord(match.Groups["number"].Value)}, ";
        });

        working = MalformedListMarkerRegex().Replace(working, match =>
        {
            listMarkersConverted++;
            markdownRemoved = true;
            return match.Groups["prefix"].Value;
        });

        working = InlineNumberedListRegex().Replace(working, match =>
        {
            listMarkersConverted++;
            markdownRemoved = true;
            return $"{match.Groups["prefix"].Value}{OrdinalWord(match.Groups["number"].Value)}, ";
        });

        working = InlineBulletListRegex().Replace(working, match =>
        {
            listMarkersConverted++;
            markdownRemoved = true;
            return match.Groups["prefix"].Value;
        });

        working = NormalizeAbbreviations(working);
        working = LowercaseOrdinalItems(working);
        working = CleanupPunctuation(working);

        var textResult = string.IsNullOrWhiteSpace(working)
            ? string.Empty
            : working.Trim();

        return new TtsTextSanitizationResult(
            textResult,
            raw.Length,
            textResult.Length,
            markdownRemoved,
            listMarkersConverted,
            codeBlocksRemoved,
            urlsRemoved);
    }

    private static string NormalizeAbbreviations(string text)
    {
        var result = Regex.Replace(text, @"\bEVs\b", "electric vehicles", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bEV\b", "electric vehicle", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bNL\b", "Netherlands", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bkm\b", "kilometers", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bkWh\b", "kilowatt hours", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\bCO2\b", "C O 2", RegexOptions.IgnoreCase);
        return result;
    }

    private static string CleanupPunctuation(string text)
    {
        var result = Regex.Replace(text, @"\s+", " ").Trim();
        result = Regex.Replace(result, @"\s*—\s*", ", ");
        result = Regex.Replace(result, @"\s+([,.;:!?])", "$1");
        result = Regex.Replace(result, @",\s+([,.;:!?])", "$1");
        result = Regex.Replace(result, @":\s+(?=[A-Z])", ". ");
        result = Regex.Replace(result, @":\s*(?=[.!?])", ".");
        result = Regex.Replace(result, @"\s*:\s*$", ".");
        result = Regex.Replace(result, @"\(\s*\)", string.Empty);
        result = Regex.Replace(result, @"\s+([)])", "$1");
        result = Regex.Replace(result, @"([(])\s+", "$1");
        result = Regex.Replace(result, @"([.!?]){2,}", "$1");
        result = Regex.Replace(result, @"\s+", " ").Trim();
        return TrimUnsafeEdges(result);
    }

    private static string EnsureSentence(string text)
    {
        var value = text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return ".!?;:".Contains(value[^1]) ? value : $"{value}.";
    }

    private static string TrimUnsafeEdges(string text)
    {
        var value = text.Trim();
        var unsafeStart = new HashSet<char> { '*', '-', '#', ':', ';', ')', '.' };
        var unsafeEnd = new HashSet<char> { '*', '-', '#', ':', ';', '(' };

        while (value.Length > 0 && unsafeStart.Contains(value[0]))
        {
            value = value[1..].TrimStart();
        }

        while (value.Length > 0 && unsafeEnd.Contains(value[^1]))
        {
            value = value[..^1].TrimEnd();
        }

        return value;
    }

    private static string LowercaseFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !char.IsLetter(value[0]) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string LowercaseOrdinalItems(string text) =>
        Regex.Replace(
            text,
            @"\b(First|Second|Third|Fourth|Fifth|Sixth|Seventh|Eighth|Ninth|Tenth),\s+([A-Z])",
            match => $"{match.Groups[1].Value}, {char.ToLowerInvariant(match.Groups[2].Value[0])}");

    private static string OrdinalWord(string value) =>
        int.TryParse(value, out var number)
            ? number switch
            {
                1 => "First",
                2 => "Second",
                3 => "Third",
                4 => "Fourth",
                5 => "Fifth",
                6 => "Sixth",
                7 => "Seventh",
                8 => "Eighth",
                9 => "Ninth",
                10 => "Tenth",
                _ => $"Item {number}"
            }
            : "Item";

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"\[(?<title>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"https?://\S+|www\.\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RawUrlRegex();

    [GeneratedRegex(@"`(?<code>[^`]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(?<content>.+?)\*\*", RegexOptions.Compiled)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?<content>[^*\n]+)\*(?!\*)", RegexOptions.Compiled)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"^#{1,6}\s+(?<heading>.+)$", RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[-*]\s+(?<item>.+)$", RegexOptions.Compiled)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^(?<number>\d+)[.)]\s+(?<item>.+)$", RegexOptions.Compiled)]
    private static partial Regex NumberedLineRegex();

    [GeneratedRegex(@"\b(?<title>Better|Worse|Pros|Cons|Advantages|Disadvantages)\s*\((?<detail>[^)]{1,120})\)\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex HeadingParentheticalRegex();

    [GeneratedRegex(@"(?<prefix>^|[\s;:])(?<number>\d+)[.)]\s+", RegexOptions.Compiled)]
    private static partial Regex InlineNumberedListRegex();

    [GeneratedRegex(@"(?<prefix>^|[\s;:])(?<number>\d+)\.\.\s+(?=\S)", RegexOptions.Compiled)]
    private static partial Regex InlineMalformedNumberedListRegex();

    [GeneratedRegex(@"(?<prefix>^|[\s;:])(?<number>\d+)\.\.(?=\s|$)", RegexOptions.Compiled)]
    private static partial Regex MalformedListMarkerRegex();

    [GeneratedRegex(@"(?<prefix>^|[\s;:])[-*]\s+(?=\S)", RegexOptions.Compiled)]
    private static partial Regex InlineBulletListRegex();
}
