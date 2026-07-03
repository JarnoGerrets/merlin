using System.Text.RegularExpressions;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.StreamingResponses;

public interface ISpeakableTextSanitizer
{
    string Sanitize(string text, SpeakableTextSanitizationContext context);
}

public sealed record SpeakableTextSanitizationContext(
    bool IsCodeExplanationExpected = false,
    bool PreserveTechnicalSymbols = false,
    bool IsFirstSegment = false);

public sealed partial class SpeakableTextSanitizer : ISpeakableTextSanitizer
{
    private readonly StreamingResponseOptions _options;

    public SpeakableTextSanitizer(IOptions<StreamingResponseOptions> options)
        : this(options.Value)
    {
    }

    public SpeakableTextSanitizer(StreamingResponseOptions options)
    {
        _options = options;
    }

    public string Sanitize(string text, SpeakableTextSanitizationContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var value = text.Replace("\r\n", "\n").Replace('\r', '\n');

        if (_options.SkipCodeBlocksInSpeech && !context.IsCodeExplanationExpected)
        {
            value = CodeFenceRegex().Replace(value, " There is a code example here. ");
        }

        if (LooksLikeJson(value))
        {
            return "I found structured data.";
        }

        value = HeadingRegex().Replace(value, "${heading}.");
        value = BulletRegex().Replace(value, "${item}.");
        value = NumberedListRegex().Replace(value, "${item}");
        value = LinkRegex().Replace(value, "${title}");
        value = BoldRegex().Replace(value, "${content}");
        value = ItalicRegex().Replace(value, "${content}");
        value = InlineCodeRegex().Replace(value, "${code}");
        value = TableDividerRegex().Replace(value, " ");
        value = value.Replace("|", " ");
        value = WhitespaceRegex().Replace(value, " ");
        value = SpaceBeforePunctuationRegex().Replace(value, "${punctuation}");
        return value.Trim();
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.Trim();
        return (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            || (trimmed.StartsWith('[') && trimmed.EndsWith(']') && trimmed.Contains(':', StringComparison.Ordinal));
    }

    [GeneratedRegex(@"```[\s\S]*?```", RegexOptions.Compiled)]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"^\s{0,3}#{1,6}\s+(?<heading>.+?)\s*#*\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*[-*+]\s+(?<item>.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^\s*\d+[.)]\s+(?<item>.+)$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex NumberedListRegex();

    [GeneratedRegex(@"\[(?<title>[^\]]+)\]\((?<url>[^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"`(?<code>[^`]+)`", RegexOptions.Compiled)]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"\*\*(?<content>.+?)\*\*", RegexOptions.Compiled)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?<content>[^*\n]+)\*(?!\*)", RegexOptions.Compiled)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex TableDividerRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\s+(?<punctuation>[.,;:?!])", RegexOptions.Compiled)]
    private static partial Regex SpaceBeforePunctuationRegex();
}
