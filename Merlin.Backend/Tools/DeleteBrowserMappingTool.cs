using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class DeleteBrowserMappingTool : ITool
{
    private const string CommandPrefix = "delete browser mapping ";
    private const string IntentName = "delete_browser_mapping";
    private readonly ITrustedUrlStore _trustedUrlStore;

    public DeleteBrowserMappingTool(ITrustedUrlStore trustedUrlStore)
    {
        _trustedUrlStore = trustedUrlStore;
    }

    public string Name => "Delete Browser Mapping";

    public string Description => "Removes a remembered alias that maps a phrase to a browser URL.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "delete the mapping of terminal to the browser",
        "forget terminal as a website",
        "stop opening terminal in the browser"
    ];

    public bool CanHandle(string command)
    {
        return TryExtractAlias(command, out _);
    }

    public Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!TryExtractAlias(command, out var alias))
        {
            return Task.FromResult(new ToolResult
            {
                Success = false,
                Message = "I could not tell which browser mapping to delete.",
                ErrorCode = "UNKNOWN_COMMAND",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "error"
            });
        }

        var removed = _trustedUrlStore.DeleteMapping(alias);
        return Task.FromResult(new ToolResult
        {
            Success = removed,
            Message = removed
                ? $"I removed the browser mapping for {alias}."
                : $"I do not have a browser mapping saved for {alias}.",
            SpokenText = removed
                ? ToolSpeechTemplates.BrowserMappingRemoved
                : "I do not have that browser mapping saved.",
            SpeechCacheKey = removed ? "tool.browser.mapping.removed" : "tool.browser.mapping.not_found",
            PreferPhraseCache = true,
            IsReplayableSpeech = true,
            ErrorCode = removed ? null : "BROWSER_MAPPING_NOT_FOUND",
            ToolName = Name,
            Intent = IntentName,
            ResponseType = removed ? "assistant" : "error"
        });
    }

    private static bool TryExtractAlias(string command, out string alias)
    {
        alias = string.Empty;
        var normalized = Normalize(command);
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
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            alias = TrustedUrlStore.NormalizeAlias(normalized[prefix.Length..]);
            return !string.IsNullOrWhiteSpace(alias);
        }

        return false;
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
