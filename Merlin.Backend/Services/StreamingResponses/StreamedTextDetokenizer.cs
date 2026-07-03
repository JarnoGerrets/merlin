using System.Text.RegularExpressions;

namespace Merlin.Backend.Services.StreamingResponses;

public interface IStreamedTextDetokenizer
{
    StreamedTextDetokenizationResult Detokenize(string text);
}

public sealed record StreamedTextDetokenizationResult(
    string Text,
    int RepairCount);

public sealed partial class StreamedTextDetokenizer : IStreamedTextDetokenizer
{
    private static readonly HashSet<string> KnownSuffixSplitStems = new(StringComparer.Ordinal)
    {
        "ask", "build", "creat", "cult", "develop", "learn", "mean", "st", "stream", "t"
    };

    private static readonly HashSet<string> CommonSuffixes = new(StringComparer.Ordinal)
    {
        "ed", "er", "ers", "ing", "ly", "ment", "s", "tion"
    };

    private readonly ILogger<StreamedTextDetokenizer>? _logger;

    public StreamedTextDetokenizer(ILogger<StreamedTextDetokenizer>? logger = null)
    {
        _logger = logger;
    }

    public StreamedTextDetokenizationResult Detokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new StreamedTextDetokenizationResult(string.Empty, 0);
        }

        var value = text;
        var repairCount = 0;
        (value, var repairs) = ReplaceAndCount(ContractionSpacingRegex(), value, "$1$2", "contraction_spacing");
        repairCount += repairs;
        (value, repairs) = ReplaceAndCount(SpaceBeforePunctuationRegex(), value, "${punctuation}", "punctuation_spacing");
        repairCount += repairs;
        (value, repairs) = ReplaceAndCount(SpaceAfterOpeningParenRegex(), value, "(", "opening_parenthesis_spacing");
        repairCount += repairs;
        (value, repairs) = ReplaceAndCount(SpaceBeforeClosingParenRegex(), value, ")", "closing_parenthesis_spacing");
        repairCount += repairs;
        (value, repairs) = ReplaceAndCount(LetterHyphenSpacingRegex(), value, "$1-$2", "hyphen_spacing");
        repairCount += repairs;
        value = SuffixSplitRegex().Replace(value, match =>
        {
            var left = match.Groups["left"].Value;
            var suffix = match.Groups["suffix"].Value;
            if (!ShouldRepairSuffixSplit(left, suffix))
            {
                return match.Value;
            }

            var repaired = left + suffix;
            repairCount++;
            _logger?.LogInformation(
                "StreamingSplitTokenArtifactRepaired. Before: {Before}. After: {After}. Rule: suffix_split_{Suffix}.",
                match.Value,
                repaired,
                suffix);
            return repaired;
        });

        if (repairCount > 0)
        {
            _logger?.LogDebug(
                "StreamedTextDetokenized. RawPreview: {RawPreview}. FixedPreview: {FixedPreview}. RepairCount: {RepairCount}.",
                Preview(text),
                Preview(value),
                repairCount);
        }

        return new StreamedTextDetokenizationResult(value.Trim(), repairCount);
    }

    private (string Text, int RepairCount) ReplaceAndCount(Regex regex, string input, string replacement, string ruleName)
    {
        var repairCount = 0;
        var text = regex.Replace(input, match =>
        {
            var repaired = match.Result(replacement);
            if (string.Equals(match.Value, repaired, StringComparison.Ordinal))
            {
                return match.Value;
            }

            repairCount++;
            _logger?.LogDebug(
                "StreamingSplitTokenArtifactRepaired. Before: {Before}. After: {After}. Rule: {RuleName}.",
                match.Value,
                repaired,
                ruleName);
            return repaired;
        });
        return (text, repairCount);
    }

    private static bool ShouldRepairSuffixSplit(string left, string suffix)
    {
        if (left.Length == 0 || suffix.Length == 0)
        {
            return false;
        }

        if (!left.All(char.IsLower) || !suffix.All(char.IsLower))
        {
            return false;
        }

        if (KnownSuffixSplitStems.Contains(left))
        {
            return true;
        }

        return left.Length <= 3 && CommonSuffixes.Contains(suffix);
    }

    private static string Preview(string text)
        => text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 77), "...");

    [GeneratedRegex(@"\b([A-Za-z]+)\s+('(?:s|t|re|ve|m|ll|d))\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex ContractionSpacingRegex();

    [GeneratedRegex(@"\s+(?<punctuation>[.,;:?!])", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationRegex();

    [GeneratedRegex(@"\(\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SpaceAfterOpeningParenRegex();

    [GeneratedRegex(@"\s+\)", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforeClosingParenRegex();

    [GeneratedRegex(@"\b([A-Za-z]+)\s+-\s+([A-Za-z]+)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex LetterHyphenSpacingRegex();

    [GeneratedRegex(@"\b(?<left>[A-Za-z]{1,8})\s+(?<suffix>ending|ivating|irs|ing|ed|er|ers|s|ly|tion|ment)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SuffixSplitRegex();
}
