using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class RuleBasedIntentParser : IIntentParser
{
    private static readonly string[] UrlPrefixes =
    [
        "take me to ",
        "go to ",
        "open ",
        "browse ",
        "visit "
    ];

    private static readonly string[] DiscoveryPhrases =
    [
        "list tools",
        "show tools",
        "what can you do",
        "what tools do you have",
        "show available tools"
    ];

    private static readonly string[] DiagnosticsPhrases =
    [
        "show status",
        "system status",
        "diagnostics",
        "health check",
        "merlin status",
        "show diagnostics"
    ];

    private static readonly string[] ConversationPhrases =
    [
        "tell me a joke",
        "who are you",
        "what are you",
        "explain yourself",
        "tell me something interesting",
        "how do you work",
        "what is merlin",
        "explain merlin"
    ];

    private static readonly string[] MissingCapabilityPhrases =
    [
        "can you check my folders",
        "can you check my files",
        "check my files",
        "check my folders",
        "check my desktop",
        "check this document",
        "check if excel is installed",
        "search my hard drive",
        "clean my downloads folder"
    ];

    private static readonly string[] UnsupportedActionPhrases =
    [
        "delete my files",
        "delete all my files",
        "delete files",
        "wipe my hard drive",
        "wipe drive",
        "disable windows security",
        "disable windows defender",
        "install chrome",
        "update windows",
        "download software",
        "open powershell"
    ];

    private readonly ApplicationLaunchOptions _applicationLaunchOptions;

    public RuleBasedIntentParser(IOptions<ApplicationLaunchOptions> applicationLaunchOptions)
    {
        _applicationLaunchOptions = applicationLaunchOptions.Value;
    }

    public Task<IntentParseResult> ParseAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var originalMessage = message ?? string.Empty;
        var normalizedMessage = NormalizeText(originalMessage);

        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            return Task.FromResult(Unknown(originalMessage, normalizedMessage));
        }

        if (TryParseToolDiscovery(normalizedMessage, originalMessage, out var discoveryResult))
        {
            return Task.FromResult(discoveryResult);
        }

        if (TryParseDiagnostics(normalizedMessage, originalMessage, out var diagnosticsResult))
        {
            return Task.FromResult(diagnosticsResult);
        }

        if (TryParseConfirmation(normalizedMessage, originalMessage, out var confirmationResult))
        {
            return Task.FromResult(confirmationResult);
        }

        if (TryParseGeneralConversation(normalizedMessage, originalMessage, out var conversationResult))
        {
            return Task.FromResult(conversationResult);
        }

        if (TryParseMissingCapability(normalizedMessage, originalMessage, out var missingCapabilityResult))
        {
            return Task.FromResult(missingCapabilityResult);
        }

        if (TryParseUnsupportedAction(normalizedMessage, originalMessage, out var unsupportedActionResult))
        {
            return Task.FromResult(unsupportedActionResult);
        }

        if (TryParseApplication(normalizedMessage, originalMessage, out var applicationResult))
        {
            return Task.FromResult(applicationResult);
        }

        if (TryParseUrl(normalizedMessage, originalMessage, out var urlResult))
        {
            return Task.FromResult(urlResult);
        }

        return Task.FromResult(Unknown(originalMessage, normalizedMessage));
    }

    private static bool TryParseGeneralConversation(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (ConversationPhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = new IntentParseResult
            {
                Intent = "general_conversation",
                NormalizedCommand = $"chat {normalizedMessage}",
                Confidence = 0.95,
                OriginalMessage = originalMessage,
                CapabilityId = "general_conversation",
                CapabilityName = "General Conversation"
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static bool TryParseUnsupportedAction(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (UnsupportedActionPhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = new IntentParseResult
            {
                Intent = "unsupported_action",
                NormalizedCommand = normalizedMessage,
                Confidence = 0.95,
                OriginalMessage = originalMessage,
                CapabilityId = GetUnsupportedCapabilityId(normalizedMessage),
                CapabilityName = GetCapabilityName(GetUnsupportedCapabilityId(normalizedMessage))
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static bool TryParseMissingCapability(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (MissingCapabilityPhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = new IntentParseResult
            {
                Intent = "missing_capability",
                NormalizedCommand = normalizedMessage,
                Confidence = 0.95,
                OriginalMessage = originalMessage,
                CapabilityId = GetMissingCapabilityId(normalizedMessage),
                CapabilityName = GetCapabilityName(GetMissingCapabilityId(normalizedMessage))
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static bool TryParseToolDiscovery(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        foreach (var phrase in DiscoveryPhrases)
        {
            if (!string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result = new IntentParseResult
            {
                Intent = "tool_discovery",
                NormalizedCommand = "list tools",
                Confidence = phrase == normalizedMessage ? 0.98 : 0.9,
                OriginalMessage = originalMessage,
                CapabilityId = "tool_discovery",
                CapabilityName = "Tool Discovery"
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static bool TryParseDiagnostics(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        foreach (var phrase in DiagnosticsPhrases)
        {
            if (!string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result = new IntentParseResult
            {
                Intent = "diagnostics",
                NormalizedCommand = "show status",
                Confidence = 0.98,
                OriginalMessage = originalMessage,
                CapabilityId = "diagnostics",
                CapabilityName = "Diagnostics"
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static bool TryParseConfirmation(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (string.Equals(normalizedMessage, "confirm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedMessage, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedMessage, "approve", StringComparison.OrdinalIgnoreCase)
            || normalizedMessage.StartsWith("choose ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedMessage, "first one", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedMessage, "second one", StringComparison.OrdinalIgnoreCase))
        {
            result = new IntentParseResult
            {
                Intent = "confirmation",
                NormalizedCommand = normalizedMessage,
                Confidence = 0.98,
                OriginalMessage = originalMessage,
                CapabilityId = "confirmation",
                CapabilityName = "Confirmation"
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private bool TryParseApplication(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        foreach (var application in _applicationLaunchOptions.Applications)
        {
            var matchingAlias = GetMatchingApplicationAlias(normalizedMessage, application.Key, application.Value);
            if (matchingAlias is null)
            {
                continue;
            }

            if (!ContainsAny(normalizedMessage, ["open", "start", "launch"]))
            {
                continue;
            }

            result = new IntentParseResult
            {
                Intent = "open_application",
                NormalizedCommand = $"open {application.Key}",
                Confidence = StartsWithAny(normalizedMessage, ["open ", "start ", "launch "]) ? 0.98 : 0.95,
                OriginalMessage = originalMessage,
                CapabilityId = "application_launch",
                CapabilityName = "Application Launch"
            };

            return true;
        }

        if (StartsWithAny(normalizedMessage, ["open ", "start ", "launch "]))
        {
            var target = normalizedMessage.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).ElementAtOrDefault(1);
            if (!string.IsNullOrWhiteSpace(target) && !LooksLikeUrlInput(target))
            {
                result = new IntentParseResult
                {
                    Intent = "open_application",
                    NormalizedCommand = $"open {target}",
                    Confidence = 0.75,
                    OriginalMessage = originalMessage,
                    CapabilityId = "application_launch",
                    CapabilityName = "Application Launch"
                };

                return true;
            }
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static string? GetMatchingApplicationAlias(
        string normalizedMessage,
        string applicationKey,
        ApplicationLaunchTarget application)
    {
        if (ContainsWholePhrase(normalizedMessage, applicationKey))
        {
            return applicationKey;
        }

        return application.Aliases.FirstOrDefault(alias => ContainsWholePhrase(normalizedMessage, alias));
    }

    private static bool TryParseUrl(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        foreach (var prefix in UrlPrefixes)
        {
            if (!normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = normalizedMessage[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                break;
            }

            if (!LooksLikeUrlInput(target))
            {
                continue;
            }

            result = new IntentParseResult
            {
                Intent = "open_url",
                NormalizedCommand = $"open {target}",
                Confidence = prefix == "open " ? 0.98 : 0.94,
                OriginalMessage = originalMessage,
                CapabilityId = "url_opening",
                CapabilityName = "URL Opening"
            };

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static IntentParseResult Unknown(string originalMessage, string normalizedMessage)
    {
        return new IntentParseResult
        {
            Intent = null,
            NormalizedCommand = normalizedMessage,
            Confidence = 0,
            OriginalMessage = originalMessage
        };
    }

    private static string NormalizeText(string value)
    {
        var trimmed = value.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string value, IReadOnlyCollection<string> terms)
    {
        return terms.Any(term => ContainsWholePhrase(value, term));
    }

    private static bool StartsWithAny(string value, IReadOnlyCollection<string> prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsWholePhrase(string value, string phrase)
    {
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

    private static bool LooksLikeUrlInput(string target)
    {
        return IsLocalWindowsPath(target)
            || IsUncPath(target)
            || TryGetExplicitScheme(target, out _)
            || (target.Contains('.') && !target.Contains(' '));
    }

    private static string GetMissingCapabilityId(string normalizedMessage)
    {
        if (ContainsWholePhrase(normalizedMessage, "folder")
            || ContainsWholePhrase(normalizedMessage, "folders")
            || ContainsWholePhrase(normalizedMessage, "file")
            || ContainsWholePhrase(normalizedMessage, "files")
            || ContainsWholePhrase(normalizedMessage, "desktop")
            || ContainsWholePhrase(normalizedMessage, "document")
            || ContainsWholePhrase(normalizedMessage, "hard drive")
            || ContainsWholePhrase(normalizedMessage, "downloads"))
        {
            return "file_access";
        }

        return "web_search";
    }

    private static string GetUnsupportedCapabilityId(string normalizedMessage)
    {
        if (ContainsWholePhrase(normalizedMessage, "delete")
            || ContainsWholePhrase(normalizedMessage, "wipe")
            || ContainsWholePhrase(normalizedMessage, "format"))
        {
            return "destructive_file_action";
        }

        if (ContainsWholePhrase(normalizedMessage, "install")
            || ContainsWholePhrase(normalizedMessage, "download")
            || ContainsWholePhrase(normalizedMessage, "update"))
        {
            return "software_installation";
        }

        return "system_settings";
    }

    private static string GetCapabilityName(string capabilityId)
    {
        return capabilityId switch
        {
            "file_access" => "File Access",
            "destructive_file_action" => "Destructive File Action",
            "software_installation" => "Software Installation",
            "system_settings" => "System Settings",
            _ => "Web Search"
        };
    }

    private static bool TryGetExplicitScheme(string target, out string scheme)
    {
        scheme = string.Empty;
        var schemeSeparatorIndex = target.IndexOf(':');

        if (schemeSeparatorIndex <= 0)
        {
            return false;
        }

        scheme = target[..schemeSeparatorIndex];
        return scheme.All(character => char.IsLetterOrDigit(character)
            || character == '+'
            || character == '-'
            || character == '.');
    }

    private static bool IsLocalWindowsPath(string target)
    {
        return target.Length >= 3
            && char.IsLetter(target[0])
            && target[1] == ':'
            && (target[2] == '\\' || target[2] == '/');
    }

    private static bool IsUncPath(string target)
    {
        return target.StartsWith(@"\\", StringComparison.Ordinal);
    }
}
