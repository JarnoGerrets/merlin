using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class RuleBasedIntentParser : IIntentParser
{
    private static readonly string[] UrlPrefixes =
    [
        "take me to ",
        "please take me to ",
        "go to ",
        "goto ",
        "please go to ",
        "please goto ",
        "pull up ",
        "please pull up ",
        "open ",
        "please open ",
        "browse ",
        "please browse ",
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

    private static readonly string[] CurrentTimePhrases =
    [
        "what time is it",
        "what is the time",
        "current time",
        "tell me the time"
    ];

    private static readonly string[] CurrentDatePhrases =
    [
        "what is today's date",
        "whats today's date",
        "what is the date",
        "current date",
        "tell me the date"
    ];

    private static readonly string[] TimeZonePhrases =
    [
        "what timezone am i in",
        "what time zone am i in",
        "what is my timezone",
        "what is my time zone",
        "local timezone",
        "local time zone"
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

    private static readonly string[] WakePhrases =
    [
        "are you awake",
        "you awake",
        "are you there",
        "you there",
        "are you listening",
        "you listening",
        "hello merlin",
        "hey merlin",
        "hi merlin",
        "merlin are you awake",
        "merlin are you there",
        "merlin wake up",
        "wake up merlin",
        "hey merlin are you awake",
        "ok merlin are you awake"
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
    private readonly ITrustedUrlStore _trustedUrlStore;

    public RuleBasedIntentParser(
        IOptions<ApplicationLaunchOptions> applicationLaunchOptions,
        ITrustedUrlStore? trustedUrlStore = null)
    {
        _applicationLaunchOptions = applicationLaunchOptions.Value;
        _trustedUrlStore = trustedUrlStore ?? NullTrustedUrlStore.Instance;
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

        if (TryParseSystemResource(normalizedMessage, originalMessage, out var systemResourceResult))
        {
            return Task.FromResult(systemResourceResult);
        }

        if (TryParseConfirmation(normalizedMessage, originalMessage, out var confirmationResult))
        {
            return Task.FromResult(confirmationResult);
        }

        if (TryParseDevVisualState(normalizedMessage, originalMessage, out var devVisualStateResult))
        {
            return Task.FromResult(devVisualStateResult);
        }

        if (TryParseDeleteBrowserMapping(normalizedMessage, originalMessage, out var deleteBrowserMappingResult))
        {
            return Task.FromResult(deleteBrowserMappingResult);
        }

        if (TryParseEditBrowserMapping(normalizedMessage, originalMessage, out var editBrowserMappingResult))
        {
            return Task.FromResult(editBrowserMappingResult);
        }

        if (TryParseTrustedUrl(normalizedMessage, originalMessage, out var trustedUrlResult))
        {
            return Task.FromResult(trustedUrlResult);
        }

        if (TryParseWakeMerlin(normalizedMessage, originalMessage, out var wakeResult))
        {
            return Task.FromResult(wakeResult);
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

    private static bool TryParseWakeMerlin(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (WakePhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = new IntentParseResult
            {
                Intent = "wake_merlin",
                NormalizedCommand = "wake_merlin",
                Confidence = 0.98,
                OriginalMessage = originalMessage,
                CapabilityId = "wake_merlin",
                CapabilityName = "Wake Merlin"
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

    private static bool TryParseSystemResource(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (CurrentTimePhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = SystemResourceResult(
                originalMessage,
                "system resource current_time",
                "system_time",
                "System Time");

            return true;
        }

        if (CurrentDatePhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = SystemResourceResult(
                originalMessage,
                "system resource current_date",
                "system_date",
                "System Date");

            return true;
        }

        if (TimeZonePhrases.Any(phrase => string.Equals(normalizedMessage, phrase, StringComparison.OrdinalIgnoreCase)))
        {
            result = SystemResourceResult(
                originalMessage,
                "system resource timezone",
                "system_timezone",
                "System Timezone");

            return true;
        }

        result = Unknown(originalMessage, normalizedMessage);
        return false;
    }

    private static IntentParseResult SystemResourceResult(
        string originalMessage,
        string normalizedCommand,
        string capabilityId,
        string capabilityName)
    {
        return new IntentParseResult
        {
            Intent = "system_resource_query",
            NormalizedCommand = normalizedCommand,
            Confidence = 0.98,
            OriginalMessage = originalMessage,
            CapabilityId = capabilityId,
            CapabilityName = capabilityName
        };
    }

    private static bool TryParseConfirmation(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        if (ConfirmationCommandMatcher.IsExplicitConfirmation(normalizedMessage)
            || ConfirmationCommandMatcher.IsCancellationCommand(normalizedMessage)
            || ConfirmationCommandMatcher.IsChoiceCommand(normalizedMessage))
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

    private static bool TryParseDevVisualState(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        result = Unknown(originalMessage, normalizedMessage);
        if (!DevVisualStateTool.TryParse(normalizedMessage, out _))
        {
            return false;
        }

        result = new IntentParseResult
        {
            Intent = "dev_visual_state",
            NormalizedCommand = normalizedMessage,
            Confidence = 0.99,
            OriginalMessage = originalMessage,
            CapabilityId = "dev_visual_state",
            CapabilityName = "Dev Visual State"
        };
        return true;
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

            if (!ContainsAny(normalizedMessage, ["open", "start", "launch", "pull up"]))
            {
                continue;
            }

            result = new IntentParseResult
            {
                Intent = "open_application",
                NormalizedCommand = $"open {application.Key}",
                Confidence = StartsWithAny(normalizedMessage, ["open ", "start ", "launch ", "pull up "]) ? 0.98 : 0.95,
                OriginalMessage = originalMessage,
                CapabilityId = "application_launch",
                CapabilityName = "Application Launch"
            };

            return true;
        }

        if (TryExtractOpenApplicationTarget(normalizedMessage, out var target))
        {
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

    private bool TryParseTrustedUrl(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        result = Unknown(originalMessage, normalizedMessage);

        foreach (var prefix in UrlPrefixes)
        {
            if (!normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = normalizedMessage[prefix.Length..].Trim();
            var mapping = _trustedUrlStore.FindByAlias(target);
            if (mapping is null)
            {
                return false;
            }

            result = new IntentParseResult
            {
                Intent = "open_url",
                NormalizedCommand = $"open {mapping.Url}",
                Confidence = 1.0,
                OriginalMessage = originalMessage,
                CapabilityId = "url_opening",
                CapabilityName = "URL Opening"
            };
            return true;
        }

        return false;
    }

    private static bool TryParseDeleteBrowserMapping(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        result = Unknown(originalMessage, normalizedMessage);
        foreach (var prefix in new[]
        {
            "delete browser mapping ",
            "please delete browser mapping ",
            "delete the browser mapping ",
            "please delete the browser mapping ",
            "delete the mapping of ",
            "please delete the mapping of ",
            "remove browser mapping ",
            "please remove browser mapping ",
            "remove the browser mapping ",
            "please remove the browser mapping ",
            "remove the mapping of ",
            "please remove the mapping of ",
            "forget ",
            "please forget ",
            "stop opening "
        })
        {
            if (!normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var alias = TrustedUrlStore.NormalizeAlias(normalizedMessage[prefix.Length..]);
            if (string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            result = new IntentParseResult
            {
                Intent = "delete_browser_mapping",
                NormalizedCommand = $"delete browser mapping {alias}",
                Confidence = 0.98,
                OriginalMessage = originalMessage,
                CapabilityId = "browser_mapping",
                CapabilityName = "Browser Mapping"
            };
            return true;
        }

        return false;
    }

    private static bool TryParseEditBrowserMapping(
        string normalizedMessage,
        string originalMessage,
        out IntentParseResult result)
    {
        result = Unknown(originalMessage, normalizedMessage);
        if (EditBrowserMappingTool.TryExtractEdit(normalizedMessage, out var alias, out var target))
        {
            result = new IntentParseResult
            {
                Intent = "edit_browser_mapping",
                NormalizedCommand = $"edit browser mapping {alias} to {target}",
                Confidence = 0.98,
                OriginalMessage = originalMessage,
                CapabilityId = "browser_mapping",
                CapabilityName = "Browser Mapping"
            };
            return true;
        }

        if (!EditBrowserMappingTool.TryExtractPendingEditAlias(normalizedMessage, out alias))
        {
            return false;
        }

        result = new IntentParseResult
        {
            Intent = "edit_browser_mapping",
            NormalizedCommand = $"edit browser mapping {alias}",
            Confidence = 0.98,
            OriginalMessage = originalMessage,
            CapabilityId = "browser_mapping",
            CapabilityName = "Browser Mapping"
        };
        return true;
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

    private static bool TryExtractOpenApplicationTarget(string normalizedMessage, out string target)
    {
        target = string.Empty;
        foreach (var prefix in new[]
        {
            "open ",
            "start ",
            "launch ",
            "pull up ",
            "please open ",
            "please start ",
            "please launch ",
            "please pull up ",
            "can you open ",
            "can you start ",
            "can you launch ",
            "can you pull up ",
            "can you please open ",
            "can you please start ",
            "can you please launch ",
            "can you please pull up ",
            "could you open ",
            "could you start ",
            "could you launch ",
            "could you pull up ",
            "could you please open ",
            "could you please start ",
            "could you please launch ",
            "could you please pull up "
        })
        {
            if (!normalizedMessage.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target = CleanPoliteTarget(normalizedMessage[prefix.Length..]);
            return !string.IsNullOrWhiteSpace(target) && !IsInformationFeedTarget(target);
        }

        return false;
    }

    private static bool IsInformationFeedTarget(string target)
    {
        return ContainsAny(target, ["newsfeed", "news feed"]);
    }

    private static string CleanPoliteTarget(string target)
    {
        var cleaned = target.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var suffix in new[]
        {
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

        foreach (var prefix in new[] { "the ", "my " })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
                break;
            }
        }

        return cleaned;
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
