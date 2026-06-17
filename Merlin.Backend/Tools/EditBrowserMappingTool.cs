using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class EditBrowserMappingTool : ITool
{
    private const string CommandPrefix = "edit browser mapping ";
    private const string IntentName = "edit_browser_mapping";
    private readonly ITrustedUrlStore _trustedUrlStore;
    private readonly IPendingInteractionService? _pendingInteractionService;

    public EditBrowserMappingTool(
        ITrustedUrlStore trustedUrlStore,
        IPendingInteractionService? pendingInteractionService = null)
    {
        _trustedUrlStore = trustedUrlStore;
        _pendingInteractionService = pendingInteractionService;
    }

    public string Name => "Edit Browser Mapping";

    public string Description => "Updates the URL saved for a remembered browser alias.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "edit the domain of terminal from .com to .co.uk",
        "change terminal browser mapping to terminal.nl",
        "update terminal website to terminal.co.uk"
    ];

    public bool CanHandle(string command)
    {
        return IsCancelPendingEditCommand(command)
            || TryExtractEdit(command, out _, out _)
            || TryExtractPendingEditAlias(command, out _);
    }

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        if (IsCancelPendingEditCommand(command))
        {
            _pendingInteractionService?.ConsumeLatestPending(PendingInteractionTypes.BrowserMappingEdit);
            return Task.FromResult(new ToolResult
            {
                Success = true,
                Message = "I cancelled that browser mapping edit.",
                SpokenText = ToolSpeechTemplates.BrowserMappingEditCancelled,
                SpeechCacheKey = "tool.browser.mapping.edit.cancelled",
                PreferPhraseCache = true,
                IsReplayableSpeech = true,
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "assistant"
            });
        }

        if (!TryExtractEdit(command, out var alias, out var target))
        {
            if (!TryExtractPendingEditAlias(command, out alias))
            {
                return Task.FromResult(new ToolResult
                {
                    Success = false,
                    Message = "I could not tell which browser mapping to edit.",
                    ErrorCode = "UNKNOWN_COMMAND",
                    ToolName = Name,
                    Intent = IntentName,
                    ResponseType = "error"
                });
            }

            return Task.FromResult(CreatePendingEdit(alias, command));
        }

        var existing = _trustedUrlStore.FindByAlias(alias);
        if (existing is null)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Message = $"I do not have a browser mapping saved for {alias}.",
                SpokenText = "I do not have that browser mapping saved.",
                SpeechCacheKey = "tool.browser.mapping.not_found",
                PreferPhraseCache = true,
                IsReplayableSpeech = true,
                ErrorCode = "BROWSER_MAPPING_NOT_FOUND",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            });
        }

        var newTarget = BuildReplacementTarget(alias, existing.Url, target);
        var normalizationResult = OpenUrlTool.NormalizeUrl(newTarget);
        if (!normalizationResult.Success)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Message = $"I could not save {newTarget} as a valid website.",
                SpokenText = "I couldn't save that as a valid website.",
                SpeechCacheKey = "tool.browser.mapping.invalid_url",
                PreferPhraseCache = true,
                IsReplayableSpeech = true,
                ErrorCode = normalizationResult.ErrorCode,
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            });
        }

        var displayName = GetDisplayName(normalizationResult.Url);
        var updated = _trustedUrlStore.UpdateMapping(alias, normalizationResult.Url, displayName);
        if (updated is null)
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Message = $"I do not have a browser mapping saved for {alias}.",
                SpokenText = "I do not have that browser mapping saved.",
                SpeechCacheKey = "tool.browser.mapping.not_found",
                PreferPhraseCache = true,
                IsReplayableSpeech = true,
                ErrorCode = "BROWSER_MAPPING_NOT_FOUND",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            });
        }

        return Task.FromResult(new ToolResult
        {
            Success = true,
            Message = $"I updated the browser mapping for {updated.Alias} to {updated.Url}.",
            SpokenText = ToolSpeechTemplates.BrowserMappingUpdated,
            SpeechCacheKey = "tool.browser.mapping.updated",
            PreferPhraseCache = true,
            IsReplayableSpeech = true,
            ToolName = Name,
            Intent = IntentName,
            ResponseType = "assistant"
        });
    }

    internal static bool TryExtractEdit(string command, out string alias, out string target)
    {
        alias = string.Empty;
        target = string.Empty;
        var normalized = Normalize(command);

        if (normalized.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TrySplitAliasAndTarget(normalized[CommandPrefix.Length..], out alias, out target);
        }

        foreach (var prefix in new[]
        {
            "edit the domain of ",
            "please edit the domain of ",
            "can you edit the domain of ",
            "can you please edit the domain of ",
            "edit domain of ",
            "please edit domain of ",
            "can you edit domain of ",
            "can you please edit domain of ",
            "change the domain of ",
            "please change the domain of ",
            "can you change the domain of ",
            "can you please change the domain of ",
            "change domain of ",
            "please change domain of ",
            "can you change domain of ",
            "can you please change domain of ",
            "update the domain of ",
            "please update the domain of ",
            "can you update the domain of ",
            "can you please update the domain of ",
            "update domain of ",
            "please update domain of ",
            "can you update domain of ",
            "can you please update domain of ",
            "edit the browser mapping of ",
            "please edit the browser mapping of ",
            "can you edit the browser mapping of ",
            "can you please edit the browser mapping of ",
            "edit browser mapping of ",
            "please edit browser mapping of ",
            "can you edit browser mapping of ",
            "can you please edit browser mapping of ",
            "change the browser mapping of ",
            "please change the browser mapping of ",
            "can you change the browser mapping of ",
            "can you please change the browser mapping of ",
            "change browser mapping of ",
            "please change browser mapping of ",
            "can you change browser mapping of ",
            "can you please change browser mapping of ",
            "update the browser mapping of ",
            "please update the browser mapping of ",
            "can you update the browser mapping of ",
            "can you please update the browser mapping of ",
            "update browser mapping of ",
            "please update browser mapping of ",
            "can you update browser mapping of ",
            "can you please update browser mapping of "
        })
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return TrySplitAliasAndTarget(normalized[prefix.Length..], out alias, out target);
        }

        foreach (var marker in new[]
        {
            "change ",
            "please change ",
            "can you change ",
            "can you please change ",
            "update ",
            "please update ",
            "can you update ",
            "can you please update ",
            "edit ",
            "please edit ",
            "can you edit ",
            "can you please edit "
        })
        {
            if (!normalized.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return TryExtractEdit(normalized[marker.Length..], out alias, out target);
        }

        foreach (var marker in new[]
        {
            " browser mapping to ",
            " website mapping to ",
            " mapping to ",
            " website to ",
            " browser to "
        })
        {
            var markerIndex = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex <= 0)
            {
                continue;
            }

            alias = TrustedUrlStore.NormalizeAlias(normalized[..markerIndex]);
            target = CleanTarget(normalized[(markerIndex + marker.Length)..]);
            return !string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(target);
        }

        return false;
    }

    internal static bool TryExtractPendingEditAlias(string command, out string alias)
    {
        alias = string.Empty;
        var normalized = Normalize(command);
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Contains(" to ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var prefix in new[]
        {
            CommandPrefix,
            "edit browser mapping of ",
            "edit the browser mapping of ",
            "please edit browser mapping of ",
            "please edit the browser mapping of ",
            "can you edit browser mapping of ",
            "can you edit the browser mapping of ",
            "can you please edit browser mapping of ",
            "can you please edit the browser mapping of ",
            "edit the browser mapping ",
            "please edit the browser mapping ",
            "can you edit the browser mapping ",
            "can you please edit the browser mapping ",
            "edit domain of ",
            "edit the domain of ",
            "please edit domain of ",
            "please edit the domain of ",
            "can you edit domain of ",
            "can you edit the domain of ",
            "can you please edit domain of ",
            "can you please edit the domain of ",
            "change domain of ",
            "change the domain of ",
            "please change domain of ",
            "please change the domain of ",
            "can you change domain of ",
            "can you change the domain of ",
            "can you please change domain of ",
            "can you please change the domain of ",
            "update domain of ",
            "update the domain of ",
            "please update domain of ",
            "please update the domain of ",
            "can you update domain of ",
            "can you update the domain of ",
            "can you please update domain of ",
            "can you please update the domain of "
        })
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            alias = TrustedUrlStore.NormalizeAlias(normalized[prefix.Length..]);
            return !string.IsNullOrWhiteSpace(alias);
        }

        return false;
    }

    private static bool TrySplitAliasAndTarget(string value, out string alias, out string target)
    {
        alias = string.Empty;
        target = string.Empty;

        var fromIndex = value.IndexOf(" from ", StringComparison.OrdinalIgnoreCase);
        var toIndex = value.LastIndexOf(" to ", StringComparison.OrdinalIgnoreCase);
        if (toIndex < 0)
        {
            return false;
        }

        var aliasEnd = fromIndex >= 0 && fromIndex < toIndex ? fromIndex : toIndex;
        alias = TrustedUrlStore.NormalizeAlias(value[..aliasEnd]);
        target = CleanTarget(value[(toIndex + " to ".Length)..]);
        return !string.IsNullOrWhiteSpace(alias) && !string.IsNullOrWhiteSpace(target);
    }

    private static string BuildReplacementTarget(string alias, string existingUrl, string target)
    {
        var cleanedTarget = CleanTarget(target);
        if (!cleanedTarget.StartsWith(".", StringComparison.Ordinal))
        {
            return cleanedTarget;
        }

        if (Uri.TryCreate(existingUrl, UriKind.Absolute, out var uri)
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            var hostParts = uri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries);
            var baseHost = hostParts.Length > 1 && string.Equals(hostParts[0], "www", StringComparison.OrdinalIgnoreCase)
                ? hostParts[1]
                : hostParts.FirstOrDefault() ?? TrustedUrlStore.NormalizeAlias(alias);
            return $"{baseHost}{cleanedTarget}";
        }

        return $"{TrustedUrlStore.NormalizeAlias(alias).Replace(' ', '-')}{cleanedTarget}";
    }

    private static string GetDisplayName(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host
            : url;
    }

    private static string CleanTarget(string value)
    {
        return value.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
    }

    private ToolResult CreatePendingEdit(string alias, string command)
    {
        var existing = _trustedUrlStore.FindByAlias(alias);
        if (existing is null)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"I do not have a browser mapping saved for {alias}.",
                SpokenText = "I do not have that browser mapping saved.",
                SpeechCacheKey = "tool.browser.mapping.not_found",
                PreferPhraseCache = true,
                IsReplayableSpeech = true,
                ErrorCode = "BROWSER_MAPPING_NOT_FOUND",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            };
        }

        if (_pendingInteractionService is null)
        {
            return new ToolResult
            {
                Success = false,
                Message = "I cannot wait for a follow-up browser mapping value right now.",
                SpokenText = "I couldn't wait for that follow-up right now.",
                ErrorCode = "PENDING_INTERACTION_UNAVAILABLE",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            };
        }

        _pendingInteractionService.Create(
            PendingInteractionTypes.BrowserMappingEdit,
            $"What should I change {existing.Alias} to?",
            new Dictionary<string, string>
            {
                ["alias"] = existing.Alias,
                ["existingUrl"] = existing.Url
            },
            command);

        return new ToolResult
        {
            Success = true,
            Message = $"Of course, sir. What should I change {existing.Alias} to?",
            SpokenText = ToolSpeechTemplates.BrowserMappingEditPrompt,
            SpeechCacheKey = "tool.browser.mapping.edit.prompt",
            PreferPhraseCache = true,
            IsReplayableSpeech = true,
            ToolName = Name,
            Intent = IntentName,
            ResponseType = "assistant"
        };
    }

    private static bool IsCancelPendingEditCommand(string command)
    {
        return string.Equals(Normalize(command), "cancel browser mapping edit", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        var trimmed = value.Trim().TrimEnd('!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
