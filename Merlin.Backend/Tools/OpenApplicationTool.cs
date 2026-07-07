using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BrowserWorkspace;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Tools;

public sealed class OpenApplicationTool : ITool
{
    private const string IntentName = "open_application";

    private static readonly string[] SupportedVerbs = ["open", "start", "launch", "pull up"];
    private readonly IApplicationResolver _applicationResolver;
    private readonly IConfirmationService _confirmationService;
    private readonly ApplicationLaunchOptions _options;
    private readonly IProcessLauncher _processLauncher;
    private readonly ITrustedUrlStore _trustedUrlStore;
    private readonly IBrowserWorkspaceService? _browserWorkspaceService;
    private readonly ILogger<OpenApplicationTool>? _logger;

    public OpenApplicationTool(
        IOptions<ApplicationLaunchOptions> options,
        IApplicationResolver applicationResolver,
        IConfirmationService confirmationService,
        IProcessLauncher processLauncher,
        ITrustedUrlStore? trustedUrlStore = null,
        IBrowserWorkspaceService? browserWorkspaceService = null,
        ILogger<OpenApplicationTool>? logger = null)
    {
        _options = options.Value;
        _applicationResolver = applicationResolver;
        _confirmationService = confirmationService;
        _processLauncher = processLauncher;
        _trustedUrlStore = trustedUrlStore ?? NullTrustedUrlStore.Instance;
        _browserWorkspaceService = browserWorkspaceService;
        _logger = logger;
    }

    public string Name => "Open Application";

    public string Description => "Opens allowlisted local applications.";

    public IReadOnlyCollection<string> Examples => BuildExamples();

    public bool CanHandle(string command)
    {
        return TryExtractApplicationName(command, out var applicationName)
            && !LooksLikeUrlOrPath(applicationName);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            new ToolExecutionContext
            {
                OriginalMessage = command,
                NormalizedCommand = command,
                Intent = IntentName
            },
            cancellationToken);
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var command = context.NormalizedCommand;
        if (!TryExtractApplicationName(command, out var applicationName))
        {
            return new ToolResult
            {
                Success = false,
                Message = "Unknown command.",
                ErrorCode = "UNKNOWN_COMMAND",
                ToolName = Name,
                Intent = IntentName
            };
        }

        if (TryFindConfiguredApplication(applicationName, out var application))
        {
            try
            {
                await _processLauncher.LaunchAsync(application.FileName, cancellationToken);

                return new ToolResult
                {
                    Success = true,
                    Message = application.SuccessMessage,
                    ToolName = Name,
                    Intent = IntentName
                };
            }
            catch (Exception exception)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = $"Failed to open {application.DisplayName}: {exception.Message}",
                    ErrorCode = "TOOL_EXECUTION_FAILED",
                    ToolName = Name,
                    Intent = IntentName
                };
            }
        }

        var trustedUrl = _trustedUrlStore.FindByAlias(applicationName);
        if (trustedUrl is not null)
        {
            try
            {
                if (_browserWorkspaceService is not null)
                {
                    _logger?.LogInformation(
                        "WebDestinationSkippedNativeAppRoute Alias: {Alias}. Url: {Url}. Source: OpenApplicationToolTrustedUrlFallback.",
                        applicationName,
                        trustedUrl.Url);
                    await _browserWorkspaceService.OpenAsync(null, cancellationToken);
                    await _browserWorkspaceService.NavigateAsync(trustedUrl.Url, cancellationToken);
                }
                else
                {
                    await _processLauncher.LaunchAsync(trustedUrl.Url, cancellationToken);
                }

                return new ToolResult
                {
                    Success = true,
                    Message = $"Opening {trustedUrl.DisplayName}...",
                    ToolName = "Open URL",
                    Intent = "open_url",
                    ResponseType = "assistant"
                };
            }
            catch (Exception exception)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = $"Failed to open URL: {exception.Message}",
                    ErrorCode = "TOOL_EXECUTION_FAILED",
                    ToolName = "Open URL",
                    Intent = "open_url",
                    ResponseType = "error"
                };
            }
        }

        var resolution = await _applicationResolver.ResolveAsync(applicationName, cancellationToken);
        var candidate = resolution.Candidates.OrderByDescending(item => item.Confidence).FirstOrDefault();
        if (!resolution.Found || candidate is null)
        {
            if (TryCreateBrowserFallbackConfirmation(applicationName, context, out var browserConfirmation))
            {
                return new ToolResult
                {
                    Success = false,
                    Message = $"I couldn't find an app called {applicationName}. Should I open {browserConfirmation.DisplayName} as a website instead?",
                    SpokenText = ToolSpeechTemplates.BrowserFallbackConfirmation,
                    SpeechCacheKey = "tool.browser.fallback.confirmation",
                    PreferPhraseCache = true,
                    IsReplayableSpeech = true,
                    ErrorCode = "CONFIRMATION_REQUIRED",
                    ToolName = Name,
                    Intent = IntentName,
                    ResponseType = "confirmation",
                    Confirmation = browserConfirmation
                };
            }

            return new ToolResult
            {
                Success = false,
                Message = "Unknown command.",
                ErrorCode = "UNKNOWN_COMMAND",
                ToolName = Name,
                Intent = IntentName
            };
        }

        if (!resolution.RequiresConfirmation)
        {
            try
            {
                await _processLauncher.LaunchAsync(candidate.ExecutablePath, cancellationToken);

                return new ToolResult
                {
                    Success = true,
                    Message = $"Opening {candidate.DisplayName}...",
                    ToolName = Name,
                    Intent = IntentName
                };
            }
            catch (Exception exception)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = $"Failed to open {candidate.DisplayName}: {exception.Message}",
                    ErrorCode = "TOOL_EXECUTION_FAILED",
                    ToolName = Name,
                    Intent = IntentName
                };
            }
        }

        if (resolution.IsAmbiguous)
        {
            var candidateNames = string.Join(
                ", ",
                resolution.Candidates.Take(3).Select(candidate => candidate.DisplayName));
            var ambiguousConfirmation = _confirmationService.Create(
                IntentName,
                string.Empty,
                applicationName,
                applicationName,
                context.OriginalMessage,
                context.Intent ?? IntentName,
                context.NormalizedCommand,
                Name,
                resolution.Candidates.ToArray());

            return new ToolResult
            {
                Success = false,
                Message = $"I found multiple apps matching that description, sir: {candidateNames}. Please choose which app you want to open.",
                ErrorCode = "AMBIGUOUS_APPLICATION",
                ToolName = Name,
                Intent = IntentName,
                ResponseType = "confirmation",
                Confirmation = ambiguousConfirmation,
                ApplicationCandidates = resolution.Candidates.ToArray()
            };
        }

        if (!resolution.RequiresConfirmation)
        {
            try
            {
                await _processLauncher.LaunchAsync(candidate.ExecutablePath, cancellationToken);

                return new ToolResult
                {
                    Success = true,
                    Message = $"Opening {candidate.DisplayName}...",
                    ToolName = Name,
                    Intent = IntentName
                };
            }
            catch (Exception exception)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = $"Failed to open {candidate.DisplayName}: {exception.Message}",
                    ErrorCode = "TOOL_EXECUTION_FAILED",
                    ToolName = Name,
                    Intent = IntentName
                };
            }
        }

        var confirmation = _confirmationService.Create(
            IntentName,
            candidate.ExecutablePath,
            candidate.DisplayName,
            applicationName,
            context.OriginalMessage,
            context.Intent ?? IntentName,
            context.NormalizedCommand,
            Name,
            [candidate]);

        return new ToolResult
        {
            Success = false,
            Message = $"You asked me to open {applicationName}. I found {candidate.DisplayName}, but I have not handled this specific application before. Please confirm before I open it.",
            ErrorCode = "CONFIRMATION_REQUIRED",
            ToolName = Name,
            Intent = IntentName,
            ResponseType = "confirmation",
            Confirmation = confirmation,
            ApplicationCandidates = resolution.Candidates.ToArray()
        };
    }

    private static bool TryExtractApplicationName(string command, out string applicationName)
    {
        applicationName = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var normalizedCommand = command.Trim();

        foreach (var verb in SupportedVerbs)
        {
            var prefix = $"{verb} ";

            if (!normalizedCommand.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            applicationName = CleanApplicationName(normalizedCommand[prefix.Length..]);
            return !string.IsNullOrWhiteSpace(applicationName);
        }

        return false;
    }

    private static string CleanApplicationName(string applicationName)
    {
        var cleaned = applicationName.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
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

    private bool TryFindConfiguredApplication(string applicationName, out ApplicationDefinition application)
    {
        foreach (var configuredApplication in _options.Applications)
        {
            if (string.Equals(applicationName, configuredApplication.Key, StringComparison.OrdinalIgnoreCase)
                || configuredApplication.Value.Aliases.Any(alias =>
                    string.Equals(applicationName, alias, StringComparison.OrdinalIgnoreCase)))
            {
                application = new ApplicationDefinition(
                    configuredApplication.Value.DisplayName,
                    configuredApplication.Value.ExecutableOrUrl,
                    $"Opening {configuredApplication.Value.DisplayName}...");

                return true;
            }
        }

        application = default;
        return false;
    }

    private static bool LooksLikeUrlOrPath(string value)
    {
        return value.Contains('.')
            || value.Contains(':')
            || value.Contains('\\')
            || value.Contains('/');
    }

    private bool TryCreateBrowserFallbackConfirmation(
        string applicationName,
        ToolExecutionContext context,
        out PendingConfirmation confirmation)
    {
        confirmation = null!;
        if (!OpenUrlTool.TryNormalizeBrowserTarget(applicationName, out var browserTarget, allowBareTarget: true))
        {
            return false;
        }

        var normalizedUrl = OpenUrlTool.NormalizeUrl(browserTarget);
        if (!normalizedUrl.Success)
        {
            return false;
        }

        confirmation = _confirmationService.Create(
            "open_url_fallback",
            normalizedUrl.Url,
            browserTarget,
            applicationName,
            context.OriginalMessage,
            "open_url",
            $"open {browserTarget}",
            "Open URL");
        return true;
    }

    private IReadOnlyCollection<string> BuildExamples()
    {
        var examples = _options.Applications
            .Take(5)
            .Select(application => $"open {application.Key}")
            .ToArray();

        return examples.Length > 0
            ? examples
            : ["open notepad", "open calculator"];
    }

    private readonly record struct ApplicationDefinition(
        string DisplayName,
        string FileName,
        string SuccessMessage);
}
