using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Tools;

public sealed class OpenApplicationTool : ITool
{
    private const string IntentName = "open_application";

    private static readonly string[] SupportedVerbs = ["open", "start", "launch"];
    private readonly IApplicationResolver _applicationResolver;
    private readonly IConfirmationService _confirmationService;
    private readonly ApplicationLaunchOptions _options;
    private readonly IProcessLauncher _processLauncher;

    public OpenApplicationTool(
        IOptions<ApplicationLaunchOptions> options,
        IApplicationResolver applicationResolver,
        IConfirmationService confirmationService,
        IProcessLauncher processLauncher)
    {
        _options = options.Value;
        _applicationResolver = applicationResolver;
        _confirmationService = confirmationService;
        _processLauncher = processLauncher;
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

        var resolution = await _applicationResolver.ResolveAsync(applicationName, cancellationToken);
        var candidate = resolution.Candidates.OrderByDescending(item => item.Confidence).FirstOrDefault();
        if (!resolution.Found || candidate is null)
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
                Message = resolution.Message,
                ErrorCode = "AMBIGUOUS_APPLICATION",
                ToolName = Name,
                Intent = IntentName,
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
            Message = $"I found {candidate.DisplayName} installed. Confirm before opening.",
            ErrorCode = "CONFIRMATION_REQUIRED",
            ToolName = Name,
            Intent = IntentName,
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

            applicationName = normalizedCommand[prefix.Length..].Trim();
            return !string.IsNullOrWhiteSpace(applicationName);
        }

        return false;
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
