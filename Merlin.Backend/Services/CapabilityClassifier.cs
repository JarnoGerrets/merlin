using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class CapabilityClassifier : ICapabilityClassifier
{
    private static readonly string[] QuestionPrefixes =
    [
        "what ",
        "why ",
        "how ",
        "who ",
        "when ",
        "where ",
        "explain ",
        "tell me ",
        "describe "
    ];

    private static readonly string[] MissingCapabilityTerms =
    [
        "news",
        "newsfeed",
        "web search",
        "search web",
        "search the web",
        "search the internet",
        "internet",
        "email",
        "emails",
        "mail",
        "folders",
        "folder",
        "files",
        "hard drive",
        "downloads",
        "calendar",
        "desktop",
        "scanner",
        "scan"
    ];

    private static readonly string[] UnsupportedActionTerms =
    [
        "delete all my files",
        "delete my files",
        "wipe my hard drive",
        "wipe drive",
        "format drive",
        "disable windows security",
        "disable windows defender",
        "disable defender",
        "destroy",
        "erase everything"
    ];

    private readonly ToolRegistry _toolRegistry;

    public CapabilityClassifier(ToolRegistry toolRegistry)
    {
        _toolRegistry = toolRegistry;
    }

    public bool MissingCapabilityDetectionEnabled => true;

    public int SupportedCapabilityCount => _toolRegistry.GetTools().Count;

    public IntentParseResult Classify(string message)
    {
        var originalMessage = message ?? string.Empty;
        var normalizedMessage = Normalize(originalMessage);

        if (string.IsNullOrWhiteSpace(normalizedMessage) || LooksLikeUnknownInput(normalizedMessage))
        {
            return CreateResult("unknown_input", normalizedMessage, originalMessage, 0.8);
        }

        if (UnsupportedActionTerms.Any(term => normalizedMessage.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return CreateResult("unsupported_action", normalizedMessage, originalMessage, 0.95);
        }

        if (MissingCapabilityTerms.Any(term => normalizedMessage.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return CreateResult("missing_capability", normalizedMessage, originalMessage, 0.9);
        }

        if (LooksLikeQuestion(normalizedMessage))
        {
            return new IntentParseResult
            {
                Intent = "general_conversation",
                NormalizedCommand = $"chat {normalizedMessage}",
                Confidence = 0.9,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(CapabilityClassifier)
            };
        }

        return new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = $"chat {normalizedMessage}",
            Confidence = 0.7,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(CapabilityClassifier)
        };
    }

    private static IntentParseResult CreateResult(
        string intent,
        string normalizedMessage,
        string originalMessage,
        double confidence)
    {
        return new IntentParseResult
        {
            Intent = intent,
            NormalizedCommand = normalizedMessage,
            Confidence = confidence,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(CapabilityClassifier)
        };
    }

    private static bool LooksLikeQuestion(string normalizedMessage)
    {
        return QuestionPrefixes.Any(prefix => normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeUnknownInput(string normalizedMessage)
    {
        var words = normalizedMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(word => IsGibberishToken(word));
    }

    private static bool IsGibberishToken(string word)
    {
        if (word.Length < 7)
        {
            return false;
        }

        var vowels = word.Count(character => "aeiou".Contains(character));
        return vowels == 0 || word.Distinct().Count() <= 3;
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
