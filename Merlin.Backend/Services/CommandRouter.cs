using Merlin.Backend.Models;
using Merlin.Backend.Configuration;
using Merlin.Backend.Services.Acknowledgement;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Merlin.Backend.Services;

public sealed class CommandRouter
{
    private readonly IAcknowledgementPolicy? _acknowledgementPolicy;
    private readonly IAcknowledgementSpeechService? _acknowledgementSpeechService;
    private readonly IIntentParser _intentParser;
    private readonly LlmOptions? _llmOptions;
    private readonly ILogger<CommandRouter> _logger;
    private readonly IAssistantResponsePresentationFormatter? _presentationFormatter;
    private readonly IRequestProgressSpeechService? _progressSpeechService;
    private readonly IResponsePolisher _responsePolisher;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly SpeechCommandNormalizer _speechCommandNormalizer;
    private readonly ToolRegistry _toolRegistry;

    public CommandRouter(
        IIntentParser intentParser,
        ToolRegistry toolRegistry,
        ILogger<CommandRouter> logger,
        IRuntimeStateService runtimeStateService,
        IResponsePolisher responsePolisher,
        SpeechCommandNormalizer? speechCommandNormalizer = null,
        IAssistantResponsePresentationFormatter? presentationFormatter = null,
        IAcknowledgementPolicy? acknowledgementPolicy = null,
        IAcknowledgementSpeechService? acknowledgementSpeechService = null,
        IRequestProgressSpeechService? progressSpeechService = null,
        IOptions<LlmOptions>? llmOptions = null)
    {
        _acknowledgementPolicy = acknowledgementPolicy;
        _acknowledgementSpeechService = acknowledgementSpeechService;
        _intentParser = intentParser;
        _llmOptions = llmOptions?.Value;
        _logger = logger;
        _presentationFormatter = presentationFormatter;
        _progressSpeechService = progressSpeechService;
        _responsePolisher = responsePolisher;
        _runtimeStateService = runtimeStateService;
        _speechCommandNormalizer = speechCommandNormalizer ?? new SpeechCommandNormalizer();
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
        var receivedAtUtc = request.ReceivedAtUtc ?? DateTimeOffset.UtcNow;
        var correlationId = GetOrCreateCorrelationId(request.CorrelationId);
        var requestId = correlationId;
        var rawMessage = request.Message;
        var shouldNormalizeSpeech = ShouldNormalizeSpeech(request);
        var message = shouldNormalizeSpeech
            ? _speechCommandNormalizer.Normalize(rawMessage)
            : rawMessage;
        _runtimeStateService.IncrementRequestsProcessed();

        if (shouldNormalizeSpeech && !string.Equals(rawMessage, message, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Speech command normalized. CorrelationId: {CorrelationId}. Raw: {RawCommand}. Normalized: {NormalizedCommand}",
                correlationId,
                rawMessage,
                message);
        }

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

        var acknowledgementDecision = DecideAcknowledgement(
            request,
            message,
            intentResult,
            tool.Name,
            receivedAtUtc);

        using var pendingSpeechCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var progressHandle = StartProgressSpeech(
            request,
            requestId,
            correlationId,
            receivedAtUtc,
            acknowledgementDecision,
            pendingSpeechCancellation.Token);

        _ = StartInitialAcknowledgementAsync(
            request,
            requestId,
            correlationId,
            receivedAtUtc,
            acknowledgementDecision,
            pendingSpeechCancellation.Token);

        ToolResult result;
        var mainWorkStopwatch = Stopwatch.StartNew();
        try
        {
            result = await tool.ExecuteAsync(
                new ToolExecutionContext
                {
                    OriginalMessage = intentResult.OriginalMessage,
                    NormalizedCommand = intentResult.NormalizedCommand,
                    Intent = intentResult.Intent
                },
                cancellationToken);
            pendingSpeechCancellation.Cancel();
            progressHandle?.MarkMainResponseReady();
            mainWorkStopwatch.Stop();
            _logger.LogInformation(
                "Main response ready. CorrelationId: {CorrelationId}. ToolName: {ToolName}. ElapsedMs: {ElapsedMs}.",
                correlationId,
                tool.Name,
                mainWorkStopwatch.Elapsed.TotalMilliseconds);
            _logger.LogInformation(
                "Final answer waiting for active acknowledgement/progress playback to finish if one is already playing. CorrelationId: {CorrelationId}.",
                correlationId);
        }
        finally
        {
            if (progressHandle is not null)
            {
                await progressHandle.StopAsync();
            }
        }
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
            SpokenText = result.SpokenText,
            SpeechCacheKey = result.SpeechCacheKey,
            PreferPhraseCache = result.PreferPhraseCache,
            IsReplayableSpeech = result.IsReplayableSpeech,
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
            ApplicationCandidates = result.ApplicationCandidates,
            DevVisualFlow = result.DevVisualFlow
        }, cancellationToken);
    }

    private AcknowledgementDecision? DecideAcknowledgement(
        AssistantRequest request,
        string normalizedMessage,
        IntentParseResult intentResult,
        string toolName,
        DateTimeOffset receivedAtUtc)
    {
        if (_acknowledgementPolicy is null)
        {
            return null;
        }

        var context = new AcknowledgementContext
        {
            UserText = intentResult.OriginalMessage,
            NormalizedText = intentResult.NormalizedCommand,
            IntentDomain = intentResult.Intent,
            Capability = intentResult.CapabilityId ?? toolName,
            IsVoiceMode = IsVoiceMode(request),
            WillUseDeepInfra = WillUseDeepInfra(intentResult, toolName),
            WillUseExternalTool = WillUseExternalTool(intentResult, toolName),
            IsExpectedFastLocalTool = IsExpectedFastLocalTool(intentResult),
            IsMemorySave = IsExplicitMemorySave(normalizedMessage),
            IsMemorySearch = IsMemorySearch(normalizedMessage),
            Now = receivedAtUtc
        };

        var decision = _acknowledgementPolicy.Decide(context);
        if (decision.ShouldSpeakInitialAcknowledgement)
        {
            _logger.LogInformation(
                "Acknowledgement selected. CorrelationId: {CorrelationId}. Category: {Category}. PhraseId: {PhraseId}. Reason: {Reason}. FirstProgressAfterMs: {FirstProgressAfterMs}.",
                request.CorrelationId,
                decision.InitialCategory,
                decision.PhraseId,
                decision.Reason,
                decision.FirstProgressAfter.TotalMilliseconds);
        }
        else
        {
            _logger.LogInformation(
                "Acknowledgement skipped. CorrelationId: {CorrelationId}. Reason: {Reason}.",
                request.CorrelationId,
                decision.Reason);
        }

        return decision;
    }

    private Task StartInitialAcknowledgementAsync(
        AssistantRequest request,
        string requestId,
        string correlationId,
        DateTimeOffset receivedAtUtc,
        AcknowledgementDecision? decision,
        CancellationToken cancellationToken)
    {
        if (_acknowledgementSpeechService is null
            || decision is null
            || request.SpeechEventSender is null
            || !decision.ShouldSpeakInitialAcknowledgement)
        {
            return Task.CompletedTask;
        }

        return _acknowledgementSpeechService.SpeakInitialAcknowledgementAsync(
            new AcknowledgementPlaybackRequest
            {
                RequestId = requestId,
                CorrelationId = correlationId,
                CommandReceivedAtUtc = receivedAtUtc,
                Decision = decision,
                SendEventAsync = request.SpeechEventSender
            },
            cancellationToken);
    }

    private IRequestProgressSpeechHandle? StartProgressSpeech(
        AssistantRequest request,
        string requestId,
        string correlationId,
        DateTimeOffset receivedAtUtc,
        AcknowledgementDecision? decision,
        CancellationToken cancellationToken)
    {
        if (_progressSpeechService is null
            || decision is null
            || request.SpeechEventSender is null
            || !decision.ShouldSpeakInitialAcknowledgement)
        {
            return null;
        }

        return _progressSpeechService.Start(
            new RequestProgressSpeechRequest
            {
                RequestId = requestId,
                CorrelationId = correlationId,
                CommandReceivedAtUtc = receivedAtUtc,
                Decision = decision,
                SendEventAsync = request.SpeechEventSender
            },
            cancellationToken);
    }

    private async Task<AssistantResponse> PolishAsync(
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        var polishedMessage = await _responsePolisher.PolishMessageAsync(response, cancellationToken);
        var polishedResponse = new AssistantResponse
        {
            Success = response.Success,
            Message = polishedMessage,
            SpokenText = response.SpokenText,
            SpeechCacheKey = response.SpeechCacheKey,
            PreferPhraseCache = response.PreferPhraseCache,
            IsReplayableSpeech = response.IsReplayableSpeech,
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
            ApplicationCandidates = response.ApplicationCandidates,
            DevVisualFlow = response.DevVisualFlow
        };

        var presentation = _presentationFormatter?.Format(polishedResponse);
        if (presentation is null)
        {
            return polishedResponse;
        }

        return new AssistantResponse
        {
            Success = polishedResponse.Success,
            Message = presentation.DisplayText,
            SpokenText = presentation.SpokenText,
            SpeechCacheKey = presentation.CacheKey,
            PreferPhraseCache = presentation.PreferPhraseCache,
            IsReplayableSpeech = presentation.IsReplayable,
            CorrelationId = polishedResponse.CorrelationId,
            ErrorCode = polishedResponse.ErrorCode,
            ToolName = polishedResponse.ToolName,
            Intent = polishedResponse.Intent,
            IntentConfidence = polishedResponse.IntentConfidence,
            OriginalMessage = polishedResponse.OriginalMessage,
            ParserUsed = polishedResponse.ParserUsed,
            CapabilityId = polishedResponse.CapabilityId,
            CapabilityName = polishedResponse.CapabilityName,
            ResponseType = polishedResponse.ResponseType,
            AvailableTools = polishedResponse.AvailableTools,
            Diagnostics = polishedResponse.Diagnostics,
            Confirmation = polishedResponse.Confirmation,
            ApplicationCandidates = polishedResponse.ApplicationCandidates,
            DevVisualFlow = polishedResponse.DevVisualFlow
        };
    }

    private static string GetOrCreateCorrelationId(string? correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
    }

    private static bool ShouldNormalizeSpeech(AssistantRequest request)
    {
        return request.InteractionSource is not null
            && (string.Equals(request.InteractionSource, "voice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.InteractionSource, "voice_stream", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsVoiceMode(AssistantRequest request)
    {
        return ShouldNormalizeSpeech(request)
            || string.Equals(request.ClientMode, "voice", StringComparison.OrdinalIgnoreCase);
    }

    private bool WillUseDeepInfra(IntentParseResult intentResult, string toolName)
    {
        return string.Equals(toolName, "General Conversation", StringComparison.OrdinalIgnoreCase)
            && string.Equals(_llmOptions?.Provider, "deepinfra", StringComparison.OrdinalIgnoreCase);
    }

    private static bool WillUseExternalTool(IntentParseResult intentResult, string toolName)
    {
        return !string.Equals(toolName, "General Conversation", StringComparison.OrdinalIgnoreCase)
            && !IsExpectedFastLocalTool(intentResult);
    }

    private static bool IsExpectedFastLocalTool(IntentParseResult intentResult)
    {
        return string.Equals(intentResult.CapabilityId, "system_time", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.CapabilityId, "system_date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.CapabilityId, "system_timezone", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.NormalizedCommand, "system resource current_time", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.NormalizedCommand, "system resource current_date", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.NormalizedCommand, "system resource timezone", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExplicitMemorySave(string text)
    {
        return ContainsAny(text, "save into long-term memory", "save to long-term memory", "remember that", "please save", "store in memory");
    }

    private static bool IsMemorySearch(string text)
    {
        return ContainsAny(text, "what do you remember", "what did we decide", "remember about", "check memory", "look through memory");
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        return terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
