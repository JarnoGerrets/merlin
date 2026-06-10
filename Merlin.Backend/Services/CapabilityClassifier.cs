using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

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

    private readonly CapabilityOptions _capabilityOptions;
    private readonly ToolRegistry _toolRegistry;

    public CapabilityClassifier(
        ToolRegistry toolRegistry,
        IOptions<CapabilityOptions> capabilityOptions)
    {
        _toolRegistry = toolRegistry;
        _capabilityOptions = MergeWithDefaults(capabilityOptions.Value);
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

        if (FindMatchingRule(_capabilityOptions.UnsupportedActions, normalizedMessage) is not null)
        {
            return CreateResult("unsupported_action", normalizedMessage, originalMessage, 0.95);
        }

        if (FindMatchingRule(_capabilityOptions.MissingCapabilities, normalizedMessage) is not null)
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

    private static CapabilityRule? FindMatchingRule(
        IEnumerable<CapabilityRule> rules,
        string normalizedMessage)
    {
        return rules.FirstOrDefault(rule =>
            rule.Keywords.Any(keyword => ContainsWholePhrase(normalizedMessage, Normalize(keyword))));
    }

    private static bool LooksLikeUnknownInput(string normalizedMessage)
    {
        var words = normalizedMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(word => IsGibberishToken(word));
    }

    private static bool IsGibberishToken(string word)
    {
        if (word is "qwerty" or "asdfgh" or "asdfghjkl")
        {
            return true;
        }

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

    private static bool ContainsWholePhrase(string value, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        var afterIndex = index + phrase.Length;
        var afterIsBoundary = afterIndex >= value.Length || !char.IsLetterOrDigit(value[afterIndex]);

        return beforeIsBoundary && afterIsBoundary;
    }

    private static CapabilityOptions MergeWithDefaults(CapabilityOptions configuredOptions)
    {
        var defaults = CapabilityOptions.CreateDefault();

        if (configuredOptions.MissingCapabilities.Count == 0)
        {
            configuredOptions.MissingCapabilities = defaults.MissingCapabilities;
        }

        if (configuredOptions.UnsupportedActions.Count == 0)
        {
            configuredOptions.UnsupportedActions = defaults.UnsupportedActions;
        }

        return configuredOptions;
    }
}
