using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class ConfirmationTool : ITool
{
    private const string IntentName = "confirmation";
    private readonly IConfirmationService _confirmationService;
    private readonly IProcessLauncher _processLauncher;
    private readonly ITrustedApplicationStore _trustedApplicationStore;
    private readonly ITrustedCommandStore _trustedCommandStore;

    public ConfirmationTool(
        IConfirmationService confirmationService,
        IProcessLauncher processLauncher,
        ITrustedApplicationStore trustedApplicationStore,
        ITrustedCommandStore trustedCommandStore)
    {
        _confirmationService = confirmationService;
        _processLauncher = processLauncher;
        _trustedApplicationStore = trustedApplicationStore;
        _trustedCommandStore = trustedCommandStore;
    }

    public string Name => "Confirmation";

    public string Description => "Confirms and executes a pending safe action.";

    public IReadOnlyCollection<string> Examples { get; } = ["confirm", "yes", "approve"];

    public bool CanHandle(string command)
    {
        var normalizedCommand = command.Trim();
        return string.Equals(normalizedCommand, "confirm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedCommand, "approve", StringComparison.OrdinalIgnoreCase)
            || TryParseChoice(normalizedCommand, out _);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var hasChoice = TryParseChoice(command.Trim(), out var choiceNumber);
        var confirmation = hasChoice && choiceNumber.HasValue
            ? _confirmationService.ConsumeChoice(choiceNumber.Value)
            : _confirmationService.ConsumeLatestPending();

        if (confirmation is null)
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
            _trustedCommandStore.SaveMapping(new TrustedCommandMapping
            {
                OriginalCommand = confirmation.OriginalUserCommand,
                Intent = confirmation.Intent,
                NormalizedCommand = confirmation.NormalizedCommand,
                ToolName = confirmation.ToolName,
                Target = confirmation.Target,
                DisplayName = confirmation.DisplayName,
                UseCount = 1
            });

            return new ToolResult
            {
                Success = true,
                Message = $"Opening {confirmation.DisplayName}...",
                ToolName = Name,
                Intent = IntentName
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
}
