using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Merlin.Backend.Models;
using Merlin.Backend.Services;

namespace Merlin.Backend.WebSocket;

public sealed class WebSocketHandler
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly CommandRouter _commandRouter;
    private readonly IAssistantSpeechPlaybackService _speechPlaybackService;
    private readonly ISpeechPolicyService _speechPolicyService;
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IRuntimeStateService _runtimeStateService;
    private readonly VoiceStreamSessionService _voiceStreamSessionService;

    public WebSocketHandler(
        CommandRouter commandRouter,
        IAssistantSpeechPlaybackService speechPlaybackService,
        ISpeechPolicyService speechPolicyService,
        ILogger<WebSocketHandler> logger,
        IRuntimeStateService runtimeStateService,
        VoiceStreamSessionService voiceStreamSessionService)
    {
        _commandRouter = commandRouter;
        _speechPlaybackService = speechPlaybackService;
        _speechPolicyService = speechPolicyService;
        _logger = logger;
        _runtimeStateService = runtimeStateService;
        _voiceStreamSessionService = voiceStreamSessionService;
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
        CancellationTokenSource? devVisualFlowCancellation = null;
        _runtimeStateService.IncrementActiveWebSocketConnections();
        _logger.LogInformation(
            "WebSocket connection opened. ActiveConnections: {ActiveConnections}",
            _runtimeStateService.ActiveWebSocketConnections);

        try
        {
            await ReceiveLoopAsync(
                webSocket,
                sendGate,
                cancellation =>
                {
                    devVisualFlowCancellation?.Cancel();
                    devVisualFlowCancellation = cancellation;
                },
                context.RequestAborted);
        }
        finally
        {
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

            if (await TryHandleVoiceStreamMessageAsync(webSocket, sendGate, message, cancellationToken))
            {
                continue;
            }

            var request = DeserializeRequest(message);
            var response = request is null
                ? await ProcessMessageAsync(message, cancellationToken)
                : await ProcessRequestAsync(
                    request,
                    visualEvent => SendVisualEventAsync(webSocket, sendGate, visualEvent, cancellationToken),
                    cancellationToken);
            await SendResponseAsync(webSocket, sendGate, response, cancellationToken);
            StartDevVisualFlowIfNeeded(webSocket, sendGate, response, setDevVisualFlowCancellation, cancellationToken);

            await QueueSpeechIfNeededAsync(webSocket, sendGate, request, response, cancellationToken);
        }
    }

    private async Task<bool> TryHandleVoiceStreamMessageAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
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
                        response = await _commandRouter.RouteAsync(
                            new AssistantRequest
                            {
                                Message = transcript,
                                CorrelationId = correlationId,
                                InteractionSource = "voice_stream",
                                ClientMode = envelope.ClientMode,
                                ReceivedAtUtc = DateTimeOffset.UtcNow,
                                SpeechEventSender = (visualEvent, token) => SendVisualEventAsync(webSocket, sendGate, visualEvent, token)
                            },
                            cancellationToken);
                    }

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

    private async Task QueueSpeechIfNeededAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
        AssistantRequest? request,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
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
