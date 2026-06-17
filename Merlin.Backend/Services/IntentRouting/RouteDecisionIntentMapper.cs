using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public static class RouteDecisionIntentMapper
{
    public static IntentParseResult ToIntentParseResult(RouteDecision decision, string originalMessage, string normalizedText)
    {
        return decision.CapabilityId switch
        {
            "system.get_time" => SystemResource(originalMessage, decision, "system resource current_time", "system_time", "System Time"),
            "system.get_date" => SystemResource(originalMessage, decision, "system resource current_date", "system_date", "System Date"),
            "system.get_timezone" => SystemResource(originalMessage, decision, "system resource timezone", "system_timezone", "System Timezone"),
            "url.open" => new IntentParseResult
            {
                Intent = "open_url",
                NormalizedCommand = NormalizeUrlCommand(originalMessage, normalizedText),
                Confidence = decision.Confidence,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(MerlinIntentRouter),
                CapabilityId = "url_opening",
                CapabilityName = "URL Opening"
            },
            "app.open" => new IntentParseResult
            {
                Intent = "open_application",
                NormalizedCommand = NormalizeAppCommand(originalMessage, normalizedText, "open"),
                Confidence = decision.Confidence,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(MerlinIntentRouter),
                CapabilityId = "application_launch",
                CapabilityName = "Application Launch"
            },
            "chat.general" or "chat.recommendation" or "chat.reasoning" or "no_tool" => GeneralConversation(originalMessage, normalizedText, decision),
            _ => new IntentParseResult
            {
                Intent = decision.ShouldExecuteTool ? "missing_capability" : "general_conversation",
                NormalizedCommand = normalizedText,
                Confidence = decision.Confidence,
                OriginalMessage = originalMessage,
                ParserUsed = nameof(MerlinIntentRouter),
                CapabilityId = decision.CapabilityId,
                CapabilityName = decision.CapabilityId
            }
        };
    }

    private static IntentParseResult SystemResource(
        string originalMessage,
        RouteDecision decision,
        string normalizedCommand,
        string capabilityId,
        string capabilityName)
    {
        return new IntentParseResult
        {
            Intent = "system_resource_query",
            NormalizedCommand = normalizedCommand,
            Confidence = decision.Confidence,
            OriginalMessage = originalMessage,
            ParserUsed = nameof(MerlinIntentRouter),
            CapabilityId = capabilityId,
            CapabilityName = capabilityName
        };
    }

    private static IntentParseResult GeneralConversation(
        string originalMessage,
        string normalizedText,
        RouteDecision decision)
    {
        return new IntentParseResult
        {
            Intent = "general_conversation",
            NormalizedCommand = $"chat {normalizedText}",
            Confidence = Math.Max(decision.Confidence, 0.7),
            OriginalMessage = originalMessage,
            ParserUsed = nameof(MerlinIntentRouter),
            CapabilityId = "general_conversation",
            CapabilityName = "General Conversation"
        };
    }

    private static string NormalizeAppCommand(string originalMessage, string normalizedText, string verb)
    {
        var target = ExtractActionTarget(originalMessage, ["open", "launch", "start", "pull up"]);
        if (!string.IsNullOrWhiteSpace(target))
        {
            return $"{verb} {CleanActionTarget(target)}";
        }

        target = ExtractActionTarget(normalizedText, ["open", "launch", "start", "pull up"]);
        if (!string.IsNullOrWhiteSpace(target))
        {
            return $"{verb} {target}";
        }

        return normalizedText;
    }

    private static string NormalizeUrlCommand(string originalMessage, string normalizedText)
    {
        var target = ExtractActionTarget(originalMessage, ["open", "go to", "take me to", "browse", "visit", "pull up"]);
        if (string.IsNullOrWhiteSpace(target))
        {
            target = ExtractUrlLikeToken(originalMessage);
        }

        if (!string.IsNullOrWhiteSpace(target))
        {
            return $"open {NormalizeBrowserTarget(CleanActionTarget(target))}";
        }

        return normalizedText;
    }

    private static string ExtractActionTarget(string message, IReadOnlyList<string> verbs)
    {
        var text = message.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var verb in verbs)
        {
            var index = text.IndexOf(verb, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterIndex = index + verb.Length;
            var afterIsBoundary = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
            if (!beforeIsBoundary || !afterIsBoundary)
            {
                continue;
            }

            return CleanActionTarget(text[afterIndex..].Trim());
        }

        return string.Empty;
    }

    private static string ExtractUrlLikeToken(string message)
    {
        var text = message.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = word.Trim('\'', '"', '(', ')', '[', ']', '{', '}', ',', ';', ':', '!', '?');
            if (trimmed.Contains("://", StringComparison.Ordinal)
                || trimmed.Contains('.') && !trimmed.Contains('\\') && !trimmed.Contains('/'))
            {
                return CleanActionTarget(trimmed);
            }
        }

        return string.Empty;
    }

    private static string CleanActionTarget(string target)
    {
        var cleaned = target.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var suffix in new[]
        {
            " in the browser",
            " in browser",
            " on the web",
            " as a website",
            " for me please",
            " for me sir",
            " for me",
            " please",
            " sir"
        })
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^suffix.Length].Trim();
                break;
            }
        }

        foreach (var prefix in new[]
        {
            "the ",
            "my "
        })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
                break;
            }
        }

        return cleaned;
    }

    private static string NormalizeBrowserTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target)
            || target.Contains('.', StringComparison.Ordinal)
            || target.Contains("://", StringComparison.Ordinal)
            || target.Contains('\\')
            || target.Contains('/'))
        {
            return target;
        }

        return $"{target}.com";
    }
}
