using System.Text.RegularExpressions;

namespace Merlin.Backend.Core.Memory.Services;

public static class TopicSummarySanitizer
{
    public const int MaxSessionMemoryCharacters = 350;
    public const int MaxRollingSummaryCharacters = 700;

    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex SpaceBeforePunctuationRegex = new("\\s+([.,!?;:])", RegexOptions.Compiled);
    private static readonly Regex SpaceAfterApostropheRegex = new("\\s'\\s", RegexOptions.Compiled);
    private static readonly Regex RepeatedUserDiscussedRegex = new(
        @"\b(?:User discussed\s+){2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AssistantBoilerplateRegex = new(
        @"(?:Assistant response touched on\s+)?(?:It seems (?:like )?(?:your message might be incomplete|you're reflecting on)[^.?!]*[.?!]?|Could you please clarify[^.?!]*[.?!]?|Please provide more context[^.?!]*[.?!]?|I'm here to help[^.?!]*[.?!]?|Let me know what you need[^.?!]*[.?!]?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RepeatedGeneralConversationLabelRegex = new(
        @"\bgeneral conversation:\s*(?:general conversation:\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LabeledClauseRegex = new(
        @"(?<label>[A-Za-z][A-Za-z0-9 /_-]{1,80}):\s*(?<body>.*?)(?=\s+[A-Za-z][A-Za-z0-9 /_-]{1,80}:\s*|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "after", "again", "also", "because", "before", "could", "does", "doing",
        "from", "have", "into", "just", "like", "that", "there", "this", "what", "when",
        "where", "which", "with", "would", "your", "youre", "you're", "user", "assistant",
        "conversation", "general", "please", "clarify"
    };

    private static readonly string[] DanglingStarts =
    [
        "and when ",
        "but when ",
        "because ",
        "when you ",
        "and then ",
        "but then "
    ];

    public static string CleanText(string? text, int maxCharacters = MaxRollingSummaryCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Replace('\r', ' ').Replace('\n', ' ');
        cleaned = SpaceAfterApostropheRegex.Replace(cleaned, "'");
        cleaned = WhitespaceRegex.Replace(cleaned, " ").Trim();
        cleaned = RepeatedUserDiscussedRegex.Replace(cleaned, "User discussed ");
        cleaned = StripRecursiveUserDiscussed(cleaned);
        cleaned = CollapseRepeatedTopicLabels(cleaned);
        cleaned = AssistantBoilerplateRegex.Replace(cleaned, string.Empty);
        cleaned = WhitespaceRegex.Replace(cleaned, " ").Trim();
        cleaned = SpaceBeforePunctuationRegex.Replace(cleaned, "$1");
        cleaned = cleaned.Replace(" .", ".", StringComparison.Ordinal)
            .Replace(" ,", ",", StringComparison.Ordinal)
            .Replace(" :", ":", StringComparison.Ordinal);
        cleaned = RemoveRepeatedClauses(cleaned);
        return Truncate(cleaned, maxCharacters);
    }

    public static string SanitizeForSession(string? summary, string? title = null, int maxCharacters = MaxSessionMemoryCharacters)
    {
        var cleaned = CleanText(summary, maxCharacters);
        var userFragments = ExtractMeaningfulUserFragments(cleaned);
        if (userFragments.Count > 0)
        {
            return Truncate(EnsureSentence(string.Join(" ", userFragments)), maxCharacters);
        }

        var topicClause = ExtractPreferredTopicClause(cleaned);
        if (!string.IsNullOrWhiteSpace(topicClause))
        {
            return Truncate(topicClause, maxCharacters);
        }

        cleaned = StripLeadingUserDiscussed(cleaned);
        cleaned = StripAssistantFragments(cleaned);
        cleaned = WhitespaceRegex.Replace(cleaned, " ").Trim();

        if (string.IsNullOrWhiteSpace(cleaned) && !string.IsNullOrWhiteSpace(title))
        {
            cleaned = NormalizeTopicTitle(title);
        }

        return Truncate(EnsureSentence(cleaned), maxCharacters);
    }

    public static string BuildRollingUserSummary(
        string? existingSummary,
        string userMessage,
        IReadOnlyCollection<string> concepts)
    {
        var cleanMessage = CleanText(userMessage, 240);
        if (string.IsNullOrWhiteSpace(cleanMessage) || IsLowValueUserMessage(cleanMessage))
        {
            return CleanText(existingSummary, MaxRollingSummaryCharacters);
        }

        var conceptText = concepts.Count == 0 ? "general conversation" : string.Join(", ", concepts.Take(8));
        var sentence = $"User discussed {conceptText}: {EnsureSentence(cleanMessage)}";
        return AppendCleanSentence(existingSummary, sentence);
    }

    public static string BuildRollingAssistantSummary(
        string? existingSummary,
        string assistantResponse,
        IReadOnlyCollection<string> concepts)
    {
        if (IsClarificationBoilerplate(assistantResponse))
        {
            return CleanText(existingSummary, MaxRollingSummaryCharacters);
        }

        var cleanResponse = CleanText(assistantResponse, 220);
        if (string.IsNullOrWhiteSpace(cleanResponse))
        {
            return CleanText(existingSummary, MaxRollingSummaryCharacters);
        }

        var conceptText = concepts.Count == 0 ? "the topic" : string.Join(", ", concepts.Take(8));
        var sentence = $"Assistant response touched on {conceptText}: {EnsureSentence(cleanResponse)}";
        return AppendCleanSentence(existingSummary, sentence);
    }

    public static bool IsLowValueUserMessage(string? message)
    {
        var cleaned = CleanText(message, 240);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return true;
        }

        var lower = cleaned.ToLowerInvariant();
        if (DanglingStarts.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return true;
        }

        var meaningfulTokens = Regex.Matches(lower, @"[a-z0-9']+")
            .Select(match => match.Value)
            .Where(token => token.Length > 2 && !Stopwords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return meaningfulTokens < 2;
    }

    public static bool IsClarificationBoilerplate(string? assistantResponse)
    {
        if (string.IsNullOrWhiteSpace(assistantResponse))
        {
            return false;
        }

        var lower = CleanText(assistantResponse, 500).ToLowerInvariant();
        var hits = 0;
        if (lower.Contains("message might be incomplete", StringComparison.Ordinal))
        {
            hits++;
        }

        if (lower.Contains("could you") && lower.Contains("clarify"))
        {
            hits++;
        }

        if (lower.Contains("provide more context", StringComparison.Ordinal))
        {
            hits++;
        }

        if (lower.Contains("i'm here to help", StringComparison.Ordinal))
        {
            hits++;
        }

        if (lower.Contains("let me know what you need", StringComparison.Ordinal))
        {
            hits++;
        }

        return hits >= 1 && lower.Length <= 700;
    }

    public static bool HasMeaningfulTopicSummary(string? summary)
    {
        var session = SanitizeForSession(summary);
        return !string.IsNullOrWhiteSpace(session) && !IsLowValueUserMessage(session);
    }

    private static string AppendCleanSentence(string? existingSummary, string sentence)
    {
        var existing = CleanText(existingSummary, MaxRollingSummaryCharacters);
        var cleanedSentence = CleanText(sentence, 280);
        var next = string.IsNullOrWhiteSpace(existing)
            ? cleanedSentence
            : $"{existing} {cleanedSentence}";
        return CleanText(next, MaxRollingSummaryCharacters);
    }

    private static string StripRecursiveUserDiscussed(string text)
    {
        var cleaned = text;
        while (cleaned.Contains("User discussed User discussed", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = Regex.Replace(cleaned, "User discussed\\s+User discussed", "User discussed", RegexOptions.IgnoreCase);
        }

        return cleaned;
    }

    private static string StripLeadingUserDiscussed(string text)
    {
        var cleaned = text.Trim();
        while (cleaned.StartsWith("User discussed ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["User discussed ".Length..].Trim();
        }

        return cleaned;
    }

    private static string StripAssistantFragments(string text)
    {
        var marker = text.IndexOf("Assistant response touched on", StringComparison.OrdinalIgnoreCase);
        return marker < 0 ? text : text[..marker].Trim();
    }

    private static string CollapseRepeatedTopicLabels(string text)
    {
        var cleaned = text;
        string previous;
        do
        {
            previous = cleaned;
            cleaned = RepeatedGeneralConversationLabelRegex.Replace(cleaned, "general conversation: ");
        }
        while (!string.Equals(previous, cleaned, StringComparison.Ordinal));

        return cleaned;
    }

    private static string? ExtractPreferredTopicClause(string text)
    {
        foreach (Match match in LabeledClauseRegex.Matches(text))
        {
            var label = CleanText(match.Groups["label"].Value, 120);
            var body = CleanText(match.Groups["body"].Value, 240);
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            if (IsAssistantAnswerClause(body) || IsLowValueUserMessage(body))
            {
                continue;
            }

            return NaturalizeTopicClause(label, body);
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractMeaningfulUserFragments(string text)
    {
        var matches = Regex.Matches(
            text,
            @"User discussed\s+(?<concept>[^:]{1,120}):\s*(?<body>.*?)(?=\s+User discussed\s+|\s+Assistant response touched on\s+|$)",
            RegexOptions.IgnoreCase);
        var fragments = new List<string>();
        foreach (Match match in matches)
        {
            var concept = StripLeadingUserDiscussed(CleanText(match.Groups["concept"].Value, 120));
            var body = StripLeadingUserDiscussed(CleanText(match.Groups["body"].Value, 220));
            if (string.IsNullOrWhiteSpace(body) || IsLowValueUserMessage(body))
            {
                continue;
            }

            var fragment = string.IsNullOrWhiteSpace(concept)
                ? EnsureSentence(body)
                : NaturalizeTopicClause(concept, body);
            fragments.Add(fragment);
        }

        return fragments
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsAssistantAnswerClause(string text)
    {
        var lower = CleanText(text, 260).ToLowerInvariant();
        string[] assistantAnswerStarts =
        [
            "since this is",
            "it depends",
            "there is no single answer",
            "many people",
            "you could think of it as",
            "in practical terms",
            "the answer is",
            "this means",
            "that means",
            "it seems",
            "i think",
            "i would"
        ];

        return assistantAnswerStarts.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string NaturalizeTopicClause(string label, string body)
    {
        var cleanLabel = CleanText(label, 120).Trim().TrimEnd(':');
        var cleanBody = NormalizeTopicBody(body);
        if (string.IsNullOrWhiteSpace(cleanBody))
        {
            return EnsureSentence(ToSentenceCase(cleanLabel));
        }

        if (string.Equals(cleanLabel, "general conversation", StringComparison.OrdinalIgnoreCase))
        {
            return $"General conversation about {cleanBody}.";
        }

        return $"{ToSentenceCase(cleanLabel)}: {EnsureSentence(cleanBody)}";
    }

    private static string NormalizeTopicBody(string body)
    {
        var cleaned = CleanText(body, 240).Trim().TrimEnd('.', '!', '?');
        if (cleaned.StartsWith("that is ", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned["that is ".Length..].Trim();
        }

        return cleaned;
    }

    private static string ToSentenceCase(string value)
    {
        var cleaned = value.Trim();
        return string.IsNullOrWhiteSpace(cleaned)
            ? string.Empty
            : char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }

    private static string RemoveRepeatedClauses(string text)
    {
        var parts = Regex.Split(text, @"(?<=[.?!])\s+")
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return string.Join(" ", parts);
    }

    private static string NormalizeTopicTitle(string title)
    {
        var cleaned = StripLeadingUserDiscussed(CleanText(title, MaxSessionMemoryCharacters));
        return EnsureSentence(cleaned);
    }

    private static string EnsureSentence(string text)
    {
        var cleaned = text.Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        return cleaned.EndsWith('.') || cleaned.EndsWith('!') || cleaned.EndsWith('?')
            ? cleaned
            : cleaned + ".";
    }

    private static string Truncate(string text, int maxCharacters)
    {
        if (maxCharacters <= 0 || text.Length <= maxCharacters)
        {
            return text;
        }

        return text[..Math.Max(0, maxCharacters - 3)].TrimEnd() + "...";
    }
}
