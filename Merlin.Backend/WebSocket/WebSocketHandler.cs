using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.LiveUtterance;

namespace Merlin.Backend.WebSocket;

public sealed class WebSocketHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly CommandRouter _commandRouter;
    private readonly IBargeInCoordinator? _bargeInCoordinator;
    private readonly IBargeInDebugSnapshotService? _bargeInDebugSnapshots;
    private readonly ICorrectionRequestBuilder _correctionRequestBuilder;
    private readonly ILiveUtteranceGate? _liveUtteranceGate;
    private readonly ILiveAssistantTurnService _liveTurnService;
    private readonly IAssistantSpeechPlaybackService _speechPlaybackService;
    private readonly ISpeechPolicyService _speechPolicyService;
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly VoiceStreamSessionService _voiceStreamSessionService;
    private readonly ConcurrentDictionary<string, AssistantRequest> _recentRequestsByCorrelationId = new(StringComparer.OrdinalIgnoreCase);

    public WebSocketHandler(
        CommandRouter commandRouter,
        ILiveAssistantTurnService liveTurnService,
        IAssistantSpeechPlaybackService speechPlaybackService,
        ISpeechPolicyService speechPolicyService,
        ILogger<WebSocketHandler> logger,
        IRuntimeStateService runtimeStateService,
        VoiceStreamSessionService voiceStreamSessionService,
        ICorrectionRequestBuilder? correctionRequestBuilder = null,
        IBargeInCoordinator? bargeInCoordinator = null,
        ILiveUtteranceGate? liveUtteranceGate = null,
        IBargeInDebugSnapshotService? bargeInDebugSnapshots = null)
    {
        _commandRouter = commandRouter;
        _bargeInCoordinator = bargeInCoordinator;
        _correctionRequestBuilder = correctionRequestBuilder ?? new CorrectionRequestBuilder();
        _liveUtteranceGate = liveUtteranceGate;
        _liveTurnService = liveTurnService;
        _speechPlaybackService = speechPlaybackService;
        _speechPolicyService = speechPolicyService;
        _logger = logger;
        _runtimeStateService = runtimeStateService;
        _voiceStreamSessionService = voiceStreamSessionService;
        _bargeInDebugSnapshots = bargeInDebugSnapshots;
    }

    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket request.", context.RequestAborted);
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var sendGate = new SemaphoreSlim(1, 1);
        ConcurrentDictionary<string, byte>? connectionCorrelationIds = null;
        CancellationTokenSource? devVisualFlowCancellation = null;
        Func<CorrectionRegenerationRequested, CancellationToken, Task>? correctionHandler = null;
        Func<LiveUserUtteranceRouted, CancellationToken, Task>? liveUtteranceHandler = null;
        Func<BargeInDebugSnapshot, CancellationToken, Task>? bargeInDebugHandler = null;
        _runtimeStateService.IncrementActiveWebSocketConnections();
        _logger.LogInformation(
            "WebSocket connection opened. ActiveConnections: {ActiveConnections}",
            _runtimeStateService.ActiveWebSocketConnections);

        try
        {
            connectionCorrelationIds = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            if (_bargeInCoordinator is not null)
            {
                var sessionCancellationToken = context.RequestAborted;
                correctionHandler = (request, oldCaptureToken) => DispatchCorrectionRegenerationAsync(
                    request,
                    webSocket,
                    sendGate,
                    connectionCorrelationIds,
                    oldCaptureToken,
                    sessionCancellationToken);
                _bargeInCoordinator.CorrectionRegenerationRequested += correctionHandler;
                liveUtteranceHandler = (routed, token) => DispatchLiveUtteranceRouteAsync(
                    routed,
                    webSocket,
                    sendGate,
                    token);
                _bargeInCoordinator.LiveUserUtteranceRouted += liveUtteranceHandler;
            }

            if (_bargeInDebugSnapshots?.IsEnabled == true)
            {
                bargeInDebugHandler = (snapshot, token) => DispatchBargeInDebugSnapshotAsync(
                    snapshot,
                    webSocket,
                    sendGate,
                    token);
                _bargeInDebugSnapshots.SnapshotAvailable += bargeInDebugHandler;
            }

            await ReceiveLoopAsync(
                webSocket,
                sendGate,
                connectionCorrelationIds,
                cancellation =>
                {
                    devVisualFlowCancellation?.Cancel();
                    devVisualFlowCancellation = cancellation;
                },
                context.RequestAborted);
        }
        finally
        {
            if (_bargeInCoordinator is not null && correctionHandler is not null)
            {
                _bargeInCoordinator.CorrectionRegenerationRequested -= correctionHandler;
            }

            if (_bargeInCoordinator is not null && liveUtteranceHandler is not null)
            {
                _bargeInCoordinator.LiveUserUtteranceRouted -= liveUtteranceHandler;
            }

            if (_bargeInDebugSnapshots is not null && bargeInDebugHandler is not null)
            {
                _bargeInDebugSnapshots.SnapshotAvailable -= bargeInDebugHandler;
            }

            devVisualFlowCancellation?.Cancel();
            _runtimeStateService.DecrementActiveWebSocketConnections();
            _logger.LogInformation(
                "WebSocket connection closed. ActiveConnections: {ActiveConnections}",
                _runtimeStateService.ActiveWebSocketConnections);
        }
    }

    private async Task ReceiveLoopAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        ConcurrentDictionary<string, byte> connectionCorrelationIds,
        Action<CancellationTokenSource> setDevVisualFlowCancellation,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveMessageAsync(webSocket, buffer, cancellationToken);

            if (message is null)
            {
                break;
            }

            if (message.Contains("\"voice_stream_chunk\"", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Incoming WebSocket voice stream chunk. Bytes: {Bytes}.", message.Length);
            }
            else
            {
                _logger.LogInformation("Incoming WebSocket message: {Message}", message);
            }

            if (await TryHandleVoiceStreamMessageAsync(webSocket, sendGate, connectionCorrelationIds, message, cancellationToken))
            {
                continue;
            }

            var request = DeserializeRequest(message);
            if (request is null)
            {
                var response = await ProcessMessageAsync(message, cancellationToken);
                await EmitProcessedResponseAsync(
                    webSocket,
                    sendGate,
                    null,
                    response,
                    setDevVisualFlowCancellation,
                    cancellationToken);
                continue;
            }

            await ProcessAndEmitLiveRequestAsync(
                webSocket,
                sendGate,
                request,
                connectionCorrelationIds,
                setDevVisualFlowCancellation,
                cancellationToken);
        }
    }

    private async Task<bool> TryHandleVoiceStreamMessageAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        ConcurrentDictionary<string, byte> connectionCorrelationIds,
        string rawMessage,
        CancellationToken cancellationToken)
    {
        VoiceStreamEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<VoiceStreamEnvelope>(rawMessage, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Type))
        {
            return false;
        }

        if (!envelope.Type.StartsWith("voice_stream_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var correlationId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : envelope.CorrelationId;

        try
        {
            switch (envelope.Type)
            {
                case "voice_stream_start":
                    _voiceStreamSessionService.Start(
                        correlationId,
                        envelope.SampleRate <= 0 ? 48000 : envelope.SampleRate,
                        envelope.Channels <= 0 ? 1 : envelope.Channels);
                    await SendJsonAsync(
                        webSocket,
                        sendGate,
                        JsonSerializer.Serialize(new
                        {
                            type = "voice_stream_ack",
                            correlationId,
                            phase = "started"
                        }, JsonSerializerOptions),
                        cancellationToken);
                    return true;

                case "voice_stream_chunk":
                    if (!string.IsNullOrWhiteSpace(envelope.Data))
                    {
                        _voiceStreamSessionService.AppendChunk(correlationId, Convert.FromBase64String(envelope.Data));
                    }

                    return true;

                case "voice_stream_cancel":
                    _voiceStreamSessionService.Cancel(correlationId);
                    return true;

                case "voice_stream_end":
                    var transcription = await _voiceStreamSessionService.CompleteAsync(correlationId, cancellationToken);
                    var transcript = transcription.Text?.Trim() ?? string.Empty;
                    await SendJsonAsync(
                        webSocket,
                        sendGate,
                        JsonSerializer.Serialize(new
                        {
                            type = "voice_transcript",
                            correlationId,
                            text = transcript
                        }, JsonSerializerOptions),
                        cancellationToken);

                    AssistantResponse response;
                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        response = new AssistantResponse
                        {
                            Success = false,
                            Message = "I did not catch that.",
                            SpokenText = "I did not catch that.",
                            SpeechCacheKey = "voice.empty_transcript",
                            PreferPhraseCache = true,
                            IsReplayableSpeech = true,
                            CorrelationId = correlationId,
                            ErrorCode = "EMPTY_TRANSCRIPT",
                            Intent = "voice_stream_transcription",
                            ResponseType = "error"
                        };
                    }
                    else
                    {
                        response = await ProcessLiveRequestAsync(
                            new AssistantRequest
                            {
                                Message = transcript,
                                CorrelationId = correlationId,
                                InteractionSource = "voice_stream",
                                ClientMode = envelope.ClientMode,
                                ReceivedAtUtc = DateTimeOffset.UtcNow
                            },
                            connectionCorrelationIds,
                            visualEvent => SendVisualEventAsync(webSocket, sendGate, visualEvent, cancellationToken),
                            cancellationToken);
                    }

                    try
                    {
                        if (CanEmitTurn(response.CorrelationId))
                        {
                            await SendResponseAsync(webSocket, sendGate, response, cancellationToken);
                            StartDevVisualFlowIfNeeded(
                                webSocket,
                                sendGate,
                                response,
                                _ => { },
                                cancellationToken);
                            await QueueSpeechIfNeededAsync(
                                webSocket,
                                sendGate,
                                new AssistantRequest
                                {
                                    Message = transcript,
                                    CorrelationId = correlationId,
                                    InteractionSource = "voice_stream",
                                    ClientMode = envelope.ClientMode
                                },
                                response,
                                cancellationToken);
                        }
                    }
                    finally
                    {
                        if (!string.IsNullOrWhiteSpace(transcript))
                        {
                            _liveTurnService.CompleteTurn(response.CorrelationId);
                        }
                    }
                    return true;
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Voice stream failed. CorrelationId: {CorrelationId}.", correlationId);
            var response = new AssistantResponse
            {
                Success = false,
                Message = $"Voice stream failed: {exception.Message}",
                CorrelationId = correlationId,
                ErrorCode = "VOICE_STREAM_FAILED",
                Intent = "voice_stream_transcription",
                ResponseType = "error"
            };
            await SendResponseAsync(webSocket, sendGate, response, cancellationToken);
            return true;
        }

        return true;
    }

    internal async Task DispatchCorrectionRegenerationAsync(
        CorrectionRegenerationRequested correction,
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        ConcurrentDictionary<string, byte> connectionCorrelationIds,
        CancellationToken oldCaptureCancellationToken,
        CancellationToken sessionCancellationToken)
    {
        if (!connectionCorrelationIds.ContainsKey(correction.OriginalCorrelationId))
        {
            return;
        }

        _recentRequestsByCorrelationId.TryGetValue(correction.OriginalCorrelationId, out var previousRequest);
        var buildResult = _correctionRequestBuilder.Build(new CorrectionRequestBuildInput(
            correction.CorrectionText,
            correction.OriginalCorrelationId,
            previousRequest));

        _logger.LogInformation(
            "Dispatching correction regeneration. OriginalCorrelationId: {OriginalCorrelationId}. NewCorrelationId: {NewCorrelationId}. Strategy: {Strategy}. OldCaptureTokenCancelled: {OldCaptureTokenCancelled}. SessionTokenCancelled: {SessionTokenCancelled}.",
            buildResult.OriginalCorrelationId,
            buildResult.NewCorrelationId,
            buildResult.Strategy,
            oldCaptureCancellationToken.IsCancellationRequested,
            sessionCancellationToken.IsCancellationRequested);

        await ProcessAndEmitLiveRequestAsync(
            webSocket,
            sendGate,
            buildResult.Request,
            connectionCorrelationIds,
            _ => { },
            sessionCancellationToken);
    }

    internal async Task DispatchLiveUtteranceRouteAsync(
        LiveUserUtteranceRouted routed,
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Live utterance route dispatched. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. Route: {Route}. Action: {Action}.",
            routed.Utterance.ActiveTurnId,
            routed.Utterance.CorrelationId,
            routed.Utterance.StateWhenCaptured,
            routed.Decision.Kind,
            routed.Decision.Action);

        var responseCorrelationId = RoutedResponseCorrelationId(routed.Utterance);
        if ((routed.Decision.Kind is UtteranceRouteKind.PauseAndClarify
                && routed.Decision.Action is "AskClarification" or "StopSpeechOnlyNoConfirmation" or "PauseActiveTurn")
            || routed.Decision.Action is "HoldForMoreSpeech")
        {
            await SendVisualStateAsync(webSocket, sendGate, "listening", cancellationToken);
            _logger.LogInformation(
                "VisualStateSetToPausedForUserSpeech. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. Action: {Action}.",
                routed.Utterance.ActiveTurnId,
                routed.Utterance.CorrelationId,
                routed.Decision.Action);
        }

        AssistantResponse? response = routed.Decision.Kind switch
        {
            UtteranceRouteKind.PauseAndClarify when routed.Decision.Action == "PauseAndConfirmCancel" => new AssistantResponse
            {
                Success = true,
                Message = BuildPendingStopQuestion(routed.Utterance),
                SpokenText = BuildPendingStopQuestion(routed.Utterance),
                CorrelationId = responseCorrelationId,
                Intent = "live_utterance_pause",
                ResponseType = "confirmation"
            },
            UtteranceRouteKind.QueueAfterActiveTurn => new AssistantResponse
            {
                Success = false,
                Message = "I heard that follow-up, but queueing active-flow requests is not implemented yet.",
                SpokenText = "I heard that follow-up, but queueing active-flow requests is not implemented yet.",
                CorrelationId = responseCorrelationId,
                ErrorCode = "NOT_IMPLEMENTED_QUEUE",
                Intent = "live_utterance_queue_after",
                ResponseType = "limitation"
            },
            UtteranceRouteKind.StatusQuestion => new AssistantResponse
            {
                Success = true,
                Message = $"I heard you while I was {FormatState(routed.Utterance.StateWhenCaptured)}.",
                SpokenText = $"I heard you while I was {FormatState(routed.Utterance.StateWhenCaptured)}.",
                CorrelationId = responseCorrelationId,
                Intent = "live_utterance_status",
                ResponseType = "assistant"
            },
            _ => null
        };

        if (response is null)
        {
            return;
        }

        await SendResponseAsync(webSocket, sendGate, response, cancellationToken);
        await QueueSpeechIfNeededAsync(
            webSocket,
            sendGate,
            new AssistantRequest
            {
                Message = routed.Utterance.Text,
                CorrelationId = responseCorrelationId,
                InteractionSource = "voice_correction",
                ClientMode = "voice"
            },
            response,
            cancellationToken);
    }

    internal static Task DispatchBargeInDebugSnapshotAsync(
        BargeInDebugSnapshot snapshot,
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "barge_in_debug_snapshot",
            snapshot
        }, JsonSerializerOptions);
        return SendJsonAsync(webSocket, sendGate, json, cancellationToken);
    }

    internal async Task<AssistantResponse> ProcessAndEmitLiveRequestAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantRequest request,
        ConcurrentDictionary<string, byte> connectionCorrelationIds,
        Action<CancellationTokenSource> setDevVisualFlowCancellation,
        CancellationToken cancellationToken)
    {
        var response = await ProcessLiveRequestAsync(
            request,
            connectionCorrelationIds,
            visualEvent => SendVisualEventAsync(webSocket, sendGate, visualEvent, cancellationToken),
            cancellationToken);

        try
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await EmitProcessedResponseAsync(
                    webSocket,
                    sendGate,
                    request,
                    response,
                    setDevVisualFlowCancellation,
                    cancellationToken);
            }
        }
        finally
        {
            CompleteLiveTurnIfNeeded(request, response.CorrelationId);
        }

        return response;
    }

    private async Task EmitProcessedResponseAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantRequest? request,
        AssistantResponse response,
        Action<CancellationTokenSource> setDevVisualFlowCancellation,
        CancellationToken cancellationToken)
    {
        if (!CanEmitTurn(response.CorrelationId))
        {
            return;
        }

        await SendResponseAsync(webSocket, sendGate, response, cancellationToken);
        StartDevVisualFlowIfNeeded(webSocket, sendGate, response, setDevVisualFlowCancellation, cancellationToken);
        await QueueSpeechIfNeededAsync(webSocket, sendGate, request, response, cancellationToken);
    }

    private async Task QueueSpeechIfNeededAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantRequest? request,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        if (!CanEmitTurn(response.CorrelationId))
        {
            return;
        }

        var speechDecision = _speechPolicyService.Decide(request, response);
        if (speechDecision.UsedLegacySpeakResponseFallback)
        {
            _logger.LogInformation(
                "Legacy speakResponse fallback used. CorrelationId: {CorrelationId}. SpeakResponse: {SpeakResponse}",
                response.CorrelationId,
                request?.SpeakResponse);
        }

        if (speechDecision.ShouldSpeak && speechDecision.ShouldQueue)
        {
            if (!string.IsNullOrWhiteSpace(response.CorrelationId))
            {
                _liveTurnService.UpdateTurnState(response.CorrelationId, LiveAssistantTurnState.Speaking);
            }

            await _speechPlaybackService.EnqueueAsync(
                speechDecision.SpeechTextOverride ?? ResponseSpeechText(response),
                response.CorrelationId,
                (visualEvent, token) => SendVisualEventAsync(webSocket, sendGate, visualEvent, token),
                response.SpeechCacheKey,
                response.SpeechCacheKey is not null ? response.IsReplayableSpeech : null,
                cancellationToken);
            return;
        }

        _logger.LogInformation(
            "Speech not queued. CorrelationId: {CorrelationId}. InteractionSource: {InteractionSource}. ClientMode: {ClientMode}. Success: {Success}. ResponseType: {ResponseType}. Intent: {Intent}. ToolName: {ToolName}. ShouldSpeak: {ShouldSpeak}. ShouldQueue: {ShouldQueue}.",
            response.CorrelationId,
            request?.InteractionSource,
            request?.ClientMode,
            response.Success,
            response.ResponseType,
            response.Intent,
            response.ToolName,
            speechDecision.ShouldSpeak,
            speechDecision.ShouldQueue);
    }

    private void StartDevVisualFlowIfNeeded(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantResponse response,
        Action<CancellationTokenSource> setCancellation,
        CancellationToken connectionCancellationToken)
    {
        if (response.DevVisualFlow is not { Count: > 0 })
        {
            return;
        }

        var flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(connectionCancellationToken);
        setCancellation(flowCancellation);
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await PlayDevVisualFlowAsync(webSocket, sendGate, response.DevVisualFlow, flowCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    flowCancellation.Dispose();
                }
            },
            CancellationToken.None);
    }

    private static async Task PlayDevVisualFlowAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        IReadOnlyList<DevVisualFlowStep> flow,
        CancellationToken cancellationToken)
    {
        foreach (var step in flow)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SendVisualStateAsync(webSocket, sendGate, step.State, cancellationToken);
            var delay = TimeSpan.FromSeconds(Math.Clamp(step.DurationSeconds, 0.25, 300.0));
            await Task.Delay(delay, cancellationToken);
        }

        await SendVisualStateAsync(webSocket, sendGate, "idle", cancellationToken);
    }

    private static Task SendVisualStateAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        string state,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(BuildVisualStatePacket(state), JsonSerializerOptions);
        return SendJsonAsync(webSocket, sendGate, json, cancellationToken);
    }

    private static object BuildVisualStatePacket(string state)
    {
        return state.Trim().ToLowerInvariant() switch
        {
            "thinking" => new { type = "visual_state", mode = "thinking", energy = 0.55, thinking_intensity = 1.0 },
            "speaking" => new { type = "visual_state", mode = "speaking", energy = 0.72, speech_energy = 0.75 },
            "listening" => new { type = "visual_state", mode = "listening", energy = 0.35 },
            "tool" or "executing" or "executing_tool" => new { type = "visual_state", mode = "tool", energy = 0.68, tool_intensity = 1.0 },
            "confirmation" => new { type = "visual_state", mode = "confirmation", energy = 0.42, confirmation_intensity = 1.0 },
            "error" => new { type = "visual_state", mode = "error", energy = 0.62, error_intensity = 1.0 },
            _ => new
            {
                type = "visual_state",
                mode = "idle",
                energy = 0.0,
                speech_energy = 0.0,
                thinking_intensity = 0.0,
                tool_intensity = 0.0,
                error_intensity = 0.0,
                confirmation_intensity = 0.0
            }
        };
    }

    private static string BuildPendingStopQuestion(UserUtterance utterance)
    {
        return utterance.StateWhenCaptured is LiveAssistantTurnState.AwaitingToolCommit
            ? "Do you want me to stop and not run that pending action?"
            : "Do you want me to stop that action?";
    }

    private static string RoutedResponseCorrelationId(UserUtterance utterance)
    {
        return !string.IsNullOrWhiteSpace(utterance.CorrelationId)
            ? utterance.CorrelationId
            : !string.IsNullOrWhiteSpace(utterance.ActiveTurnId)
                ? utterance.ActiveTurnId
                : Guid.NewGuid().ToString("N");
    }

    private static string FormatState(LiveAssistantTurnState state)
    {
        return state switch
        {
            LiveAssistantTurnState.ProcessingTurn => "processing that request",
            LiveAssistantTurnState.PlanningTool => "planning the tool action",
            LiveAssistantTurnState.AwaitingToolCommit => "waiting briefly before the tool action",
            LiveAssistantTurnState.ExecutingTool => "running the tool",
            LiveAssistantTurnState.Speaking => "speaking",
            _ => state.ToString()
        };
    }

    private static async Task<string?> ReceiveMessageAsync(
        System.Net.WebSockets.WebSocket webSocket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connection closed by client.",
                    cancellationToken);

                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                return string.Empty;
            }

            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    internal async Task<AssistantResponse> ProcessMessageAsync(string rawMessage, CancellationToken cancellationToken)
    {
        try
        {
            var request = JsonSerializer.Deserialize<AssistantRequest>(rawMessage, JsonSerializerOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.Message))
            {
                var correlationId = Guid.NewGuid().ToString("N");

                _logger.LogInformation(
                    "Unknown command. CorrelationId: {CorrelationId}. Command: {Command}",
                    correlationId,
                    request?.Message);

                return new AssistantResponse
                {
                    Success = false,
                    Message = "Unknown command.",
                    CorrelationId = correlationId,
                    ErrorCode = "UNKNOWN_COMMAND"
                };
            }

            return await _commandRouter.RouteAsync(request, cancellationToken);
        }
        catch (JsonException exception)
        {
            var correlationId = Guid.NewGuid().ToString("N");

            _logger.LogWarning(
                exception,
                "Invalid JSON received over WebSocket. CorrelationId: {CorrelationId}",
                correlationId);

            return new AssistantResponse
            {
                Success = false,
                Message = "Invalid JSON.",
                CorrelationId = correlationId,
                ErrorCode = "INVALID_JSON"
            };
        }
    }

    private Task<AssistantResponse> ProcessRequestAsync(
        AssistantRequest request,
        Func<AssistantVisualEvent, Task> sendVisualEventAsync,
        CancellationToken cancellationToken)
    {
        return _commandRouter.RouteAsync(
            new AssistantRequest
            {
                Message = request.Message,
                CorrelationId = request.CorrelationId,
                SpeakResponse = request.SpeakResponse,
                InteractionSource = request.InteractionSource,
                ClientMode = request.ClientMode,
                ReceivedAtUtc = DateTimeOffset.UtcNow,
                SpeechEventSender = (visualEvent, _) => sendVisualEventAsync(visualEvent)
            },
            cancellationToken);
    }

    private async Task<AssistantResponse> ProcessLiveRequestAsync(
        AssistantRequest request,
        ConcurrentDictionary<string, byte> connectionCorrelationIds,
        Func<AssistantVisualEvent, Task> sendVisualEventAsync,
        CancellationToken cancellationToken)
    {
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : request.CorrelationId;
        connectionCorrelationIds.TryAdd(correlationId, 0);

        var gateResponse = EvaluateVoiceRequestGate(request, correlationId);
        if (gateResponse is not null)
        {
            return gateResponse;
        }

        _recentRequestsByCorrelationId[correlationId] = new AssistantRequest
        {
            Message = request.Message,
            CorrelationId = correlationId,
            SpeakResponse = request.SpeakResponse,
            InteractionSource = request.InteractionSource,
            ClientMode = request.ClientMode,
            ReceivedAtUtc = request.ReceivedAtUtc
        };
        var turn = _liveTurnService.BeginTurn(
            "default",
            correlationId,
            assistantTurnId: correlationId,
            requestAborted: cancellationToken);

        try
        {
            var response = await ProcessRequestAsync(
                new AssistantRequest
                {
                    Message = request.Message,
                    CorrelationId = correlationId,
                    SpeakResponse = request.SpeakResponse,
                    InteractionSource = request.InteractionSource,
                    ClientMode = request.ClientMode,
                    ReceivedAtUtc = request.ReceivedAtUtc,
                    SpeechEventSender = (visualEvent, _) => sendVisualEventAsync(visualEvent)
                },
                sendVisualEventAsync,
                turn.CancellationToken);

            if (!CanEmitTurn(correlationId))
            {
                return response;
            }

            return response;
        }
        catch (OperationCanceledException) when (!_liveTurnService.ShouldEmit(correlationId) || cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Live turn processing cancelled. CorrelationId: {CorrelationId}.",
                correlationId);
            return new AssistantResponse
            {
                Success = false,
                Message = string.Empty,
                CorrelationId = correlationId,
                ErrorCode = "TURN_CANCELLED",
                ResponseType = "cancelled"
            };
        }
    }

    private bool CanEmitTurn(string? correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return true;
        }

        if (_liveTurnService.ShouldEmit(correlationId))
        {
            return true;
        }

        if (!_liveTurnService.IsCancelled(correlationId))
        {
            return true;
        }

        _logger.LogInformation(
            "Suppressing stale assistant response for live turn. CorrelationId: {CorrelationId}.",
            correlationId);
        return false;
    }

    private AssistantResponse? EvaluateVoiceRequestGate(AssistantRequest request, string correlationId)
    {
        if (_liveUtteranceGate is null || !IsVoiceRequest(request))
        {
            return null;
        }

        LiveAssistantTurn? activeTurn = null;
        if (!_liveTurnService.TryGetActiveTurn(correlationId, out activeTurn))
        {
            _liveTurnService.TryGetCurrentActiveTurn(out activeTurn);
        }

        var state = activeTurn?.State ?? LiveAssistantTurnState.IdleListening;
        var utterance = new UserUtterance
        {
            Text = request.Message.Trim(),
            TimestampUtc = request.ReceivedAtUtc ?? DateTimeOffset.UtcNow,
            ActiveTurnId = activeTurn?.AssistantTurnId,
            CorrelationId = correlationId,
            StateWhenCaptured = state,
            AssistantWasSpeaking = state is LiveAssistantTurnState.Speaking,
            Source = request.InteractionSource ?? "voice",
            Confidence = null
        };
        var result = _liveUtteranceGate.Evaluate(new LiveUtteranceGateInput
        {
            Utterance = utterance,
            ActiveTurn = activeTurn,
            CurrentSystemState = state.ToString(),
            AssistantWasSpeaking = utterance.AssistantWasSpeaking,
            IsIdleListening = activeTurn is null || state is LiveAssistantTurnState.IdleListening,
            PendingCommandDescription = activeTurn?.PendingCommandDescription,
            SttConfidence = utterance.Confidence
        });

        if (result.ShouldRouteToCommandRouter)
        {
            return null;
        }

        return result.Decision switch
        {
            LiveUtteranceGateDecisionKind.HoldForMoreSpeech => BuildGateResponse(
                correlationId,
                "LIVE_UTTERANCE_HELD",
                "live_utterance_hold",
                result.ClarificationPrompt ?? string.Empty,
                result),
            LiveUtteranceGateDecisionKind.AskClarification => BuildGateResponse(
                correlationId,
                "LIVE_UTTERANCE_CLARIFICATION",
                "live_utterance_clarification",
                result.ClarificationPrompt ?? "Sorry, I didn't catch that.",
                result,
                success: true,
                responseType: "confirmation"),
            LiveUtteranceGateDecisionKind.AcceptPlaybackControl
                or LiveUtteranceGateDecisionKind.AcceptCancellation
                or LiveUtteranceGateDecisionKind.AcceptContinuation => BuildGateResponse(
                    correlationId,
                    "LIVE_UTTERANCE_CONTROL_HANDLED",
                    "live_utterance_control",
                    string.Empty,
                    result),
            LiveUtteranceGateDecisionKind.IgnoreAsGarbageTranscript
                or LiveUtteranceGateDecisionKind.IgnoreAsNoise
                or LiveUtteranceGateDecisionKind.IgnoreAsEcho
                or LiveUtteranceGateDecisionKind.IgnoreAsWakewordLeak => BuildGateResponse(
                    correlationId,
                    "LIVE_UTTERANCE_IGNORED",
                    "live_utterance_ignored",
                    string.Empty,
                    result),
            _ => null
        };
    }

    private static AssistantResponse BuildGateResponse(
        string correlationId,
        string errorCode,
        string intent,
        string message,
        LiveUtteranceGateResult result,
        bool success = false,
        string responseType = "ignored")
    {
        return new AssistantResponse
        {
            Success = success,
            Message = message,
            SpokenText = message,
            CorrelationId = correlationId,
            ErrorCode = success ? null : errorCode,
            Intent = intent,
            IntentConfidence = result.Confidence,
            OriginalMessage = result.NormalizedText,
            ParserUsed = result.Decision.ToString(),
            CapabilityId = "live_utterance_gate",
            CapabilityName = result.Reason,
            ResponseType = responseType
        };
    }

    private static bool IsVoiceRequest(AssistantRequest request)
    {
        return string.Equals(request.InteractionSource, "voice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "voice_stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.ClientMode, "voice", StringComparison.OrdinalIgnoreCase);
    }

    private void CompleteLiveTurnIfNeeded(AssistantRequest? request, string correlationId)
    {
        if (request is null)
        {
            return;
        }

        _liveTurnService.CompleteTurn(correlationId);
    }

    private AssistantRequest? DeserializeRequest(string rawMessage)
    {
        try
        {
            return JsonSerializer.Deserialize<AssistantRequest>(rawMessage, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task SendResponseAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, JsonSerializerOptions);
        await SendJsonAsync(webSocket, sendGate, json, cancellationToken);
    }

    private static async Task SendVisualEventAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantVisualEvent visualEvent,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(visualEvent, JsonSerializerOptions);
        await SendJsonAsync(webSocket, sendGate, json, cancellationToken);
    }

    private static async Task SendJsonAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        string json,
        CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(json);

        await sendGate.WaitAsync(cancellationToken);
        try
        {
            if (webSocket.State != WebSocketState.Open)
            {
                return;
            }

            await webSocket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }
        finally
        {
            sendGate.Release();
        }
    }

    internal static string ResponseSpeechText(AssistantResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.SpokenText))
        {
            return response.SpokenText;
        }

        if (string.Equals(response.ResponseType, "confirmation", StringComparison.OrdinalIgnoreCase)
            && response.Confirmation is not null)
        {
            if (response.ApplicationCandidates is { Count: > 1 })
            {
                return "I found multiple apps matching that description, sir. Please choose which app you want to open.";
            }

            if (!string.IsNullOrWhiteSpace(response.Confirmation.DisplayName))
            {
                return $"I found {response.Confirmation.DisplayName}, but I have not handled this specific application before. Please confirm before I open it.";
            }
        }

        if (response.Success)
        {
            return response.Message;
        }

        if (!string.IsNullOrWhiteSpace(response.ErrorCode)
            && response.ErrorCode != "UNKNOWN_INPUT"
            && response.ErrorCode != "EMPTY_TRANSCRIPT"
            && response.ErrorCode != "MISSING_CAPABILITY"
            && response.ErrorCode != "UNSUPPORTED_ACTION")
        {
            return $"{response.ErrorCode} - {response.Message}";
        }

        return response.Message;
    }

    private sealed class VoiceStreamEnvelope
    {
        public string Type { get; init; } = string.Empty;

        public string? CorrelationId { get; init; }

        public int SampleRate { get; init; }

        public int Channels { get; init; }

        public string? Format { get; init; }

        public string? Data { get; init; }

        public string? ClientMode { get; init; }
    }
}
