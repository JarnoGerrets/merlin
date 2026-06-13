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

    public WebSocketHandler(
        CommandRouter commandRouter,
        IAssistantSpeechPlaybackService speechPlaybackService,
        ISpeechPolicyService speechPolicyService,
        ILogger<WebSocketHandler> logger,
        IRuntimeStateService runtimeStateService)
    {
        _commandRouter = commandRouter;
        _speechPlaybackService = speechPlaybackService;
        _speechPolicyService = speechPolicyService;
        _logger = logger;
        _runtimeStateService = runtimeStateService;
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
        _runtimeStateService.IncrementActiveWebSocketConnections();
        _logger.LogInformation(
            "WebSocket connection opened. ActiveConnections: {ActiveConnections}",
            _runtimeStateService.ActiveWebSocketConnections);

        try
        {
            await ReceiveLoopAsync(webSocket, sendGate, context.RequestAborted);
        }
        finally
        {
            _runtimeStateService.DecrementActiveWebSocketConnections();
            _logger.LogInformation(
                "WebSocket connection closed. ActiveConnections: {ActiveConnections}",
                _runtimeStateService.ActiveWebSocketConnections);
        }
    }

    private async Task ReceiveLoopAsync(
        System.Net.WebSockets.WebSocket webSocket,
        SemaphoreSlim sendGate,
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

            _logger.LogInformation("Incoming WebSocket message: {Message}", message);

            var request = DeserializeRequest(message);
            var response = await ProcessMessageAsync(message, cancellationToken);
            await SendResponseAsync(webSocket, sendGate, response, cancellationToken);

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
                    cancellationToken);
            }
        }
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

    private static string ResponseSpeechText(AssistantResponse response)
    {
        if (response.Success)
        {
            return response.Message;
        }

        if (!string.IsNullOrWhiteSpace(response.ErrorCode)
            && response.ErrorCode != "UNKNOWN_INPUT"
            && response.ErrorCode != "MISSING_CAPABILITY"
            && response.ErrorCode != "UNSUPPORTED_ACTION")
        {
            return $"{response.ErrorCode} - {response.Message}";
        }

        return response.Message;
    }
}
