using Merlin.Backend.Models;
using Merlin.Backend.Configuration;
using Merlin.Backend.Next.Host;
using Merlin.Backend.Services.Acknowledgement;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.Motion;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Services.Feedback;
using Merlin.Backend.Services.Motion;
using Merlin.Backend.Services.Vision;
using Merlin.Backend.Services.Web;
using Merlin.Backend.Tools;
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
    private readonly IFeedbackContextFactory? _feedbackContextFactory;
    private readonly IResponsiveFeedbackOrchestrator? _responsiveFeedbackOrchestrator;
    private readonly ResponsiveFeedbackOptions? _responsiveFeedbackOptions;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly SpeechCommandNormalizer _speechCommandNormalizer;
    private readonly ToolRegistry _toolRegistry;
    private readonly ILiveAssistantTurnService? _liveTurnService;
    private readonly UiControlModeController? _uiControlModeController;
    private readonly IVisionSidecarHost? _visionSidecarHost;
    private readonly IBrowserWorkspaceService? _browserWorkspaceService;
    private readonly BrowserMotionOverlayModeService? _browserMotionOverlayModeService;
    private readonly IWebDestinationParser? _webDestinationParser;
    private readonly IActiveSurfaceService? _activeSurfaceService;
    private readonly IMotionControlModeService? _motionControlModeService;
    private readonly ILegacyMerlinRequestAdapter? _merlinNextRequestAdapter;
    private readonly IMerlinNextShadowBridge? _merlinNextShadowBridge;

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
        IOptions<LlmOptions>? llmOptions = null,
        ILiveAssistantTurnService? liveTurnService = null,
        IFeedbackContextFactory? feedbackContextFactory = null,
        IResponsiveFeedbackOrchestrator? responsiveFeedbackOrchestrator = null,
        IOptions<ResponsiveFeedbackOptions>? responsiveFeedbackOptions = null,
        UiControlModeController? uiControlModeController = null,
        IVisionSidecarHost? visionSidecarHost = null,
        IBrowserWorkspaceService? browserWorkspaceService = null,
        BrowserMotionOverlayModeService? browserMotionOverlayModeService = null,
        IWebDestinationParser? webDestinationParser = null,
        IActiveSurfaceService? activeSurfaceService = null,
        IMotionControlModeService? motionControlModeService = null,
        ILegacyMerlinRequestAdapter? merlinNextRequestAdapter = null,
        IMerlinNextShadowBridge? merlinNextShadowBridge = null)
    {
        _acknowledgementPolicy = acknowledgementPolicy;
        _acknowledgementSpeechService = acknowledgementSpeechService;
        _intentParser = intentParser;
        _llmOptions = llmOptions?.Value;
        _logger = logger;
        _presentationFormatter = presentationFormatter;
        _progressSpeechService = progressSpeechService;
        _responsePolisher = responsePolisher;
        _feedbackContextFactory = feedbackContextFactory;
        _responsiveFeedbackOrchestrator = responsiveFeedbackOrchestrator;
        _responsiveFeedbackOptions = responsiveFeedbackOptions?.Value;
        _runtimeStateService = runtimeStateService;
        _speechCommandNormalizer = speechCommandNormalizer ?? new SpeechCommandNormalizer();
        _toolRegistry = toolRegistry;
        _liveTurnService = liveTurnService;
        _uiControlModeController = uiControlModeController;
        _visionSidecarHost = visionSidecarHost;
        _browserWorkspaceService = browserWorkspaceService;
        _browserMotionOverlayModeService = browserMotionOverlayModeService;
        _webDestinationParser = webDestinationParser;
        _activeSurfaceService = activeSurfaceService;
        _motionControlModeService = motionControlModeService;
        _merlinNextRequestAdapter = merlinNextRequestAdapter;
        _merlinNextShadowBridge = merlinNextShadowBridge;
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
        var captureId = request.CaptureId;
        var requestId = correlationId;
        var rawMessage = request.Message;
        var activeSurface = request.ActiveSurface
            ?? _activeSurfaceService?.Current
            ?? KnownSurfaces.Dashboard(DateTimeOffset.UtcNow);
        var shouldNormalizeSpeech = ShouldNormalizeSpeech(request);
        var message = shouldNormalizeSpeech
            ? _speechCommandNormalizer.Normalize(rawMessage)
            : rawMessage;
        _runtimeStateService.IncrementRequestsProcessed();

        if (shouldNormalizeSpeech && !string.Equals(rawMessage, message, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Speech command normalized. CaptureId: {CaptureId}. CorrelationId: {CorrelationId}. Raw: {RawCommand}. Normalized: {NormalizedCommand}",
                captureId,
                correlationId,
                rawMessage,
                message);
        }

        _logger.LogInformation(
            "Command received. CaptureId: {CaptureId}. CorrelationId: {CorrelationId}. RequestCount: {RequestCount}. Command: {Command}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. ActiveSurfaceSource: {ActiveSurfaceSource}. ActiveSurfaceConfidence: {ActiveSurfaceConfidence}.",
            captureId,
            correlationId,
            _runtimeStateService.TotalRequestsProcessed,
            message,
            activeSurface.Kind,
            activeSurface.SurfaceId,
            activeSurface.Source,
            activeSurface.Confidence);
        if (!string.IsNullOrWhiteSpace(captureId))
        {
            _logger.LogInformation(
                "CommandRouterVoiceInputReceived. CaptureId: {CaptureId}. RawText: {RawText}. NormalizedText: {NormalizedText}. CorrelationId: {CorrelationId}. Source: {Source}.",
                captureId,
                rawMessage,
                message,
                correlationId,
                request.InteractionSource);
        }

        TryStartMerlinNextShadow(
            request,
            requestId,
            message,
            activeSurface,
            receivedAtUtc);

        SetTurnState(correlationId, LiveAssistantTurnState.Interpreting);
        var feedbackContext = _feedbackContextFactory?.CreateInitial(
            request,
            correlationId,
            message,
            receivedAtUtc);

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
                CaptureId = captureId,
                ErrorCode = "UNKNOWN_INPUT",
                Intent = "unknown_input",
                OriginalMessage = message,
                ResponseType = "error"
            }, cancellationToken);
        }

        if (ChatLogCommandMatcher.TryMatch(message, out var chatLogAction))
        {
            var chatLogIntentResult = ChatLogCommandMatcher.ToIntentParseResult(
                chatLogAction,
                rawMessage,
                message);
            _runtimeStateService.RecordIntentParserUsed(nameof(ChatLogCommandMatcher), chatLogIntentResult.Intent);
            _logger.LogInformation(
                "Chat panel UI command matched. CorrelationId: {CorrelationId}. Action: {Action}. Command: {Command}",
                correlationId,
                chatLogAction,
                message);

            return await PolishAsync(new AssistantResponse
            {
                Success = true,
                Message = chatLogAction == ChatLogCommandAction.Show
                    ? "Opening chat."
                    : "Closing chat.",
                SpokenText = chatLogAction == ChatLogCommandAction.Show
                    ? "Opening chat."
                    : "Closing chat.",
                CorrelationId = correlationId,
                CaptureId = captureId,
                ToolName = "Chat Panel",
                Intent = chatLogIntentResult.Intent,
                IntentConfidence = chatLogIntentResult.Confidence,
                OriginalMessage = chatLogIntentResult.OriginalMessage,
                ParserUsed = chatLogIntentResult.ParserUsed,
                CapabilityId = chatLogIntentResult.CapabilityId,
                CapabilityName = chatLogIntentResult.CapabilityName,
                ResponseType = "system"
            }, cancellationToken);
        }

        var webDestinationCommand = _webDestinationParser?.TryParse(message, activeSurface);
        if (webDestinationCommand is not null)
        {
            return await HandleWebDestinationCommandAsync(
                webDestinationCommand,
                rawMessage,
                message,
                activeSurface,
                correlationId,
                captureId,
                cancellationToken);
        }

        if (UiControlModeCommandMatcher.TryMatch(message, out var uiControlAction))
        {
            var uiControlIntentResult = UiControlModeCommandMatcher.ToIntentParseResult(uiControlAction, rawMessage);
            _runtimeStateService.RecordIntentParserUsed(nameof(UiControlModeCommandMatcher), uiControlIntentResult.Intent);

            if (uiControlAction == UiControlModeCommandAction.CalibratePinch)
            {
                _logger.LogInformation("UiControlPinchCalibrationCommandDetected CorrelationId: {CorrelationId}. Command: {Command}", correlationId, message);
                _uiControlModeController?.Start();
                if (_visionSidecarHost is not null)
                {
                    await _visionSidecarHost.StartTrackingAsync(cancellationToken);
                }

                var success = _visionSidecarHost is not null;
                var spokenText = success
                    ? "Starting pinch calibration. One beep means open hand. Two beeps means pinch. Three beeps means release."
                    : "Vision sidecar is not available.";

                return await PolishAsync(new AssistantResponse
                {
                    Success = success,
                    Message = spokenText,
                    SpokenText = spokenText,
                    CorrelationId = correlationId,
                    CaptureId = captureId,
                    ToolName = "UI Control Mode",
                    Intent = uiControlIntentResult.Intent,
                    IntentConfidence = uiControlIntentResult.Confidence,
                    OriginalMessage = uiControlIntentResult.OriginalMessage,
                    ParserUsed = uiControlIntentResult.ParserUsed,
                    CapabilityId = uiControlIntentResult.CapabilityId,
                    CapabilityName = uiControlIntentResult.CapabilityName,
                    ResponseType = "system"
                }, cancellationToken);
            }

            if (uiControlAction == UiControlModeCommandAction.CalibrateMotionRegion)
            {
                _logger.LogInformation("VisionMotionRegionCalibrationCommandDetected CorrelationId: {CorrelationId}. Command: {Command}", correlationId, message);
                _uiControlModeController?.Start();
                if (_visionSidecarHost is not null)
                {
                    await _visionSidecarHost.StartTrackingAsync(cancellationToken);
                }

                var success = _visionSidecarHost is not null;
                var spokenText = success
                    ? "Starting motion region calibration. Move your hand to each corner when you hear the beeps: top left, top right, bottom right, then bottom left."
                    : "Vision sidecar is not available.";

                return await PolishAsync(new AssistantResponse
                {
                    Success = success,
                    Message = spokenText,
                    SpokenText = spokenText,
                    CorrelationId = correlationId,
                    CaptureId = captureId,
                    ToolName = "Motion Region Calibration",
                    Intent = uiControlIntentResult.Intent,
                    IntentConfidence = uiControlIntentResult.Confidence,
                    OriginalMessage = uiControlIntentResult.OriginalMessage,
                    ParserUsed = uiControlIntentResult.ParserUsed,
                    CapabilityId = "vision_motion_region_calibration",
                    CapabilityName = "Motion Region Calibration",
                    ResponseType = "system"
                }, cancellationToken);
            }

            if (uiControlAction == UiControlModeCommandAction.Start)
            {
                _logger.LogInformation("UiControlModeStartCommandDetected CorrelationId: {CorrelationId}. Command: {Command}", correlationId, message);
                if (_motionControlModeService is not null)
                {
                    await _motionControlModeService.EnableAsync("ui_control_command", cancellationToken: cancellationToken);
                }
                else
                {
                    _uiControlModeController?.Start();
                    if (_visionSidecarHost is not null)
                    {
                        await _visionSidecarHost.StartTrackingAsync(cancellationToken);
                    }
                }
            }
            else
            {
                _logger.LogInformation("UiControlModeStopCommandDetected CorrelationId: {CorrelationId}. Command: {Command}", correlationId, message);
                if (_motionControlModeService is not null)
                {
                    await _motionControlModeService.DisableAsync("ui_control_command", cancellationToken);
                }
                else
                {
                    if (_visionSidecarHost is not null)
                    {
                        await _visionSidecarHost.StopTrackingAsync(cancellationToken);
                    }

                    _uiControlModeController?.Stop();
                }
            }

            return await PolishAsync(new AssistantResponse
            {
                Success = true,
                Message = uiControlAction == UiControlModeCommandAction.Start
                    ? "UI control mode started."
                    : "UI control mode stopped.",
                SpokenText = uiControlAction == UiControlModeCommandAction.Start
                    ? "UI control mode started."
                    : "UI control mode stopped.",
                CorrelationId = correlationId,
                CaptureId = captureId,
                ToolName = "UI Control Mode",
                Intent = uiControlIntentResult.Intent,
                IntentConfidence = uiControlIntentResult.Confidence,
                OriginalMessage = uiControlIntentResult.OriginalMessage,
                ParserUsed = uiControlIntentResult.ParserUsed,
                CapabilityId = uiControlIntentResult.CapabilityId,
                CapabilityName = uiControlIntentResult.CapabilityName,
                ResponseType = "system"
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
                CaptureId = captureId,
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

        SetTurnState(correlationId, LiveAssistantTurnState.PlanningTool);
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
                CaptureId = captureId,
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
            "Matched tool. CaptureId: {CaptureId}. CorrelationId: {CorrelationId}. Intent: {Intent}. ToolName: {ToolName}. Command: {Command}",
            captureId,
            correlationId,
            intentResult.Intent,
            tool.Name,
            intentResult.NormalizedCommand);
        feedbackContext = feedbackContext is null
            ? null
            : _feedbackContextFactory?.EnrichWithRouting(
                feedbackContext,
                intentResult,
                tool,
                FeedbackPhase.Executing);

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

        if (ShouldUseResponsiveImmediateFeedback()
            && feedbackContext is not null
            && acknowledgementDecision?.ShouldSpeakInitialAcknowledgement == true)
        {
            _ = StartImmediateResponsiveFeedbackAsync(
                request,
                feedbackContext,
                pendingSpeechCancellation.Token);
        }
        else
        {
            _ = StartInitialAcknowledgementAsync(
                request,
                requestId,
                correlationId,
                receivedAtUtc,
                acknowledgementDecision,
                pendingSpeechCancellation.Token);
        }

        ToolResult result;
        var mainWorkStopwatch = Stopwatch.StartNew();
        try
        {
            if (ShouldUsePendingCommandGate(tool, intentResult))
            {
                SetTurnState(
                    correlationId,
                    LiveAssistantTurnState.AwaitingToolCommit,
                    DescribePendingCommand(tool, intentResult.NormalizedCommand));
                _logger.LogInformation(
                    "PendingCommandCreated. CorrelationId: {CorrelationId}. ToolName: {ToolName}. Command: {Command}.",
                    correlationId,
                    tool.Name,
                    intentResult.NormalizedCommand);
                _logger.LogInformation(
                    "PendingCommandAwaitingCommit. CorrelationId: {CorrelationId}. ToolName: {ToolName}. GraceMs: {GraceMs}.",
                    correlationId,
                    tool.Name,
                    PendingCommandGraceMs);
                await Task.Delay(TimeSpan.FromMilliseconds(PendingCommandGraceMs), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                _logger.LogInformation(
                    "PendingCommandCommitted. CorrelationId: {CorrelationId}. ToolName: {ToolName}.",
                    correlationId,
                    tool.Name);
            }

            SetTurnState(correlationId, LiveAssistantTurnState.ExecutingTool);
            var streamingProgressCancelled = 0;
            var shouldSpeakForContext = request.SpeechEventSender is not null
                && (string.Equals(request.InteractionSource, "voice", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.InteractionSource, "voice_stream", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.InteractionSource, "voice_correction", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.InteractionSource, "backend_idle_voice", StringComparison.OrdinalIgnoreCase))
                && string.Equals(request.ClientMode, "orb", StringComparison.OrdinalIgnoreCase);
            result = await tool.ExecuteAsync(
                new ToolExecutionContext
                {
                    CorrelationId = correlationId,
                    OriginalMessage = intentResult.OriginalMessage,
                    NormalizedCommand = intentResult.NormalizedCommand,
                    Intent = intentResult.Intent,
                    Route = intentResult.Route,
                    ShouldSpeak = shouldSpeakForContext,
                    SpeechEventSender = request.SpeechEventSender,
                    StreamingFinalAnswerStarted = () =>
                    {
                        if (Interlocked.Exchange(ref streamingProgressCancelled, 1) == 1)
                        {
                            return;
                        }

                        _logger.LogInformation(
                            "StreamingFinalAnswerProgressCancelled CorrelationId: {CorrelationId}. ToolName: {ToolName}.",
                            correlationId,
                            tool.Name);
                        pendingSpeechCancellation.Cancel();
                        progressHandle?.MarkMainResponseReady();
                    }
                },
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _responsiveFeedbackOrchestrator?.MarkMainResponseReady(correlationId);
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
        catch (OperationCanceledException)
        {
            pendingSpeechCancellation.Cancel();
            if (ShouldUsePendingCommandGate(tool, intentResult))
            {
                _logger.LogInformation(
                    "PendingCommandCancelledBeforeCommit. CorrelationId: {CorrelationId}. ToolName: {ToolName}.",
                    correlationId,
                    tool.Name);
            }

            throw;
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
            "Tool execution completed. CaptureId: {CaptureId}. CorrelationId: {CorrelationId}. ToolName: {ToolName}. Success: {Success}. ErrorCode: {ErrorCode}",
            captureId,
            correlationId,
            result.ToolName ?? tool.Name,
            result.Success,
            result.ErrorCode);

        SetTurnState(correlationId, LiveAssistantTurnState.ProcessingTurn);
        cancellationToken.ThrowIfCancellationRequested();
        var suppressFinalSpeech = ShouldSuppressFinalSuccessSpeechAfterImmediateFeedback(
            result,
            tool.Name,
            intentResult,
            correlationId);
        return await PolishAsync(new AssistantResponse
        {
            Success = result.Success,
            Message = result.Message,
            SpokenText = result.SpokenText,
            SpeechCacheKey = result.SpeechCacheKey,
            PreferPhraseCache = result.PreferPhraseCache,
            IsReplayableSpeech = result.IsReplayableSpeech,
            SuppressSpeech = suppressFinalSpeech,
            SegmentedSpeechStarted = result.SegmentedSpeechStarted,
            CorrelationId = correlationId,
            CaptureId = captureId,
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

    private const int PendingCommandGraceMs = 500;

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

    private Task StartImmediateResponsiveFeedbackAsync(
        AssistantRequest request,
        FeedbackContext context,
        CancellationToken cancellationToken)
    {
        if (_responsiveFeedbackOrchestrator is null
            || request.SpeechEventSender is null)
        {
            return Task.CompletedTask;
        }

        return _responsiveFeedbackOrchestrator.TryEmitImmediateFeedbackAsync(
            context,
            request.SpeechEventSender,
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
        cancellationToken.ThrowIfCancellationRequested();
        var polishedMessage = await _responsePolisher.PolishMessageAsync(response, cancellationToken);
        var polishedResponse = new AssistantResponse
        {
            Success = response.Success,
            Message = polishedMessage,
            SpokenText = response.SpokenText,
            SpeechCacheKey = response.SpeechCacheKey,
            PreferPhraseCache = response.PreferPhraseCache,
            IsReplayableSpeech = response.IsReplayableSpeech,
            SuppressSpeech = response.SuppressSpeech,
            SegmentedSpeechStarted = response.SegmentedSpeechStarted,
            CorrelationId = response.CorrelationId,
            CaptureId = response.CaptureId,
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

        if (polishedResponse.SuppressSpeech)
        {
            return polishedResponse;
        }

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
            SuppressSpeech = polishedResponse.SuppressSpeech,
            SegmentedSpeechStarted = polishedResponse.SegmentedSpeechStarted,
            CorrelationId = polishedResponse.CorrelationId,
            CaptureId = polishedResponse.CaptureId,
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

    private void SetTurnState(
        string correlationId,
        LiveAssistantTurnState state,
        string? pendingCommandDescription = null)
    {
        _liveTurnService?.UpdateTurnState(correlationId, state, pendingCommandDescription);
    }

    private async Task<AssistantResponse> HandleWebDestinationCommandAsync(
        WebDestinationCommand command,
        string rawMessage,
        string normalizedMessage,
        ActiveSurfaceSnapshot activeSurface,
        string correlationId,
        string? captureId,
        CancellationToken cancellationToken)
    {
        var intent = command.Action switch
        {
            WebDestinationAction.CloseWorkspace => "browser_workspace_close",
            WebDestinationAction.OpenWorkspace => "browser_workspace_open",
            WebDestinationAction.Search => "browser_workspace_search",
            WebDestinationAction.SearchCurrentPage => "browser_workspace_page_search",
            WebDestinationAction.ClickVisibleElement => "browser_workspace_page_click",
            WebDestinationAction.Back => "browser_workspace_back",
            WebDestinationAction.Forward => "browser_workspace_forward",
            WebDestinationAction.Refresh => "browser_workspace_refresh",
            WebDestinationAction.Scroll => "browser_workspace_scroll",
            WebDestinationAction.ScrollToTop => "browser_workspace_scroll_top",
            WebDestinationAction.ScrollToBottom => "browser_workspace_scroll_bottom",
            WebDestinationAction.ZoomIn => "browser_workspace_zoom_in",
            WebDestinationAction.ZoomOut => "browser_workspace_zoom_out",
            WebDestinationAction.ResetZoom => "browser_workspace_zoom_reset",
            WebDestinationAction.InspectPage => "browser_workspace_page_snapshot",
            WebDestinationAction.PageInfo => "browser_workspace_page_info",
            WebDestinationAction.SummarizePage => "browser_workspace_page_summary",
            WebDestinationAction.FindOnPage => "browser_workspace_page_find",
            WebDestinationAction.CommonPageAction => "browser_workspace_common_action",
            WebDestinationAction.EnableBrowserMotionOverlay => "browser_motion_overlay_start",
            WebDestinationAction.DisableBrowserMotionOverlay => "browser_motion_overlay_stop",
            _ => "browser_workspace_open_url"
        };
        _runtimeStateService.RecordIntentParserUsed(nameof(WebDestinationParser), intent);

        if (_browserWorkspaceService is null)
        {
            _logger.LogWarning(
                "WebDestinationCommandDetected CorrelationId: {CorrelationId}. Action: {Action}. Reason: {Reason}. Rejected: service_unavailable. Command: {Command}",
                correlationId,
                command.Action,
                command.Reason,
                normalizedMessage);

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = "Browser is not available.",
                SpokenText = "Browser is not available.",
                CorrelationId = correlationId,
                CaptureId = captureId,
                ErrorCode = "BROWSER_WORKSPACE_UNAVAILABLE",
                ToolName = "Merlin Browser Workspace",
                Intent = "browser_workspace_unavailable",
                IntentConfidence = 1,
                OriginalMessage = rawMessage,
                ParserUsed = nameof(WebDestinationParser),
                CapabilityId = "browser_workspace",
                CapabilityName = "Browser Workspace",
                ResponseType = "error"
            }, cancellationToken);
        }

        _logger.LogInformation(
            "WebDestinationCommandDetected CorrelationId: {CorrelationId}. Action: {Action}. Url: {Url}. Reason: {Reason}. Command: {Command}. ActiveSurfaceKind: {ActiveSurfaceKind}. ActiveSurfaceId: {ActiveSurfaceId}. ActiveSurfaceSource: {ActiveSurfaceSource}. ActiveSurfaceConfidence: {ActiveSurfaceConfidence}.",
            correlationId,
            command.Action,
            command.Url,
            command.Reason,
            normalizedMessage,
            activeSurface.Kind,
            activeSurface.SurfaceId,
            activeSurface.Source,
            activeSurface.Confidence);

        try
        {
            string message;
            switch (command.Action)
            {
                case WebDestinationAction.CloseWorkspace:
                    _logger.LogInformation(
                        "BrowserCommandDetected CorrelationId: {CorrelationId}. Action: {Action}.",
                        correlationId,
                        command.Action);
                    _logger.LogInformation("BrowserWorkspaceCloseRequested CorrelationId: {CorrelationId}.", correlationId);
                    await _browserWorkspaceService.CloseAsync(cancellationToken);
                    message = "Closed.";
                    break;

                case WebDestinationAction.Back:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserBackCommandSent CorrelationId: {CorrelationId}.", correlationId);
                    await _browserWorkspaceService.BackAsync(cancellationToken);
                    message = "Back.";
                    break;

                case WebDestinationAction.Forward:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserForwardCommandSent CorrelationId: {CorrelationId}.", correlationId);
                    await _browserWorkspaceService.ForwardAsync(cancellationToken);
                    message = "Forward.";
                    break;

                case WebDestinationAction.Refresh:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserRefreshCommandSent CorrelationId: {CorrelationId}.", correlationId);
                    await _browserWorkspaceService.RefreshAsync(cancellationToken);
                    message = "Refreshed.";
                    break;

                case WebDestinationAction.Scroll:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var direction = command.ScrollDirection
                        ?? throw new InvalidOperationException("No scroll direction was resolved.");
                    var amount = command.ScrollAmount
                        ?? BrowserScrollAmount.Normal;
                    _logger.LogInformation(
                        "BrowserScrollCommandSent CorrelationId: {CorrelationId}. Direction: {Direction}. Amount: {Amount}.",
                        correlationId,
                        direction,
                        amount);
                    await _browserWorkspaceService.ScrollAsync(direction, amount, cancellationToken);
                    message = "Scrolling.";
                    break;

                case WebDestinationAction.ScrollToTop:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserScrollCommandSent CorrelationId: {CorrelationId}. Direction: top.", correlationId);
                    await _browserWorkspaceService.ScrollToTopAsync(cancellationToken);
                    message = "Scrolling.";
                    break;

                case WebDestinationAction.ScrollToBottom:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserScrollCommandSent CorrelationId: {CorrelationId}. Direction: bottom.", correlationId);
                    await _browserWorkspaceService.ScrollToBottomAsync(cancellationToken);
                    message = "Scrolling.";
                    break;

                case WebDestinationAction.ZoomIn:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserZoomCommandSent CorrelationId: {CorrelationId}. Action: zoom_in.", correlationId);
                    await _browserWorkspaceService.ZoomInAsync(cancellationToken);
                    message = "Zooming in.";
                    break;

                case WebDestinationAction.ZoomOut:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserZoomCommandSent CorrelationId: {CorrelationId}. Action: zoom_out.", correlationId);
                    await _browserWorkspaceService.ZoomOutAsync(cancellationToken);
                    message = "Zooming out.";
                    break;

                case WebDestinationAction.ResetZoom:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    _logger.LogInformation("BrowserZoomCommandSent CorrelationId: {CorrelationId}. Action: reset_zoom.", correlationId);
                    await _browserWorkspaceService.ResetZoomAsync(cancellationToken);
                    message = "Reset zoom.";
                    break;

                case WebDestinationAction.Search:
                    if (string.IsNullOrWhiteSpace(command.SearchQuery))
                    {
                        throw new InvalidOperationException("No search query was resolved.");
                    }

                    _logger.LogInformation(
                        "BrowserSearchCommandDetected CorrelationId: {CorrelationId}. QueryLength: {QueryLength}.",
                        correlationId,
                        command.SearchQuery.Trim().Length);
                    await _browserWorkspaceService.SearchAsync(command.SearchQuery, cancellationToken);
                    message = "Searching.";
                    break;

                case WebDestinationAction.SearchCurrentPage:
                    if (string.IsNullOrWhiteSpace(command.SearchQuery))
                    {
                        throw new InvalidOperationException("No page search query was resolved.");
                    }

                    _logger.LogInformation(
                        "PageSearchCommandDetected CorrelationId: {CorrelationId}. QueryLength: {QueryLength}. HasSiteHint: {HasSiteHint}.",
                        correlationId,
                        command.SearchQuery.Trim().Length,
                        !string.IsNullOrWhiteSpace(command.SiteHint));
                    if (!string.IsNullOrWhiteSpace(command.Url))
                    {
                        _logger.LogInformation(
                            "PageSearchSiteHintNavigateRequested CorrelationId: {CorrelationId}. SiteHint: {SiteHint}. Url: {Url}.",
                            correlationId,
                            command.SiteHint,
                            command.Url);
                        await _browserWorkspaceService.NavigateAsync(command.Url, cancellationToken);
                        await Task.Delay(TimeSpan.FromMilliseconds(900), cancellationToken);
                    }
                    else if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var pageSearchResult = await _browserWorkspaceService.SearchCurrentPageAsync(
                        command.SearchQuery,
                        cancellationToken: cancellationToken);
                    if (!pageSearchResult.Success)
                    {
                        var failureMessage = pageSearchResult.ErrorCode switch
                        {
                            "search_field_not_found" => "I could not find a search field on this page.",
                            "unsafe_action_blocked" => "I can't handle password or payment fields.",
                            "browser_inactive" => "The browser is not open.",
                            _ => "I could not search this page."
                        };

                        return await PolishAsync(new AssistantResponse
                        {
                            Success = false,
                            Message = failureMessage,
                            SpokenText = failureMessage,
                            CorrelationId = correlationId,
                            CaptureId = captureId,
                            ErrorCode = pageSearchResult.ErrorCode?.ToUpperInvariant() ?? "PAGE_SEARCH_FAILED",
                            ToolName = "Merlin Browser Workspace",
                            Intent = intent,
                            IntentConfidence = 1,
                            OriginalMessage = rawMessage,
                            ParserUsed = nameof(WebDestinationParser),
                            CapabilityId = "browser_workspace",
                            CapabilityName = "Browser Workspace",
                            ResponseType = "error"
                        }, cancellationToken);
                    }

                    message = "Searching.";
                    break;

                case WebDestinationAction.ClickVisibleElement:
                    _logger.LogInformation(
                        "PageClickCommandDetected CorrelationId: {CorrelationId}. QueryLength: {QueryLength}. TargetKind: {TargetKind}. Ordinal: {Ordinal}.",
                        correlationId,
                        command.ClickQuery?.Trim().Length ?? 0,
                        command.TargetKind,
                        command.Ordinal);
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var clickResult = await _browserWorkspaceService.ClickVisibleElementAsync(
                        command.ClickQuery,
                        command.TargetKind,
                        command.Ordinal,
                        cancellationToken);
                    if (!clickResult.Success)
                    {
                        var failureMessage = clickResult.ErrorCode switch
                        {
                            "element_not_found" => "I could not find that on the page.",
                            "ambiguous_match" => $"I found multiple matches{(string.IsNullOrWhiteSpace(command.ClickQuery) ? "." : $" for {command.ClickQuery}.")}",
                            "unsafe_action_requires_confirmation" => $"I need confirmation before clicking \"{(string.IsNullOrWhiteSpace(clickResult.ElementText) ? "that" : clickResult.ElementText)}\".",
                            "unsafe_action_blocked" => "I can't do that automatically.",
                            "confirmation_stale" => "That page changed, so I won't click it.",
                            "browser_inactive" => "The browser is not open.",
                            _ => "I could not click that."
                        };

                        return await PolishAsync(new AssistantResponse
                        {
                            Success = false,
                            Message = failureMessage,
                            SpokenText = failureMessage,
                            CorrelationId = correlationId,
                            CaptureId = captureId,
                            ErrorCode = clickResult.ErrorCode?.ToUpperInvariant() ?? "PAGE_CLICK_FAILED",
                            ToolName = "Merlin Browser Workspace",
                            Intent = intent,
                            IntentConfidence = 1,
                            OriginalMessage = rawMessage,
                            ParserUsed = nameof(WebDestinationParser),
                            CapabilityId = "browser_workspace",
                            CapabilityName = "Browser Workspace",
                            ResponseType = string.Equals(clickResult.ErrorCode, "unsafe_action_requires_confirmation", StringComparison.OrdinalIgnoreCase)
                                ? "confirmation"
                                : "error",
                            Confirmation = clickResult.Confirmation
                        }, cancellationToken);
                    }

                    message = string.Equals(command.TargetKind, "result", StringComparison.OrdinalIgnoreCase)
                        || command.Ordinal is not null
                        || !string.IsNullOrWhiteSpace(clickResult.ElementHref)
                            ? "Opening."
                            : "Clicked.";
                    break;

                case WebDestinationAction.InspectPage:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var snapshot = await _browserWorkspaceService.GetSnapshotAsync(cancellationToken);
                    if (snapshot is null)
                    {
                        throw new InvalidOperationException("No page snapshot was returned.");
                    }

                    message = string.IsNullOrWhiteSpace(snapshot.Error)
                        ? FormatPageSnapshotSummary(snapshot)
                        : "I could not inspect the page.";
                    break;

                case WebDestinationAction.PageInfo:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var pageInfoSnapshot = await _browserWorkspaceService.GetSnapshotAsync(cancellationToken);
                    if (pageInfoSnapshot is null)
                    {
                        throw new InvalidOperationException("No page snapshot was returned.");
                    }

                    message = string.IsNullOrWhiteSpace(pageInfoSnapshot.Error)
                        ? FormatPageInfo(pageInfoSnapshot)
                        : "I could not inspect the page.";
                    break;

                case WebDestinationAction.SummarizePage:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var summarySnapshot = await _browserWorkspaceService.GetSnapshotAsync(cancellationToken);
                    if (summarySnapshot is null)
                    {
                        throw new InvalidOperationException("No page snapshot was returned.");
                    }

                    message = string.IsNullOrWhiteSpace(summarySnapshot.Error)
                        ? FormatPageReadout(summarySnapshot)
                        : "I could not inspect the page.";
                    break;

                case WebDestinationAction.FindOnPage:
                    if (string.IsNullOrWhiteSpace(command.SearchQuery))
                    {
                        throw new InvalidOperationException("No page find query was resolved.");
                    }

                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    var findSnapshot = await _browserWorkspaceService.GetSnapshotAsync(cancellationToken);
                    if (findSnapshot is null)
                    {
                        throw new InvalidOperationException("No page snapshot was returned.");
                    }

                    message = string.IsNullOrWhiteSpace(findSnapshot.Error)
                        ? FormatPageFindResult(findSnapshot, command.SearchQuery)
                        : "I could not inspect the page.";
                    break;

                case WebDestinationAction.CommonPageAction:
                    if (!_browserWorkspaceService.IsActive)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    if (string.IsNullOrWhiteSpace(command.CommonAction))
                    {
                        throw new InvalidOperationException("No browser common action was resolved.");
                    }

                    _logger.LogInformation(
                        "BrowserCommonActionDetected CorrelationId: {CorrelationId}. Action: {Action}.",
                        correlationId,
                        command.CommonAction);
                    var commonResult = await _browserWorkspaceService.PerformCommonActionAsync(
                        command.CommonAction,
                        cancellationToken);
                    if (!commonResult.Success)
                    {
                        var failureMessage = commonResult.ErrorCode switch
                        {
                            "skip_button_not_found" => "I don't see a skip button.",
                            "common_action_not_found" => "I could not find that on the page.",
                            "ambiguous_match" => "I found multiple matches.",
                            "unsafe_action_blocked" => "I can't do that automatically.",
                            "unsafe_action_requires_confirmation" => "I can't do that automatically.",
                            _ => "I could not do that."
                        };

                        return await PolishAsync(new AssistantResponse
                        {
                            Success = false,
                            Message = failureMessage,
                            SpokenText = failureMessage,
                            CorrelationId = correlationId,
                            CaptureId = captureId,
                            ErrorCode = commonResult.ErrorCode?.ToUpperInvariant() ?? "BROWSER_COMMON_ACTION_FAILED",
                            ToolName = "Merlin Browser Workspace",
                            Intent = intent,
                            IntentConfidence = 1,
                            OriginalMessage = rawMessage,
                            ParserUsed = nameof(WebDestinationParser),
                            CapabilityId = "browser_workspace",
                            CapabilityName = "Browser Workspace",
                            ResponseType = "error"
                        }, cancellationToken);
                    }

                    message = command.CommonAction switch
                    {
                        "skip_ad" => "Skipped.",
                        "play_video" => "Playing.",
                        "pause_video" => "Paused.",
                        "mute_video" => "Muted.",
                        "unmute_video" => "Unmuted.",
                        "fullscreen" => "Fullscreen.",
                        "exit_fullscreen" => "Exited fullscreen.",
                        _ => "Clicked."
                    };
                    break;

                case WebDestinationAction.EnableBrowserMotionOverlay:
                    if (_motionControlModeService is not null)
                    {
                        if (_browserWorkspaceService is null || !_browserWorkspaceService.IsActive)
                        {
                            return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                        }

                        var bounds = _browserWorkspaceService.CurrentBounds;
                        if (bounds is null || bounds.IsMinimized || bounds.Width <= 0 || bounds.Height <= 0)
                        {
                            return await PolishAsync(new AssistantResponse
                            {
                                Success = false,
                                Message = "The browser is not available.",
                                SpokenText = "The browser is not available.",
                                CorrelationId = correlationId,
                                CaptureId = captureId,
                                ErrorCode = "BROWSER_UNAVAILABLE",
                                ToolName = "Merlin Browser Workspace",
                                Intent = intent,
                                IntentConfidence = 1,
                                OriginalMessage = rawMessage,
                                ParserUsed = nameof(WebDestinationParser),
                                CapabilityId = "browser_workspace",
                                CapabilityName = "Browser Workspace",
                                ResponseType = "error"
                            }, cancellationToken);
                        }

                        var profileOverride = new MotionControlProfileOverride(
                            MotionControlProfileId.BrowserWorkspace,
                            "browser_pointer_command");
                        await _motionControlModeService.EnableAsync(
                            "browser_pointer_command",
                            profileOverride,
                            cancellationToken);

                        message = "Browser pointer started.";
                        break;
                    }

                    if (_browserMotionOverlayModeService is null)
                    {
                        return await PolishAsync(new AssistantResponse
                        {
                            Success = false,
                            Message = "Browser pointer is not available.",
                            SpokenText = "Browser pointer is not available.",
                            CorrelationId = correlationId,
                            CaptureId = captureId,
                            ErrorCode = "BROWSER_POINTER_UNAVAILABLE",
                            ToolName = "Merlin Browser Workspace",
                            Intent = intent,
                            IntentConfidence = 1,
                            OriginalMessage = rawMessage,
                            ParserUsed = nameof(WebDestinationParser),
                            CapabilityId = "browser_workspace",
                            CapabilityName = "Browser Workspace",
                            ResponseType = "error"
                        }, cancellationToken);
                    }

                    var startResult = await _browserMotionOverlayModeService.EnableAsync(cancellationToken);
                    if (startResult is BrowserMotionOverlayStartResult.BrowserNotOpen)
                    {
                        return await BrowserNotOpenAsync(command, rawMessage, correlationId, captureId, cancellationToken);
                    }

                    if (startResult is BrowserMotionOverlayStartResult.BrowserUnavailable)
                    {
                        return await PolishAsync(new AssistantResponse
                        {
                            Success = false,
                            Message = "The browser is not available.",
                            SpokenText = "The browser is not available.",
                            CorrelationId = correlationId,
                            CaptureId = captureId,
                            ErrorCode = "BROWSER_UNAVAILABLE",
                            ToolName = "Merlin Browser Workspace",
                            Intent = intent,
                            IntentConfidence = 1,
                            OriginalMessage = rawMessage,
                            ParserUsed = nameof(WebDestinationParser),
                            CapabilityId = "browser_workspace",
                            CapabilityName = "Browser Workspace",
                            ResponseType = "error"
                        }, cancellationToken);
                    }

                    if (_visionSidecarHost is not null)
                    {
                        await _visionSidecarHost.StartTrackingAsync(cancellationToken);
                    }

                    message = "Browser pointer started.";
                    break;

                case WebDestinationAction.DisableBrowserMotionOverlay:
                    if (_motionControlModeService is not null)
                    {
                        if (string.Equals(
                            _motionControlModeService.Current.ActiveProfileId,
                            MotionControlProfileId.BrowserWorkspace,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            await _motionControlModeService.DisableAsync("browser_pointer_command", cancellationToken);
                        }

                        message = "Browser pointer stopped.";
                        break;
                    }

                    if (_browserMotionOverlayModeService is not null)
                    {
                        await _browserMotionOverlayModeService.DisableAsync("voice_command", cancellationToken);
                    }

                    if (_visionSidecarHost is not null && _uiControlModeController?.IsActive != true)
                    {
                        await _visionSidecarHost.StopTrackingAsync(cancellationToken);
                    }

                    message = "Browser pointer stopped.";
                    break;

                case WebDestinationAction.Navigate:
                    if (string.IsNullOrWhiteSpace(command.Url))
                    {
                        throw new InvalidOperationException("No URL was resolved for browser navigation.");
                    }

                    _logger.LogInformation(
                        "WebDestinationResolved CorrelationId: {CorrelationId}. Url: {Url}. Reason: {Reason}.",
                        correlationId,
                        command.Url,
                        command.Reason);
                    _logger.LogInformation(
                        "BrowserWorkspaceAutoOpenRequested CorrelationId: {CorrelationId}. Url: {Url}.",
                        correlationId,
                        command.Url);
                    await _browserWorkspaceService.NavigateAsync(command.Url, cancellationToken);
                    message = "Opening.";
                    break;

                default:
                    _logger.LogInformation("BrowserWorkspaceGenericOpenRequested CorrelationId: {CorrelationId}.", correlationId);
                    await _browserWorkspaceService.OpenAsync(null, cancellationToken);
                    message = "Opening browser.";
                    break;
            }

            return await PolishAsync(new AssistantResponse
            {
                Success = true,
                Message = message,
                SpokenText = message,
                CorrelationId = correlationId,
                CaptureId = captureId,
                ToolName = "Merlin Browser Workspace",
                Intent = intent,
                IntentConfidence = 1,
                OriginalMessage = rawMessage,
                ParserUsed = nameof(WebDestinationParser),
                CapabilityId = "browser_workspace",
                CapabilityName = "Browser Workspace",
                ResponseType = "system"
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "BrowserWorkspaceCommandFailed CorrelationId: {CorrelationId}. Action: {Action}. Url: {Url}.",
                correlationId,
                command.Action,
                command.Url);

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = $"Browser failed: {exception.Message}",
                SpokenText = "Browser failed.",
                CorrelationId = correlationId,
                CaptureId = captureId,
                ErrorCode = "BROWSER_WORKSPACE_FAILED",
                ToolName = "Merlin Browser Workspace",
                Intent = "browser_workspace_failed",
                IntentConfidence = 1,
                OriginalMessage = rawMessage,
                ParserUsed = nameof(WebDestinationParser),
                CapabilityId = "browser_workspace",
                CapabilityName = "Browser Workspace",
                ResponseType = "error"
            }, cancellationToken);
        }
    }

    private static string FormatPageSnapshotSummary(BrowserPageSnapshot snapshot)
    {
        static string Count(int count, string singular, string plural) =>
            $"{count} {(count == 1 ? singular : plural)}";

        return $"I can see {Count(snapshot.SearchFields.Count, "search field", "search fields")}, {Count(snapshot.Buttons.Count, "button", "buttons")}, {Count(snapshot.Links.Count, "link", "links")}, and {Count(snapshot.Headings.Count, "heading", "headings")}.";
    }

    private static string FormatPageInfo(BrowserPageSnapshot snapshot)
    {
        var title = CleanPageText(snapshot.Title);
        var host = TryGetHost(snapshot.Url);
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(host))
        {
            return $"You are on {title} at {host}.";
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            return $"You are on {title}.";
        }

        if (!string.IsNullOrWhiteSpace(host))
        {
            return $"You are on {host}.";
        }

        return "I can see the browser page, but it does not expose a title or URL.";
    }

    private static string FormatPageReadout(BrowserPageSnapshot snapshot)
    {
        var title = CleanPageText(snapshot.Title);
        var heading = FirstText(snapshot.Headings);
        var textBlocks = snapshot.TextBlocks
            .Select(static element => CleanPageText(element.Text))
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Take(3)
            .ToArray();
        var results = snapshot.Results
            .Select(static element => CleanPageText(element.Text))
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Take(3)
            .ToArray();

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(title))
        {
            parts.Add($"Page: {title}.");
        }

        if (!string.IsNullOrWhiteSpace(heading)
            && !string.Equals(heading, title, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"Main heading: {heading}.");
        }

        if (textBlocks.Length > 0)
        {
            parts.Add("Visible text: " + string.Join(" ", textBlocks) + ".");
        }
        else if (results.Length > 0)
        {
            parts.Add("Top visible results: " + string.Join("; ", results) + ".");
        }

        if (parts.Count == 0)
        {
            return FormatPageSnapshotSummary(snapshot);
        }

        return LimitSentence(string.Join(" ", parts), 900);
    }

    private static string FormatPageFindResult(BrowserPageSnapshot snapshot, string query)
    {
        var normalizedQuery = NormalizePageText(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return "I need something to find on the page.";
        }

        var matches = EnumerateReadableSnapshotElements(snapshot)
            .Select(element => new
            {
                Text = CleanPageText(GetReadableElementText(element)),
                Element = element
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Where(item => NormalizePageText(item.Text).Contains(normalizedQuery, StringComparison.Ordinal))
            .Take(5)
            .ToArray();

        if (matches.Length == 0)
        {
            return $"I do not see \"{CleanPageText(query)}\" on this page.";
        }

        var labels = matches
            .Select(static item => item.Text!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        return $"I found {matches.Length} {(matches.Length == 1 ? "match" : "matches")}: {string.Join("; ", labels)}.";
    }

    private static IEnumerable<BrowserSnapshotElement> EnumerateReadableSnapshotElements(BrowserPageSnapshot snapshot)
    {
        foreach (var element in snapshot.Headings) yield return element;
        foreach (var element in snapshot.Results) yield return element;
        foreach (var element in snapshot.Links) yield return element;
        foreach (var element in snapshot.Buttons) yield return element;
        foreach (var element in snapshot.TextBlocks) yield return element;
    }

    private static string? GetReadableElementText(BrowserSnapshotElement element) =>
        element.Text
        ?? element.Label
        ?? element.AriaLabel
        ?? element.Title
        ?? element.DataTitleNoTooltip
        ?? element.DataTooltipTitle
        ?? element.Placeholder;

    private static string? FirstText(IEnumerable<BrowserSnapshotElement> elements) =>
        elements
            .Select(static element => CleanPageText(GetReadableElementText(element)))
            .FirstOrDefault(static text => !string.IsNullOrWhiteSpace(text));

    private static string? TryGetHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
    }

    private static string CleanPageText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return LimitSentence(cleaned, 240);
    }

    private static string NormalizePageText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", " ");
        return System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
    }

    private static string LimitSentence(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, Math.Max(0, maxLength - 3)), "...");
    }

    private async Task<AssistantResponse> BrowserNotOpenAsync(
        WebDestinationCommand command,
        string rawMessage,
        string correlationId,
        string? captureId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "BrowserCommandRejectedBrowserNotOpen CorrelationId: {CorrelationId}. Action: {Action}.",
            correlationId,
            command.Action);

        return await PolishAsync(new AssistantResponse
        {
            Success = false,
            Message = "The browser is not open.",
            SpokenText = "The browser is not open.",
            CorrelationId = correlationId,
            CaptureId = captureId,
            ErrorCode = "BROWSER_WORKSPACE_NOT_OPEN",
            ToolName = "Merlin Browser Workspace",
            Intent = "browser_workspace_not_open",
            IntentConfidence = 1,
            OriginalMessage = rawMessage,
            ParserUsed = nameof(WebDestinationParser),
            CapabilityId = "browser_workspace",
            CapabilityName = "Browser Workspace",
            ResponseType = "error"
        }, cancellationToken);
    }

    private async Task<AssistantResponse> HandleBrowserWorkspaceCommandAsync(
        BrowserWorkspaceAction action,
        string? url,
        string rawMessage,
        string normalizedMessage,
        string correlationId,
        string? captureId,
        CancellationToken cancellationToken)
    {
        _runtimeStateService.RecordIntentParserUsed(
            nameof(BrowserWorkspaceCommandMatcher),
            action == BrowserWorkspaceAction.Close
                ? "browser_workspace_close"
                : url is null ? "browser_workspace_open" : "browser_workspace_open_url");

        if (_browserWorkspaceService is null)
        {
            _logger.LogWarning(
                "BrowserWorkspaceCommandRejected CorrelationId: {CorrelationId}. Reason: service_unavailable. Command: {Command}",
                correlationId,
                normalizedMessage);

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = "Browser is not available.",
                SpokenText = "Browser is not available.",
                CorrelationId = correlationId,
                CaptureId = captureId,
                ErrorCode = "BROWSER_WORKSPACE_UNAVAILABLE",
                ToolName = "Merlin Browser Workspace",
                Intent = "browser_workspace_unavailable",
                OriginalMessage = rawMessage,
                ParserUsed = nameof(BrowserWorkspaceCommandMatcher),
                CapabilityId = "browser_workspace",
                CapabilityName = "Browser Workspace",
                ResponseType = "error"
            }, cancellationToken);
        }

        try
        {
            if (action == BrowserWorkspaceAction.Close)
            {
                _logger.LogInformation(
                    "BrowserWorkspaceCloseCommandDetected CorrelationId: {CorrelationId}. Command: {Command}",
                    correlationId,
                    normalizedMessage);
                await _browserWorkspaceService.CloseAsync(cancellationToken);

                return await PolishAsync(new AssistantResponse
                {
                    Success = true,
                    Message = "Closing browser.",
                    SpokenText = "Closing browser.",
                    CorrelationId = correlationId,
                    CaptureId = captureId,
                    ToolName = "Merlin Browser Workspace",
                    Intent = "browser_workspace_close",
                    IntentConfidence = 1,
                    OriginalMessage = rawMessage,
                    ParserUsed = nameof(BrowserWorkspaceCommandMatcher),
                    CapabilityId = "browser_workspace",
                    CapabilityName = "Browser Workspace",
                    ResponseType = "system"
                }, cancellationToken);
            }

            _logger.LogInformation(
                "BrowserWorkspaceOpenCommandDetected CorrelationId: {CorrelationId}. Url: {Url}. Command: {Command}",
                correlationId,
                url,
                normalizedMessage);
            await _browserWorkspaceService.OpenAsync(url, cancellationToken);

            var message = url is null
                ? "Opening browser."
                : $"Opening {url}.";

            return await PolishAsync(new AssistantResponse
            {
                Success = true,
                Message = message,
                SpokenText = message,
                CorrelationId = correlationId,
                CaptureId = captureId,
                ToolName = "Merlin Browser Workspace",
                Intent = url is null ? "browser_workspace_open" : "browser_workspace_open_url",
                IntentConfidence = 1,
                OriginalMessage = rawMessage,
                ParserUsed = nameof(BrowserWorkspaceCommandMatcher),
                CapabilityId = "browser_workspace",
                CapabilityName = "Browser Workspace",
                ResponseType = "system"
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "BrowserWorkspaceCommandFailed CorrelationId: {CorrelationId}. Action: {Action}. Url: {Url}.",
                correlationId,
                action,
                url);

            return await PolishAsync(new AssistantResponse
            {
                Success = false,
                Message = $"Browser failed: {exception.Message}",
                SpokenText = "Browser failed.",
                CorrelationId = correlationId,
                CaptureId = captureId,
                ErrorCode = "BROWSER_WORKSPACE_FAILED",
                ToolName = "Merlin Browser Workspace",
                Intent = "browser_workspace_failed",
                IntentConfidence = 1,
                OriginalMessage = rawMessage,
                ParserUsed = nameof(BrowserWorkspaceCommandMatcher),
                CapabilityId = "browser_workspace",
                CapabilityName = "Browser Workspace",
                ResponseType = "error"
            }, cancellationToken);
        }
    }

    private static bool TryMatchBrowserWorkspaceCommand(
        string message,
        out BrowserWorkspaceAction action,
        out string? url)
    {
        return BrowserWorkspaceCommandMatcher.TryMatch(message, out action, out url);
    }

    private enum BrowserWorkspaceAction
    {
        Open,
        Close
    }

    private static class BrowserWorkspaceCommandMatcher
    {
        private static readonly string[] OpenCommands =
        [
            "open browser workspace",
            "open the browser workspace",
            "open your browser",
            "use your browser",
            "start browser workspace",
            "show browser workspace"
        ];

        private static readonly string[] CloseCommands =
        [
            "close browser",
            "close browser workspace",
            "close the browser workspace",
            "close your browser",
            "stop browser workspace",
            "hide browser workspace"
        ];

        private static readonly string[] InsideSuffixes =
        [
            " inside you",
            " inside merlin",
            " in merlin"
        ];

        private static readonly string[] UrlPrefixes =
        [
            "go to ",
            "open ",
            "browse ",
            "visit ",
            "pull up "
        ];

        public static bool TryMatch(
            string message,
            out BrowserWorkspaceAction action,
            out string? url)
        {
            action = BrowserWorkspaceAction.Open;
            url = null;

            var command = Clean(message);
            if (string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            if (OpenCommands.Any(openCommand => string.Equals(command, openCommand, StringComparison.OrdinalIgnoreCase)))
            {
                action = BrowserWorkspaceAction.Open;
                return true;
            }

            if (CloseCommands.Any(closeCommand => string.Equals(command, closeCommand, StringComparison.OrdinalIgnoreCase)))
            {
                action = BrowserWorkspaceAction.Close;
                return true;
            }

            foreach (var suffix in InsideSuffixes)
            {
                if (!command.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var withoutSuffix = command[..^suffix.Length].Trim();
                foreach (var prefix in UrlPrefixes)
                {
                    if (!withoutSuffix.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var target = withoutSuffix[prefix.Length..].Trim();
                    if (string.Equals(target, "this", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(target, "that", StringComparison.OrdinalIgnoreCase))
                    {
                        action = BrowserWorkspaceAction.Open;
                        url = null;
                        return true;
                    }

                    var normalizationResult = OpenUrlTool.NormalizeUrl(target);
                    if (!normalizationResult.Success)
                    {
                        return false;
                    }

                    action = BrowserWorkspaceAction.Open;
                    url = normalizationResult.Url;
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string message)
        {
            var cleaned = (message ?? string.Empty)
                .Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',');

            if (cleaned.StartsWith("merlin ", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned["merlin ".Length..].Trim();
            }

            foreach (var prefix in new[] { "please ", "can you ", "could you " })
            {
                if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleaned = cleaned[prefix.Length..].Trim();
                    break;
                }
            }

            if (cleaned.EndsWith(" please", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^" please".Length].Trim();
            }

            return cleaned;
        }
    }

    private static bool ShouldUsePendingCommandGate(ITool tool, IntentParseResult intentResult)
    {
        return string.Equals(tool.Name, "Open URL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.Intent, "open_url", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intentResult.CapabilityId, "url.open", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribePendingCommand(ITool tool, string command)
    {
        return $"{tool.Name}: {command}";
    }

    private static string GetOrCreateCorrelationId(string? correlationId)
    {
        return string.IsNullOrWhiteSpace(correlationId)
            ? Guid.NewGuid().ToString("N")
            : correlationId;
    }

    private void TryStartMerlinNextShadow(
        AssistantRequest request,
        string requestId,
        string normalizedMessage,
        ActiveSurfaceSnapshot activeSurface,
        DateTimeOffset receivedAtUtc)
    {
        if (_merlinNextRequestAdapter is null || _merlinNextShadowBridge is null)
        {
            return;
        }

        try
        {
            var merlinRequest = _merlinNextRequestAdapter.FromAssistantRequest(
                request,
                requestId,
                normalizedMessage,
                activeSurface,
                receivedAtUtc);

            _merlinNextShadowBridge.TryStartShadow(merlinRequest);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "MerlinNextShadowBridgeStartFailed RequestId: {RequestId}.",
                requestId);
        }
    }

    private static bool ShouldNormalizeSpeech(AssistantRequest request)
    {
        return request.InteractionSource is not null
            && (string.Equals(request.InteractionSource, "voice", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.InteractionSource, "voice_stream", StringComparison.OrdinalIgnoreCase)
                || string.Equals(request.InteractionSource, "backend_idle_voice", StringComparison.OrdinalIgnoreCase));
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

    private bool ShouldUseResponsiveImmediateFeedback()
    {
        return _responsiveFeedbackOrchestrator is not null
            && _responsiveFeedbackOptions?.Enabled == true
            && _responsiveFeedbackOptions.UseCardSelectorForImmediateFeedback;
    }

    private bool ShouldSuppressFinalSuccessSpeechAfterImmediateFeedback(
        ToolResult result,
        string matchedToolName,
        IntentParseResult intentResult,
        string correlationId)
    {
        if (_responsiveFeedbackOptions?.SuppressFinalSuccessSpeechAfterImmediateFeedback != true)
        {
            return false;
        }

        if (_responsiveFeedbackOrchestrator?.WasImmediateFeedbackEmitted(correlationId) != true)
        {
            return false;
        }

        if (!result.Success
            || result.Confirmation is not null
            || result.ApplicationCandidates is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(result.ErrorCode)
            || !string.IsNullOrWhiteSpace(result.SpokenText)
            || !string.IsNullOrWhiteSpace(result.SpeechCacheKey))
        {
            return false;
        }

        return IsOpenApplicationOrUrl(result.ToolName ?? matchedToolName, result.Intent ?? intentResult.Intent);
    }

    private static bool IsOpenApplicationOrUrl(string? toolName, string? intent)
    {
        return string.Equals(toolName, "Open Application", StringComparison.OrdinalIgnoreCase)
            || string.Equals(toolName, "Open URL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intent, "open_application", StringComparison.OrdinalIgnoreCase)
            || string.Equals(intent, "open_url", StringComparison.OrdinalIgnoreCase);
    }
}
