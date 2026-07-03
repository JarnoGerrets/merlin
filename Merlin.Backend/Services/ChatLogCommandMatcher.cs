using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public enum ChatLogCommandAction
{
    Show,
    Hide
}

public static class ChatLogCommandMatcher
{
    private static readonly HashSet<string> OpenVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "show",
        "open"
    };

    private static readonly HashSet<string> CloseVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "hide",
        "close"
    };

    private static readonly HashSet<string> IgnoredTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "merlin",
        "please",
        "can",
        "could",
        "you",
        "would",
        "kindly",
        "the",
        "my",
        "a",
        "me"
    };

    private static readonly HashSet<string> ChatTargetTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "chat",
        "chatlog",
        "jetlog"
    };

    public static bool TryMatch(string? message, out ChatLogCommandAction action)
    {
        action = ChatLogCommandAction.Show;
        var normalized = SpokenCommandNormalizer.Normalize(message);
        if (string.IsNullOrWhiteSpace(normalized.CommandText))
        {
            return false;
        }

        var tokens = Tokenize(normalized.CommandText)
            .Where(token => !IgnoredTokens.Contains(token))
            .ToList();
        if (tokens.Count is < 2 or > 3 || !TryGetAction(tokens[0], out action))
        {
            return false;
        }

        return IsChatTarget(tokens.Skip(1).ToList());
    }

    private static bool TryGetAction(string token, out ChatLogCommandAction action)
    {
        if (OpenVerbs.Contains(token))
        {
            action = ChatLogCommandAction.Show;
            return true;
        }

        if (CloseVerbs.Contains(token))
        {
            action = ChatLogCommandAction.Hide;
            return true;
        }

        action = ChatLogCommandAction.Show;
        return false;
    }

    public static IntentParseResult ToIntentParseResult(
        ChatLogCommandAction action,
        string originalMessage,
        string normalizedMessage)
    {
        return new IntentParseResult
        {
            Intent = action == ChatLogCommandAction.Show ? "ui_panel_show" : "ui_panel_hide",
            NormalizedCommand = action == ChatLogCommandAction.Show
                ? "ui panel show chat"
                : "ui panel hide chat",
            Confidence = 1.0,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(ChatLogCommandMatcher),
            CapabilityId = "ui_panel",
            CapabilityName = "Chat Panel"
        };
    }

    private static List<string> Tokenize(string message)
    {
        return message
            .Trim()
            .ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', '!', '?', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static bool IsChatTarget(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 1 && ChatTargetTokens.Contains(tokens[0]))
        {
            return true;
        }

        return tokens.Count == 2
            && string.Equals(tokens[0], "chat", StringComparison.OrdinalIgnoreCase)
            && string.Equals(tokens[1], "log", StringComparison.OrdinalIgnoreCase);
    }
}
