using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.Tools;

public sealed class GeneralConversationTool : ITool
{
    private const string CommandPrefix = "chat ";
    private const string IntentName = "general_conversation";
    private readonly ILocalAIChatService _localAIChatService;

    public GeneralConversationTool(ILocalAIChatService localAIChatService)
    {
        _localAIChatService = localAIChatService;
    }

    public string Name => "General Conversation";

    public string Description => "Handles conversational interaction using the local AI model.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "tell me a joke",
        "who are you",
        "what can you do",
        "explain Merlin",
        "how do you work",
        "tell me something interesting"
    ];

    public bool CanHandle(string command)
    {
        return command.Trim().StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        var message = ExtractMessage(command);
        var result = await _localAIChatService.GenerateResponseAsync(message, cancellationToken);

        return new ToolResult
        {
            Success = result.Success,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            ToolName = Name,
            Intent = IntentName
        };
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var message = string.IsNullOrWhiteSpace(context.OriginalMessage)
            ? ExtractMessage(context.NormalizedCommand)
            : context.OriginalMessage;

        var result = await _localAIChatService.GenerateResponseAsync(message, cancellationToken);

        return new ToolResult
        {
            Success = result.Success,
            Message = result.Message,
            ErrorCode = result.ErrorCode,
            ToolName = Name,
            Intent = IntentName
        };
    }

    private static string ExtractMessage(string command)
    {
        var trimmedCommand = command.Trim();
        return trimmedCommand.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmedCommand[CommandPrefix.Length..].Trim()
            : trimmedCommand;
    }
}
