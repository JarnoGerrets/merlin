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
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly IRuntimeStateService _runtimeStateService;

    public WebSocketHandler(
        CommandRouter commandRouter,
        ILogger<WebSocketHandler> logger,
        IRuntimeStateService runtimeStateService)
    {
        _commandRouter = commandRouter;
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
        _runtimeStateService.IncrementActiveWebSocketConnections();
        _logger.LogInformation(
            "WebSocket connection opened. ActiveConnections: {ActiveConnections}",
            _runtimeStateService.ActiveWebSocketConnections);

        try
        {
            await ReceiveLoopAsync(webSocket, context.RequestAborted);
        }
        finally
        {
            _runtimeStateService.DecrementActiveWebSocketConnections();
            _logger.LogInformation(
                "WebSocket connection closed. ActiveConnections: {ActiveConnections}",
                _runtimeStateService.ActiveWebSocketConnections);
        }
    }

    private async Task ReceiveLoopAsync(System.Net.WebSockets.WebSocket webSocket, CancellationToken cancellationToken)
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

            var response = await ProcessMessageAsync(message, cancellationToken);
            await SendResponseAsync(webSocket, response, cancellationToken);
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

    private static async Task SendResponseAsync(
        System.Net.WebSockets.WebSocket webSocket,
        AssistantResponse response,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, JsonSerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await webSocket.SendAsync(
            bytes,
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }
}
