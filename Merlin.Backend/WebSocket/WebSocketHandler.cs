using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Merlin.Backend.Models;
using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BargeIn;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.InterruptionIntelligence;
using Merlin.Backend.Services.LiveUtterance;
using Merlin.Backend.Services.Motion;
using Merlin.Backend.Services.SpeechPresence;
using Merlin.Backend.Services.Vision;
using Microsoft.Extensions.Options;

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
    private readonly ILiveSpokenAnswerTrackingService? _spokenAnswerTracking;
    private readonly ISpeechPolicyService _speechPolicyService;
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly IOptionsMonitor<VoiceInputOptions> _voiceInputOptions;
    private readonly IOptionsMonitor<ChatLogOptions> _chatLogOptions;
    private readonly VoiceStreamSessionService _voiceStreamSessionService;
    private readonly ISpeechPresenceDecisionLogSink? _speechPresenceDecisionLogSink;
    private readonly AssistantUiStateBroadcaster? _assistantUiStateBroadcaster;
    private readonly IBrowserWorkspaceService? _browserWorkspaceService;
    private readonly VisionGestureEventRouter? _visionGestureEventRouter;
    private readonly IMotionControlModeService? _motionControlModeService;
    private readonly IVisionSidecarHost? _visionSidecarHost;
    private readonly MerlinAwakeStateService _merlinAwakeState;
    private readonly WakeResponsePhraseLibrary _wakeResponsePhrases;
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
        IBargeInDebugSnapshotService? bargeInDebugSnapshots = null,
        IOptionsMonitor<VoiceInputOptions>? voiceInputOptions = null,
        IOptionsMonitor<ChatLogOptions>? chatLogOptions = null,
        ISpeechPresenceDecisionLogSink? speechPresenceDecisionLogSink = null,
        ILiveSpokenAnswerTrackingService? spokenAnswerTracking = null,
        AssistantUiStateBroadcaster? assistantUiStateBroadcaster = null,
        IBrowserWorkspaceService? browserWorkspaceService = null,
        VisionGestureEventRouter? visionGestureEventRouter = null,
        IMotionControlModeService? motionControlModeService = null,
        IVisionSidecarHost? visionSidecarHost = null,
        MerlinAwakeStateService? merlinAwakeState = null,
        WakeResponsePhraseLibrary? wakeResponsePhrases = null)
    {
        _commandRouter = commandRouter;
        _bargeInCoordinator = bargeInCoordinator;
        _correctionRequestBuilder = correctionRequestBuilder ?? new CorrectionRequestBuilder();
        _liveUtteranceGate = liveUtteranceGate;
        _liveTurnService = liveTurnService;
        _speechPlaybackService = speechPlaybackService;
        _spokenAnswerTracking = spokenAnswerTracking;
        _speechPolicyService = speechPolicyService;
        _logger = logger;
        _runtimeStateService = runtimeStateService;
        _voiceInputOptions = voiceInputOptions ?? new StaticOptionsMonitor<VoiceInputOptions>(new VoiceInputOptions());
        _chatLogOptions = chatLogOptions ?? new StaticOptionsMonitor<ChatLogOptions>(new ChatLogOptions());
        _voiceStreamSessionService = voiceStreamSessionService;
        _bargeInDebugSnapshots = bargeInDebugSnapshots;
        _speechPresenceDecisionLogSink = speechPresenceDecisionLogSink;
        _assistantUiStateBroadcaster = assistantUiStateBroadcaster;
        _browserWorkspaceService = browserWorkspaceService;
        _visionGestureEventRouter = visionGestureEventRouter;
        _motionControlModeService = motionControlModeService;
        _visionSidecarHost = visionSidecarHost;
        _merlinAwakeState = merlinAwakeState ?? new MerlinAwakeStateService();
        _wakeResponsePhrases = wakeResponsePhrases ?? new WakeResponsePhraseLibrary();
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
        Func<BackendVoiceRequestCaptured, CancellationToken, Task>? backendVoiceHandler = null;
        Func<LiveUserUtteranceRouted, CancellationToken, Task>? liveUtteranceHandler = null;
        Func<BargeInDebugSnapshot, CancellationToken, Task>? bargeInDebugHandler = null;
        Func<AssistantUiStateEvent, string, CancellationToken, Task>? assistantUiStateHandler = null;
        Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? browserWorkspaceStateHandler = null;
        Func<VisionGestureEvent, CancellationToken, Task>? visionGestureHandler = null;
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
                backendVoiceHandler = (request, token) => DispatchBackendVoiceRequestAsync(
                    request,
                    webSocket,
                    sendGate,
                    connectionCorrelationIds,
                    token);
                _bargeInCoordinator.BackendVoiceRequestCaptured += backendVoiceHandler;
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

            if (_assistantUiStateBroadcaster is not null)
            {
                assistantUiStateHandler = (uiState, source, token) => SendAssistantUiStateAsync(
                    webSocket,
                    sendGate,
                    uiState,
                    source,
                    token);
                _assistantUiStateBroadcaster.StateChanged += assistantUiStateHandler;
            }

            if (_browserWorkspaceService is not null)
            {
                browserWorkspaceStateHandler = (state, token) => SendBrowserWorkspaceStateAsync(
                    webSocket,
                    sendGate,
                    state,
                    token);
                _browserWorkspaceService.StateChanged += browserWorkspaceStateHandler;
                if (_browserWorkspaceService.IsActive)
                {
                    await SendBrowserWorkspaceStateAsync(
                        webSocket,
                        sendGate,
                        new BrowserWorkspaceStateChanged(true, _browserWorkspaceService.CurrentBounds, "connected"),
                        context.RequestAborted);
                }
            }

            if (_motionControlModeService is not null)
            {
                visionGestureHandler = (gestureEvent, token) => SendVisionGestureEventAsync(
                    webSocket,
                    sendGate,
                    gestureEvent,
                    token);
                _motionControlModeService.DashboardGestureForwarded += visionGestureHandler;
            }
            else if (_visionGestureEventRouter is not null)
            {
                visionGestureHandler = (gestureEvent, token) => SendVisionGestureEventAsync(
                    webSocket,
                    sendGate,
                    gestureEvent,
                    token);
                _visionGestureEventRouter.GestureEventForwarded += visionGestureHandler;
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

            if (_bargeInCoordinator is not null && backendVoiceHandler is not null)
            {
                _bargeInCoordinator.BackendVoiceRequestCaptured -= backendVoiceHandler;
            }

            if (_bargeInCoordinator is not null && liveUtteranceHandler is not null)
            {
                _bargeInCoordinator.LiveUserUtteranceRouted -= liveUtteranceHandler;
            }

            if (_bargeInDebugSnapshots is not null && bargeInDebugHandler is not null)
            {
                _bargeInDebugSnapshots.SnapshotAvailable -= bargeInDebugHandler;
            }

            if (_assistantUiStateBroadcaster is not null && assistantUiStateHandler is not null)
            {
                _assistantUiStateBroadcaster.StateChanged -= assistantUiStateHandler;
            }

            if (_browserWorkspaceService is not null && browserWorkspaceStateHandler is not null)
            {
                _browserWorkspaceService.StateChanged -= browserWorkspaceStateHandler;
            }

            if (_motionControlModeService is not null && visionGestureHandler is not null)
            {
                _motionControlModeService.DashboardGestureForwarded -= visionGestureHandler;
            }
            else if (_visionGestureEventRouter is not null && visionGestureHandler is not null)
            {
                _visionGestureEventRouter.GestureEventForwarded -= visionGestureHandler;
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

            if (TryHandleSpeechPresenceMarkerMessage(message))
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

        if (ShouldRejectFrontendVoiceStream())
        {
            if (string.Equals(envelope.Type, "voice_stream_chunk", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "VoiceStreamRejectedBackendOwnedVoiceMode. Type: {Type}. CorrelationId: {CorrelationId}.",
                    envelope.Type,
                    correlationId);
            }
            else
            {
                _logger.LogInformation(
                    "VoiceStreamRejectedBackendOwnedVoiceMode. Type: {Type}. CorrelationId: {CorrelationId}. Owner: {Owner}. FrontendVoiceInputEnabled: {FrontendVoiceInputEnabled}.",
                    envelope.Type,
                    correlationId,
                    _voiceInputOptions.CurrentValue.Owner,
                    _voiceInputOptions.CurrentValue.FrontendVoiceInputEnabled);
            }

            if (string.Equals(envelope.Type, "voice_stream_cancel", StringComparison.OrdinalIgnoreCase))
            {
                _voiceStreamSessionService.Cancel(correlationId);
            }

            return true;
        }

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
                    await EmitAssistantUiStateImmediateAsync(
                        AssistantUiStateEvent.Create(
                            "listening",
                            "voice_stream_capture_started",
                            correlationId,
                            correlationId,
                            interruptionState: "capturing"),
                        nameof(WebSocketHandler),
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
                            text = transcript,
                            timestampUtc = DateTimeOffset.UtcNow
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

    internal bool ShouldRejectFrontendVoiceStream()
    {
        return _voiceInputOptions.CurrentValue.IsBackendOwnedMode;
    }

    internal bool TryHandleSpeechPresenceMarkerMessage(string rawMessage)
    {
        SpeechPresenceMarkerEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SpeechPresenceMarkerEnvelope>(rawMessage, JsonSerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (envelope is null
            || !string.Equals(envelope.Type, "speech_presence_marker", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(envelope.MarkerType, "user_started_speaking", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "SpeechPresenceMarkerIgnored. MarkerType: {MarkerType}. Source: {Source}.",
                envelope.MarkerType,
                envelope.Source);
            return true;
        }

        var source = string.IsNullOrWhiteSpace(envelope.Source)
            ? "frontend_debug_button"
            : envelope.Source.Trim();
        var marker = new SpeechPresenceManualMarker
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            MarkerType = "user_started_speaking",
            Source = source,
            ClientTimestampUtc = TryParseClientTimestamp(envelope.ClientTimestampUtc),
            Note = "manual speech start marker"
        };
        _speechPresenceDecisionLogSink?.TryLogManualSpeechStartMarker(marker);
        _logger.LogInformation(
            "ManualSpeechStartMarkerReceived. MarkerType: {MarkerType}. Source: {Source}. TimestampUtc: {TimestampUtc}. ClientTimestampUtc: {ClientTimestampUtc}.",
            marker.MarkerType,
            marker.Source,
            marker.TimestampUtc,
            marker.ClientTimestampUtc);
        return true;
    }

    private static DateTimeOffset? TryParseClientTimestamp(string? clientTimestampUtc)
    {
        if (string.IsNullOrWhiteSpace(clientTimestampUtc))
        {
            return null;
        }

        return DateTimeOffset.TryParse(clientTimestampUtc, out var timestamp)
            ? timestamp.ToUniversalTime()
            : null;
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

    internal async Task DispatchBackendVoiceRequestAsync(
        BackendVoiceRequestCaptured request,
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        ConcurrentDictionary<string, byte> connectionCorrelationIds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Dispatching backend idle voice request. CorrelationId: {CorrelationId}. Source: {Source}. Text: {Text}.",
            request.CorrelationId,
            request.InteractionSource,
            request.Text);

        await ProcessAndEmitLiveRequestAsync(
            webSocket,
            sendGate,
            new AssistantRequest
            {
                Message = request.Text,
                CorrelationId = request.CorrelationId,
                CaptureId = request.CaptureId,
                InteractionSource = request.InteractionSource,
                ClientMode = "orb",
                ReceivedAtUtc = request.Utterance.TimestampUtc
            },
            connectionCorrelationIds,
            _ => { },
            cancellationToken);
    }

    internal async Task DispatchLiveUtteranceRouteAsync(
        LiveUserUtteranceRouted routed,
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Live utterance route dispatched. CaptureId: {CaptureId}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. StateWhenCaptured: {StateWhenCaptured}. Route: {Route}. Action: {Action}.",
            routed.Utterance.CaptureId,
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
            await EmitAssistantUiStateImmediateAsync(
                AssistantUiStateEvent.Create(
                    "listening",
                    "live_utterance_route_listening",
                    routed.Utterance.CorrelationId,
                    routed.Utterance.ActiveTurnId,
                    overlayState: "none",
                    audiblePlaybackActive: false,
                    interruptionState: "capturing"),
                nameof(WebSocketHandler),
                cancellationToken);
            _logger.LogInformation(
                "VisualStateSetToPausedForUserSpeech. CaptureId: {CaptureId}. ActiveTurnId: {ActiveTurnId}. CorrelationId: {CorrelationId}. Action: {Action}.",
                routed.Utterance.CaptureId,
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
                CaptureId = routed.Utterance.CaptureId,
                Intent = "live_utterance_pause",
                ResponseType = "confirmation"
            },
            UtteranceRouteKind.QueueAfterActiveTurn => new AssistantResponse
            {
                Success = false,
                Message = "I heard that follow-up, but queueing active-flow requests is not implemented yet.",
                SpokenText = "I heard that follow-up, but queueing active-flow requests is not implemented yet.",
                CorrelationId = responseCorrelationId,
                CaptureId = routed.Utterance.CaptureId,
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
                CaptureId = routed.Utterance.CaptureId,
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
                CaptureId = routed.Utterance.CaptureId,
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
        await SendUiPanelEventIfNeededAsync(webSocket, sendGate, response, cancellationToken);
        var responseUiState = BuildResponseUiState(response);
        if (responseUiState is not null)
        {
            await EmitAssistantUiStateImmediateAsync(
                responseUiState,
                nameof(WebSocketHandler),
                cancellationToken);
        }

        StartDevVisualFlowIfNeeded(webSocket, sendGate, response, setDevVisualFlowCancellation, cancellationToken);
        await QueueSpeechIfNeededAsync(webSocket, sendGate, request, response, cancellationToken);
    }

    private Task SendUiPanelEventIfNeededAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        var eventName = response.Intent switch
        {
            "ui_panel_show" => "UI_PANEL_SHOW",
            "ui_panel_hide" => "UI_PANEL_HIDE",
            "ui_control_mode_start" => "UI_CONTROL_MODE_STARTED",
            "ui_control_mode_stop" => "UI_CONTROL_MODE_STOPPED",
            _ => null
        };
        if (eventName is null)
        {
            return Task.CompletedTask;
        }

        var isPanelEvent = response.Intent is "ui_panel_show" or "ui_panel_hide";
        if (isPanelEvent && !_chatLogOptions.CurrentValue.Enabled)
        {
            return Task.CompletedTask;
        }

        return SendJsonAsync(
            webSocket,
            sendGate,
            JsonSerializer.Serialize(new
            {
                type = eventName,
                @event = eventName,
                panelId = isPanelEvent
                    ? "chatlog"
                    : null,
                correlationId = response.CorrelationId
            }, JsonSerializerOptions),
            cancellationToken);
    }

    private Task SendVisionGestureEventAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        VisionGestureEvent gestureEvent,
        CancellationToken cancellationToken)
    {
        var eventName = gestureEvent.Type switch
        {
            "gesture.pointer.move" => "GESTURE_POINTER_MOVE",
            "gesture.pinch.start" => "GESTURE_PINCH_START",
            "gesture.pinch.move" => "GESTURE_PINCH_MOVE",
            "gesture.pinch.end" => "GESTURE_PINCH_END",
            _ => null
        };

        if (eventName is null)
        {
            return Task.CompletedTask;
        }

        return SendJsonAsync(
            webSocket,
            sendGate,
            JsonSerializer.Serialize(new
            {
                type = eventName,
                @event = eventName,
                pointerId = gestureEvent.PointerId,
                x = gestureEvent.X,
                y = gestureEvent.Y,
                confidence = gestureEvent.Confidence,
                source = gestureEvent.Source
            }, JsonSerializerOptions),
            cancellationToken);
    }

    private Task SendBrowserWorkspaceStateAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        BrowserWorkspaceStateChanged state,
        CancellationToken cancellationToken)
    {
        return SendJsonAsync(
            webSocket,
            sendGate,
            JsonSerializer.Serialize(new
            {
                type = "BROWSER_WORKSPACE_STATE",
                @event = "BROWSER_WORKSPACE_STATE",
                active = state.Active,
                reason = state.Reason,
                bounds = state.Bounds is null
                    ? null
                    : new
                    {
                        x = state.Bounds.X,
                        y = state.Bounds.Y,
                        width = state.Bounds.Width,
                        height = state.Bounds.Height,
                        isMinimized = state.Bounds.IsMinimized,
                        isFocused = state.Bounds.IsFocused
                }
            }, JsonSerializerOptions),
            cancellationToken);
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

        if (response.SegmentedSpeechStarted)
        {
            if (ShouldAppendAssistantChatLog(request, response, speechDecision))
            {
                await SendChatLogAppendAsync(
                    webSocket,
                    sendGate,
                    "assistant",
                    speechDecision.SpeechTextOverride ?? ResponseSpeechText(response),
                    "segmented_tts",
                    response.CorrelationId,
                    cancellationToken);
            }

            _logger.LogInformation(
                "Skipping full-response speech enqueue because segmented streaming speech already started. CorrelationId: {CorrelationId}.",
                response.CorrelationId);
            return;
        }

        if (speechDecision.ShouldSpeak && speechDecision.ShouldQueue)
        {
            var speechText = speechDecision.SpeechTextOverride ?? ResponseSpeechText(response);
            var shouldStartPinchCalibrationAfterSpeech = ShouldStartPinchCalibrationAfterSpeech(response);
            var shouldStartMotionRegionCalibrationAfterSpeech = ShouldStartMotionRegionCalibrationAfterSpeech(response);
            var pinchCalibrationStarted = 0;
            var motionRegionCalibrationStarted = 0;
            await SendChatLogAppendAsync(
                webSocket,
                sendGate,
                "assistant",
                speechText,
                "tts",
                response.CorrelationId,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(response.CorrelationId))
            {
                if (ShouldTrackMainAnswerSpeech(request, response))
                {
                    _spokenAnswerTracking?.StartAnswer(
                        response.CorrelationId,
                        response.CorrelationId,
                        request?.Message ?? string.Empty,
                        speechText);
                }

                _liveTurnService.UpdateTurnState(response.CorrelationId, LiveAssistantTurnState.Speaking);
            }

            await _speechPlaybackService.EnqueueAsync(
                speechText,
                response.CorrelationId,
                async (visualEvent, token) =>
                {
                    await SendVisualEventAsync(webSocket, sendGate, visualEvent, token);
                    if (shouldStartPinchCalibrationAfterSpeech
                        && string.Equals(visualEvent.Event, "SPEAKING_END", StringComparison.Ordinal)
                        && Interlocked.Exchange(ref pinchCalibrationStarted, 1) == 0)
                    {
                        StartPinchCalibrationAfterSpeech(response.CorrelationId);
                    }

                    if (shouldStartMotionRegionCalibrationAfterSpeech
                        && string.Equals(visualEvent.Event, "SPEAKING_END", StringComparison.Ordinal)
                        && Interlocked.Exchange(ref motionRegionCalibrationStarted, 1) == 0)
                    {
                        StartMotionRegionCalibrationAfterSpeech(response.CorrelationId);
                    }
                },
                response.SpeechCacheKey,
                response.SpeechCacheKey is not null ? response.IsReplayableSpeech : null,
                cancellationToken,
                DetermineSpeechPlaybackItemType(response));
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

    private static SpeechPlaybackItemType DetermineSpeechPlaybackItemType(AssistantResponse response)
    {
        return string.Equals(response.ResponseType, "sleep_acknowledgement", StringComparison.OrdinalIgnoreCase)
            ? SpeechPlaybackItemType.SleepAcknowledgement
            : SpeechPlaybackItemType.FinalAnswer;
    }

    private bool ShouldStartPinchCalibrationAfterSpeech(AssistantResponse response)
    {
        return response.Success
            && _visionSidecarHost is not null
            && string.Equals(response.Intent, "ui_control_pinch_calibration", StringComparison.OrdinalIgnoreCase);
    }

    private bool ShouldStartMotionRegionCalibrationAfterSpeech(AssistantResponse response)
    {
        return response.Success
            && _visionSidecarHost is not null
            && string.Equals(response.Intent, "vision_motion_region_calibration", StringComparison.OrdinalIgnoreCase);
    }

    private void StartPinchCalibrationAfterSpeech(string? correlationId)
    {
        if (_visionSidecarHost is null)
        {
            return;
        }

        _logger.LogInformation(
            "UiControlPinchCalibrationSpeechCompletedStartingCalibration CorrelationId: {CorrelationId}.",
            correlationId);
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _visionSidecarHost.CalibratePinchAsync(CancellationToken.None);
                _logger.LogInformation(
                    "UiControlPinchCalibrationBackgroundCompleted Success: {Success}. Start: {Start}. Hold: {Hold}. Release: {Release}. OpenSamples: {OpenSamples}. PinchSamples: {PinchSamples}. ReleaseSamples: {ReleaseSamples}. Message: {Message}.",
                    result.Success,
                    result.PinchStartRatio,
                    result.PinchHoldRatio,
                    result.PinchReleaseRatio,
                    result.OpenSamples,
                    result.PinchSamples,
                    result.ReleaseSamples,
                    result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "UiControlPinchCalibrationBackgroundFailed");
            }
        }, CancellationToken.None);
    }

    private void StartMotionRegionCalibrationAfterSpeech(string? correlationId)
    {
        if (_visionSidecarHost is null)
        {
            return;
        }

        _logger.LogInformation(
            "VisionMotionRegionCalibrationSpeechCompletedStartingCalibration CorrelationId: {CorrelationId}.",
            correlationId);
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await _visionSidecarHost.CalibrateMotionRegionAsync(CancellationToken.None);
                _logger.LogInformation(
                    "VisionMotionRegionCalibrationBackgroundCompleted Success: {Success}. Left: {Left}. Top: {Top}. Right: {Right}. Bottom: {Bottom}. TopLeftSamples: {TopLeftSamples}. TopRightSamples: {TopRightSamples}. BottomRightSamples: {BottomRightSamples}. BottomLeftSamples: {BottomLeftSamples}. Message: {Message}.",
                    result.Success,
                    result.ControlRegionLeft,
                    result.ControlRegionTop,
                    result.ControlRegionRight,
                    result.ControlRegionBottom,
                    result.TopLeftSamples,
                    result.TopRightSamples,
                    result.BottomRightSamples,
                    result.BottomLeftSamples,
                    result.Message);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "VisionMotionRegionCalibrationBackgroundFailed");
            }
        }, CancellationToken.None);
    }

    internal static bool ShouldAppendAssistantChatLog(
        AssistantRequest? request,
        AssistantResponse response,
        SpeechPolicyDecision speechDecision)
    {
        if (string.IsNullOrWhiteSpace(response.CorrelationId))
        {
            return false;
        }

        if (!speechDecision.ShouldSpeak)
        {
            return false;
        }

        return IsVoiceRequest(request ?? new AssistantRequest());
    }

    private Task SendChatLogAppendAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        string role,
        string text,
        string source,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!_chatLogOptions.CurrentValue.Enabled || string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        return SendJsonAsync(
            webSocket,
            sendGate,
            JsonSerializer.Serialize(new
            {
                @event = "UI_CHATLOG_APPEND",
                panelId = "chatlog",
                role,
                text = text.Trim(),
                timestampUtc = DateTimeOffset.UtcNow,
                source,
                correlationId
            }, JsonSerializerOptions),
            cancellationToken);
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

    internal static bool ShouldTrackMainAnswerSpeech(AssistantRequest? request, AssistantResponse response)
    {
        if (request is null || !response.Success)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Message) || string.IsNullOrWhiteSpace(response.CorrelationId))
        {
            return false;
        }

        if (response.SuppressSpeech || !string.IsNullOrWhiteSpace(response.SpeechCacheKey))
        {
            return false;
        }

        if (!string.Equals(response.ToolName, "General Conversation", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(response.Intent, "general_conversation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "assistant", StringComparison.OrdinalIgnoreCase);
    }

    internal static AssistantUiStateEvent? BuildResponseUiState(AssistantResponse response)
    {
        if (IsConfirmationResponse(response))
        {
            return AssistantUiStateEvent.Create(
                "thinking",
                "confirmation_required",
                response.CorrelationId,
                response.CorrelationId,
                overlayState: "confirmation");
        }

        if (IsErrorResponse(response))
        {
            return AssistantUiStateEvent.Create(
                "idle",
                "error_response_generated",
                response.CorrelationId,
                response.CorrelationId,
                overlayState: "error");
        }

        return null;
    }

    private static bool IsConfirmationResponse(AssistantResponse response)
    {
        return string.Equals(response.ResponseType, "confirmation", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ErrorCode, "CONFIRMATION_REQUIRED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsErrorResponse(AssistantResponse response)
    {
        if (response.Success)
        {
            return false;
        }

        if (string.Equals(response.ResponseType, "cancelled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "ignored", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(response.ErrorCode)
            || string.Equals(response.ResponseType, "error", StringComparison.OrdinalIgnoreCase)
            || string.Equals(response.ResponseType, "limitation", StringComparison.OrdinalIgnoreCase);
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
                CaptureId = request.CaptureId,
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

        await EmitAssistantUiStateCoalescedAsync(
            AssistantUiStateEvent.Create(
                "thinking",
                "request_accepted",
                correlationId,
                correlationId,
                overlayState: "none"),
            nameof(WebSocketHandler),
            cancellationToken);

        _recentRequestsByCorrelationId[correlationId] = new AssistantRequest
        {
            Message = request.Message,
            CorrelationId = correlationId,
            CaptureId = request.CaptureId,
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
                    CaptureId = request.CaptureId,
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
        if (!IsVoiceRequest(request))
        {
            return null;
        }

        var awakeResult = _merlinAwakeState?.EvaluateVoiceActivity(
            request.Message,
            correlationId,
            correlationId);
        if (awakeResult is not null)
        {
            if (awakeResult.IsWakePhrase)
            {
                var wakePhrase = _wakeResponsePhrases.Select(DateTimeOffset.UtcNow);
                return BuildAwakeGateResponse(
                    correlationId,
                    "WAKE_PHRASE_ACCEPTED",
                    "wake_phrase_accepted",
                    wakePhrase.Text,
                    awakeResult,
                    speechCacheKey: wakePhrase.Id,
                    success: true,
                    responseType: "wake_acknowledgement");
            }

            if (awakeResult.IsSleepPhrase && awakeResult.ShouldAllow)
            {
                var sleepPhrase = _wakeResponsePhrases.SelectSleep(DateTimeOffset.UtcNow);
                return BuildAwakeGateResponse(
                    correlationId,
                    "SLEEP_PHRASE_ACCEPTED",
                    "sleep_phrase_accepted",
                    sleepPhrase.Text,
                    awakeResult,
                    speechCacheKey: sleepPhrase.Id,
                    success: true,
                    responseType: "sleep_acknowledgement");
            }

            if (!awakeResult.ShouldAllow)
            {
                return BuildAwakeGateResponse(
                    correlationId,
                    "MERLIN_SLEEPING",
                    "merlin_sleeping",
                    string.Empty,
                    awakeResult);
            }
        }

        if (_liveUtteranceGate is null)
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
            _merlinAwakeState?.TouchActivity();
            return null;
        }

        if (result.Decision is LiveUtteranceGateDecisionKind.AcceptPlaybackControl
            or LiveUtteranceGateDecisionKind.AcceptCancellation
            or LiveUtteranceGateDecisionKind.AcceptContinuation
            or LiveUtteranceGateDecisionKind.AcceptCorrection
            or LiveUtteranceGateDecisionKind.AcceptReplacement
            or LiveUtteranceGateDecisionKind.AcceptStatusQuestion
            or LiveUtteranceGateDecisionKind.AskClarification)
        {
            _merlinAwakeState?.TouchActivity();
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

    private static AssistantResponse BuildAwakeGateResponse(
        string correlationId,
        string errorCode,
        string intent,
        string message,
        MerlinAwakeGateResult result,
        string? speechCacheKey = null,
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
            IntentConfidence = result.ShouldAllow ? 1.0 : 0.0,
            SpeechCacheKey = speechCacheKey,
            PreferPhraseCache = !string.IsNullOrWhiteSpace(speechCacheKey),
            IsReplayableSpeech = !string.IsNullOrWhiteSpace(speechCacheKey),
            OriginalMessage = result.Reason,
            ParserUsed = nameof(MerlinAwakeStateService),
            CapabilityId = "merlin_awake_state",
            CapabilityName = result.Reason,
            ResponseType = responseType
        };
    }

    private static bool IsVoiceRequest(AssistantRequest request)
    {
        return string.Equals(request.InteractionSource, "voice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "voice_stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "voice_correction", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "backend_idle_voice", StringComparison.OrdinalIgnoreCase)
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

    private async Task SendVisualEventAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantVisualEvent visualEvent,
        CancellationToken cancellationToken)
    {
        if (visualEvent.AssistantUiState is not null)
        {
            await EmitAssistantUiStateImmediateAsync(
                visualEvent.AssistantUiState,
                visualEvent.AssistantUiStateSource ?? nameof(WebSocketHandler),
                cancellationToken);
            return;
        }

        var json = JsonSerializer.Serialize(visualEvent, JsonSerializerOptions);
        await SendJsonAsync(webSocket, sendGate, json, cancellationToken);
    }

    private Task EmitAssistantUiStateImmediateAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        return _assistantUiStateBroadcaster?.EmitImmediateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    private Task EmitAssistantUiStateCoalescedAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        return _assistantUiStateBroadcaster?.RequestCoalescedStateAsync(uiState, source, cancellationToken)
            ?? Task.CompletedTask;
    }

    private async Task SendAssistantUiStateAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(uiState, JsonSerializerOptions);
        await SendJsonAsync(webSocket, sendGate, json, cancellationToken);
    }

    private static async Task SendJsonAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        string json,
        CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open || cancellationToken.IsCancellationRequested)
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

    private sealed class SpeechPresenceMarkerEnvelope
    {
        public string Type { get; init; } = string.Empty;

        public string MarkerType { get; init; } = string.Empty;

        public string? ClientTimestampUtc { get; init; }

        public string Source { get; init; } = string.Empty;
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public T CurrentValue { get; }

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
