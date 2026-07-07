using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BrowserWorkspace;

namespace Merlin.Backend.Tools;

public sealed class ConfirmationTool : ITool
{
    private const string IntentName = "confirmation";
    private readonly IConfirmationService _confirmationService;
    private readonly IProcessLauncher _processLauncher;
    private readonly ITrustedApplicationStore _trustedApplicationStore;
    private readonly ITrustedUrlStore _trustedUrlStore;
    private readonly IBrowserWorkspaceService? _browserWorkspaceService;
    private readonly ILogger<ConfirmationTool>? _logger;

    public ConfirmationTool(
        IConfirmationService confirmationService,
        IProcessLauncher processLauncher,
        ITrustedApplicationStore trustedApplicationStore,
        ITrustedUrlStore? trustedUrlStore = null,
        IBrowserWorkspaceService? browserWorkspaceService = null,
        ILogger<ConfirmationTool>? logger = null)
    {
        _confirmationService = confirmationService;
        _processLauncher = processLauncher;
        _trustedApplicationStore = trustedApplicationStore;
        _trustedUrlStore = trustedUrlStore ?? NullTrustedUrlStore.Instance;
        _browserWorkspaceService = browserWorkspaceService;
        _logger = logger;
    }

    public string Name => "Confirmation";

    public string Description => "Confirms and executes a pending safe action.";

    public IReadOnlyCollection<string> Examples { get; } = ["confirm", "yes", "approve"];

    public bool CanHandle(string command)
    {
        var normalizedCommand = command.Trim();
        return ConfirmationCommandMatcher.IsExplicitConfirmation(normalizedCommand)
            || ConfirmationCommandMatcher.IsCancellationCommand(normalizedCommand)
            || TryParseChoice(normalizedCommand, out _)
            || MatchesPendingCandidateName(normalizedCommand);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var normalizedCommand = command.Trim();
        if (ConfirmationCommandMatcher.IsCancellationCommand(normalizedCommand))
        {
            var cancelled = _confirmationService.ConsumeLatestPending();
            return new ToolResult
            {
                Success = cancelled is not null,
                Message = cancelled is null
                    ? "No pending confirmation."
                    : string.Equals(cancelled.Action, "browser_page_click", StringComparison.OrdinalIgnoreCase)
                        ? "Okay, I will not click it."
                        : "Okay, I will not open anything.",
                ErrorCode = cancelled is null ? "NO_PENDING_CONFIRMATION" : null,
                ToolName = Name,
                Intent = IntentName,
                ResponseType = cancelled is null ? "error" : "confirmation"
            };
        }

        var hasChoice = TryParseChoice(normalizedCommand, out var choiceNumber);
        if (hasChoice || !ConfirmationCommandMatcher.IsExplicitConfirmation(normalizedCommand))
        {
            var selectedConfirmation = hasChoice && choiceNumber.HasValue
                ? _confirmationService.SelectChoice(choiceNumber.Value)
                : _confirmationService.SelectCandidateName(normalizedCommand);
            if (selectedConfirmation is null)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = hasChoice ? "Invalid confirmation choice." : "No pending confirmation.",
                    ErrorCode = hasChoice ? "INVALID_CONFIRMATION_CHOICE" : "NO_PENDING_CONFIRMATION",
                    ToolName = Name,
                    Intent = IntentName
                };
            }

            return new ToolResult
            {
                Success = false,
                Message = $"You selected {selectedConfirmation.DisplayName}. Please confirm before I open it.",
                ErrorCode = "CONFIRMATION_REQUIRED",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "confirmation",
                Confirmation = selectedConfirmation,
                ApplicationCandidates = selectedConfirmation.Candidates
            };
        }

        var pendingConfirmation = _confirmationService.GetLatestPending();
        if (pendingConfirmation is not null
            && string.IsNullOrWhiteSpace(pendingConfirmation.Target)
            && pendingConfirmation.Candidates.Count > 1)
        {
            return new ToolResult
            {
                Success = false,
                Message = "Please choose which app you want to open before confirming.",
                ErrorCode = "AMBIGUOUS_APPLICATION",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "confirmation",
                Confirmation = pendingConfirmation,
                ApplicationCandidates = pendingConfirmation.Candidates
            };
        }

        var confirmation = _confirmationService.ConsumeLatestPending();

        if (confirmation is null)
        {
            return new ToolResult
            {
                Success = false,
                Message = "No pending confirmation.",
                ErrorCode = "NO_PENDING_CONFIRMATION",
                ToolName = Name,
                Intent = IntentName
            };
        }

        if (string.Equals(confirmation.Action, "browser_page_click", StringComparison.OrdinalIgnoreCase))
        {
            if (_browserWorkspaceService is null || confirmation.BrowserPage is null)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = "I can't do that automatically.",
                    ErrorCode = "BROWSER_CONFIRMATION_UNAVAILABLE",
                    ToolName = Name,
                    Intent = IntentName
                };
            }

            var result = await _browserWorkspaceService.ConfirmBrowserPageClickAsync(
                confirmation.BrowserPage,
                cancellationToken);
            if (!result.Success)
            {
                var message = result.ErrorCode switch
                {
                    "confirmation_stale" => "That page changed, so I won't click it.",
                    "unsafe_action_blocked" => "I can't do that automatically.",
                    "browser_inactive" => "The browser is not open.",
                    _ => "I could not click that."
                };
                _logger?.LogInformation(
                    "BrowserPageConfirmationRejected ElementId: {ElementId}. ErrorCode: {ErrorCode}.",
                    confirmation.BrowserPage.ElementId,
                    result.ErrorCode);
                return new ToolResult
                {
                    Success = false,
                    Message = message,
                    ErrorCode = result.ErrorCode?.ToUpperInvariant() ?? "BROWSER_PAGE_CONFIRMATION_FAILED",
                    ToolName = Name,
                    Intent = "browser_workspace_page_click",
                    ResponseType = "error"
                };
            }

            return new ToolResult
            {
                Success = true,
                Message = string.IsNullOrWhiteSpace(result.ElementHref) ? "Clicked." : "Opening.",
                ToolName = Name,
                Intent = "browser_workspace_page_click",
                ResponseType = "confirmation"
            };
        }

        if (string.Equals(confirmation.Action, "open_url", StringComparison.OrdinalIgnoreCase)
            || string.Equals(confirmation.Action, "open_url_fallback", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (_browserWorkspaceService is not null)
                {
                    _logger?.LogInformation(
                        "WebsiteFallbackConfirmedToBrowserWorkspace Action: {Action}. Url: {Url}. Alias: {Alias}.",
                        confirmation.Action,
                        confirmation.Target,
                        confirmation.RequestedAlias);
                    await _browserWorkspaceService.OpenAsync(null, cancellationToken);
                    await _browserWorkspaceService.NavigateAsync(confirmation.Target, cancellationToken);
                }
                else
                {
                    await _processLauncher.LaunchAsync(confirmation.Target, cancellationToken);
                }

                if (string.Equals(confirmation.Action, "open_url_fallback", StringComparison.OrdinalIgnoreCase))
                {
                    _trustedUrlStore.SaveMapping(
                        confirmation.RequestedAlias,
                        confirmation.Target,
                        confirmation.DisplayName);
                }

                return new ToolResult
                {
                    Success = true,
                    Message = $"Opening {confirmation.DisplayName}...",
                    SpokenText = ToolSpeechTemplates.UrlOpenSuccess,
                    SpeechCacheKey = "tool.url.open.success.generic",
                    PreferPhraseCache = true,
                    IsReplayableSpeech = true,
                    ToolName = Name,
                    Intent = "open_url",
                    ResponseType = "confirmation"
                };
            }
            catch (Exception exception)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = $"Failed to execute confirmation: {exception.Message}",
                    ErrorCode = "TOOL_EXECUTION_FAILED",
                    ToolName = Name,
                    Intent = IntentName
                };
            }
        }

        if (!string.Equals(confirmation.Action, "open_application", StringComparison.OrdinalIgnoreCase))
        {
            return new ToolResult
            {
                Success = false,
                Message = "Unsupported confirmation action.",
                ErrorCode = "UNKNOWN_COMMAND",
                ToolName = Name,
                Intent = IntentName
            };
        }

        try
        {
            await _processLauncher.LaunchAsync(confirmation.Target, cancellationToken);
            _trustedApplicationStore.SaveMapping(
                confirmation.RequestedAlias,
                new ApplicationCandidate
                {
                    DisplayName = confirmation.DisplayName,
                    ExecutablePath = confirmation.Target,
                    Source = "Trusted",
                    Confidence = 1
                });

            return new ToolResult
            {
                Success = true,
                Message = $"Opening {confirmation.DisplayName}...",
                SpokenText = ToolSpeechTemplates.AppOpenSuccess,
                SpeechCacheKey = "tool.app.open.success.generic",
                PreferPhraseCache = true,
                IsReplayableSpeech = true,
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "confirmation"
            };
        }
        catch (Exception exception)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Failed to execute confirmation: {exception.Message}",
                ErrorCode = "TOOL_EXECUTION_FAILED",
                ToolName = Name,
                Intent = IntentName
            };
        }
    }

    private static bool TryParseChoice(string command, out int? choiceNumber)
    {
        choiceNumber = null;

        if (command.StartsWith("choose ", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(command["choose ".Length..].Trim(), out var parsedChoice))
        {
            choiceNumber = parsedChoice;
            return true;
        }

        if (string.Equals(command, "first one", StringComparison.OrdinalIgnoreCase))
        {
            choiceNumber = 1;
            return true;
        }

        if (string.Equals(command, "second one", StringComparison.OrdinalIgnoreCase))
        {
            choiceNumber = 2;
            return true;
        }

        return false;
    }

    private bool MatchesPendingCandidateName(string command)
    {
        var pending = _confirmationService.GetLatestPending();
        return pending is not null
            && pending.Candidates.Any(candidate => string.Equals(
                NormalizeName(candidate.DisplayName),
                NormalizeName(command),
                StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeName(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
