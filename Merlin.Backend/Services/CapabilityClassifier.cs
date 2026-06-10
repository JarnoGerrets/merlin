using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class CapabilityClassifier : ICapabilityClassifier
{
    private static readonly string[] DestructiveTerms =
    [
        "delete all files",
        "delete all my files",
        "delete files",
        "delete my files",
        "wipe drive",
        "wipe my hard drive",
        "format disk",
        "format drive",
        "disable windows defender",
        "disable windows security"
    ];

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
            return CreateResult("unknown_input", normalizedMessage, originalMessage, null, 0.8);
        }

        if (DestructiveTerms.Any(term => ContainsWholePhrase(normalizedMessage, term)))
        {
            return CreateResult(
                "unsupported_action",
                normalizedMessage,
                originalMessage,
                GetDomain("destructive_file_action"),
                0.95);
        }

        if (TryFindObviousMissingDomain(normalizedMessage, out var missingDomain))
        {
            return CreateResult("missing_capability", normalizedMessage, originalMessage, missingDomain, 0.9);
        }

        if (LooksLikeQuestion(normalizedMessage))
        {
            var conversationDomain = GetDomain("general_conversation");
            return new IntentParseResult
            {
                Intent = "general_conversation",
                NormalizedCommand = $"chat {normalizedMessage}",
                Confidence = 0.9,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(CapabilityClassifier),
                CapabilityId = conversationDomain?.Id,
                CapabilityName = conversationDomain?.Name
            };
        }

        var fallbackDomain = GetDomain("general_conversation");
        return new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = $"chat {normalizedMessage}",
            Confidence = 0.7,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(CapabilityClassifier),
            CapabilityId = fallbackDomain?.Id,
            CapabilityName = fallbackDomain?.Name
        };
    }

    private IntentParseResult CreateResult(
        string intent,
        string normalizedMessage,
        string originalMessage,
        CapabilityDomain? domain,
        double confidence)
    {
        return new IntentParseResult
        {
            Intent = intent,
            NormalizedCommand = normalizedMessage,
            Confidence = confidence,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(CapabilityClassifier),
            CapabilityId = domain?.Id,
            CapabilityName = domain?.Name
        };
    }

    private static bool LooksLikeQuestion(string normalizedMessage)
    {
        return QuestionPrefixes.Any(prefix => normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private bool TryFindObviousMissingDomain(string normalizedMessage, out CapabilityDomain? domain)
    {
        domain = null;

        var domainId = normalizedMessage switch
        {
            var message when ContainsWholePhrase(message, "time") => "time",
            var message when ContainsWholePhrase(message, "news")
                || ContainsWholePhrase(message, "newsfeed")
                || ContainsWholePhrase(message, "headlines") => "news",
            var message when ContainsWholePhrase(message, "internet")
                || ContainsWholePhrase(message, "web search")
                || ContainsWholePhrase(message, "search web")
                || ContainsWholePhrase(message, "search internet")
                || ContainsWholePhrase(message, "search the internet") => "web_search",
            var message when ContainsWholePhrase(message, "email")
                || ContainsWholePhrase(message, "emails")
                || ContainsWholePhrase(message, "mail") => "email",
            var message when ContainsWholePhrase(message, "calendar") => "calendar",
            var message when ContainsWholePhrase(message, "folder")
                || ContainsWholePhrase(message, "folders")
                || ContainsWholePhrase(message, "file")
                || ContainsWholePhrase(message, "files")
                || ContainsWholePhrase(message, "hard drive")
                || ContainsWholePhrase(message, "desktop")
                || ContainsWholePhrase(message, "downloads") => "file_access",
            _ => null
        };

        if (domainId is null)
        {
            return false;
        }

        domain = GetDomain(domainId);
        return domain is not null;
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

    private CapabilityDomain? GetDomain(string domainId)
    {
        return _capabilityOptions.CapabilityDomains.FirstOrDefault(domain =>
            string.Equals(domain.Id, domainId, StringComparison.OrdinalIgnoreCase));
    }

    private static CapabilityOptions MergeWithDefaults(CapabilityOptions configuredOptions)
    {
        var defaults = CapabilityOptions.CreateDefault();

        if (configuredOptions.CapabilityDomains.Count == 0)
        {
            configuredOptions.CapabilityDomains = defaults.CapabilityDomains;
        }

        return configuredOptions;
    }
}
