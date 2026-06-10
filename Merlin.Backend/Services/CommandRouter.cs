using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class CommandRouter
{
    private readonly IIntentParser _intentParser;
    private readonly ILogger<CommandRouter> _logger;
    private readonly IResponsePolisher _responsePolisher;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly ToolRegistry _toolRegistry;

    public CommandRouter(
        IIntentParser intentParser,
        ToolRegistry toolRegistry,
        ILogger<CommandRouter> logger,
        IRuntimeStateService runtimeStateService,
        IResponsePolisher responsePolisher)
    {
        _intentParser = intentParser;
        _logger = logger;
        _responsePolisher = responsePolisher;
        _runtimeStateService = runtimeStateService;
        _toolRegistry = toolRegistry;
    }

    public async Task<AssistantResponse> RouteAsync(string message, CancellationToken cancellationToken = default)
    {
        return await RouteAsync(new AssistantRequest { Message = message }, cancellationToken);
    }

    public async Task<AssistantResponse> RouteAsync(
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = GetOrCreateCorrelationId(request.CorrelationId);
        var message = request.Message;
        _runtimeStateService.IncrementRequestsProcessed();

        _logger.LogInformation(
            "Command received. CorrelationId: {CorrelationId}. RequestCount: {RequestCount}. Command: {Command}",
            correlationId,
            _runtimeStateService.TotalRequestsProcessed,
            message);

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogInformation(
                "Unknown command. CorrelationId: {CorrelationId}. Command: {Command}",
                correlationId,
                message);

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = "I couldn't understand that request.",
                CorrelationId = correlationId,
                ErrorCode = "UNKNOWN_INPUT",
                Intent = "unknown_input",
                OriginalMessage = message,
                ResponseType = "error"
            }, cancellationToken);
        }

        var intentResult = await _intentParser.ParseAsync(message, cancellationToken);

        if (string.Equals(intentResult.Intent, "unsupported_action", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.Intent, "missing_capability", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.Intent, "unknown_input", StringComparison.OrdinalIgnoreCase))
        {
            var errorCode = intentResult.Intent switch
            {
                "missing_capability" => "MISSING_CAPABILITY",
                "unknown_input" => "UNKNOWN_INPUT",
                _ => "UNSUPPORTED_ACTION"
            };

            var responseMessage = intentResult.Intent switch
            {
                "missing_capability" => "Missing capability.",
                "unknown_input" => "I couldn't understand that request.",
                _ => "Unsupported action."
            };

            var responseType = intentResult.Intent switch
            {
                "missing_capability" => "limitation",
                "unknown_input" => "error",
                _ => "safety"
            };

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = responseMessage,
                CorrelationId = correlationId,
                ErrorCode = errorCode,
                ToolName = "General Conversation",
                Intent = intentResult.Intent,
                IntentConfidence = intentResult.Confidence,
                OriginalMessage = intentResult.OriginalMessage,
                ParserUsed = intentResult.ParserUsed,
                CapabilityId = intentResult.CapabilityId,
                CapabilityName = intentResult.CapabilityName,
                ResponseType = responseType
            }, cancellationToken);
        }

        var tool = _toolRegistry.FindTool(intentResult.NormalizedCommand);

        if (tool is null)
        {
            _logger.LogInformation(
                "Unknown command. CorrelationId: {CorrelationId}. Intent: {Intent}. NormalizedCommand: {Command}",
                correlationId,
                intentResult.Intent,
                intentResult.NormalizedCommand);

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = "Unknown command.",
                CorrelationId = correlationId,
                ErrorCode = "UNKNOWN_COMMAND",
                Intent = intentResult.Intent,
                IntentConfidence = intentResult.Confidence,
                OriginalMessage = intentResult.OriginalMessage,
                ParserUsed = intentResult.ParserUsed,
                CapabilityId = intentResult.CapabilityId,
                CapabilityName = intentResult.CapabilityName,
                ResponseType = "error"
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Matched tool. CorrelationId: {CorrelationId}. Intent: {Intent}. ToolName: {ToolName}. Command: {Command}",
            correlationId,
            intentResult.Intent,
            tool.Name,
            intentResult.NormalizedCommand);

        var result = await tool.ExecuteAsync(
            new ToolExecutionContext
            {
                OriginalMessage = intentResult.OriginalMessage,
                NormalizedCommand = intentResult.NormalizedCommand,
                Intent = intentResult.Intent
            },
            cancellationToken);
        if (result.Success)
        {
            _runtimeStateService.IncrementSuccessfulToolExecutions();
            _logger.LogInformation(
                "Successful tool execution count incremented. Count: {Count}",
                _runtimeStateService.TotalSuccessfulToolExecutions);
        }
        else
        {
            _runtimeStateService.IncrementFailedToolExecutions();
            _logger.LogInformation(
                "Failed tool execution count incremented. Count: {Count}",
                _runtimeStateService.TotalFailedToolExecutions);
        }

        _logger.LogInformation(
            "Tool execution completed. CorrelationId: {CorrelationId}. ToolName: {ToolName}. Success: {Success}. ErrorCode: {ErrorCode}",
            correlationId,
            result.ToolName ?? tool.Name,
            result.Success,
            result.ErrorCode);

        return await PolishAsync(new AssistantResponse
        {
            Success = result.Success,
            Message = result.Message,
            CorrelationId = correlationId,
            ErrorCode = result.ErrorCode,
            ToolName = result.ToolName ?? tool.Name,
            Intent = result.Intent ?? intentResult.Intent,
            IntentConfidence = intentResult.Confidence,
            OriginalMessage = intentResult.OriginalMessage,
            ParserUsed = intentResult.ParserUsed,
            CapabilityId = result.CapabilityId ?? intentResult.CapabilityId,
            CapabilityName = result.CapabilityName ?? intentResult.CapabilityName,
            ResponseType = result.ResponseType ?? (result.Success ? "assistant" : "error"),
            AvailableTools = result.AvailableTools,
            Diagnostics = result.Diagnostics,
            Confirmation = result.Confirmation,
            ApplicationCandidates = result.ApplicationCandidates
        }, cancellationToken);
    }

    private async Task<AssistantResponse> PolishAsync(
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        var polishedMessage = await _responsePolisher.PolishMessageAsync(response, cancellationToken);
        return new AssistantResponse
        {
            Success = response.Success,
            Message = polishedMessage,
            CorrelationId = response.CorrelationId,
            ErrorCode = response.ErrorCode,
            ToolName = response.ToolName,
            Intent = response.Intent,
            IntentConfidence = response.IntentConfidence,
            OriginalMessage = response.OriginalMessage,
            ParserUsed = response.ParserUsed,
            CapabilityId = response.CapabilityId,
            CapabilityName = response.CapabilityName,
            ResponseType = response.ResponseType,
            AvailableTools = response.AvailableTools,
            Diagnostics = response.Diagnostics,
            Confirmation = response.Confirmation,
            ApplicationCandidates = response.ApplicationCandidates
        };
    }

    private static string GetOrCreateCorrelationId(string? correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
    }
}
