using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class IntentFallbackClassifier : IIntentFallbackClassifier
{
    private static readonly string[] ActionPrefixes =
    [
        "open ",
        "install ",
        "delete ",
        "search ",
        "check ",
        "launch ",
        "start ",
        "update ",
        "scan ",
        "find ",
        "clean "
    ];

    private static readonly string[] ActionPhrases =
    [
        "can you check ",
        "could you check ",
        "please check ",
        "search my ",
        "delete my ",
        "install ",
        "update ",
        "clean my ",
        "scan my ",
        "find my "
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

    public IntentParseResult Classify(string message)
    {
        var originalMessage = message ?? string.Empty;
        var normalizedMessage = Normalize(originalMessage);

        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return new IntentParseResult
            {
                Intent = "unknown",
                NormalizedCommand = normalizedMessage,
                Confidence = 0,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(IntentFallbackClassifier)
            };
        }

        if (LooksLikeActionRequest(normalizedMessage))
        {
            return new IntentParseResult
            {
                Intent = "unsupported_action",
                NormalizedCommand = normalizedMessage,
                Confidence = 0.9,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(IntentFallbackClassifier)
            };
        }

        return new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = $"chat {normalizedMessage}",
            Confidence = LooksLikeQuestion(normalizedMessage) ? 0.9 : 0.75,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(IntentFallbackClassifier)
        };
    }

    private static bool LooksLikeActionRequest(string normalizedMessage)
    {
        return ActionPrefixes.Any(prefix => normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            || ActionPhrases.Any(phrase => normalizedMessage.StartsWith(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeQuestion(string normalizedMessage)
    {
        return QuestionPrefixes.Any(prefix => normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
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
