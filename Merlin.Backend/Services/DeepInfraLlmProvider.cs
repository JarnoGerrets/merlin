using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class DeepInfraLlmProvider : IChatProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepInfraLlmProvider> _logger;
    private readonly LlmOptions _options;

    public DeepInfraLlmProvider(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<DeepInfraLlmProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "DeepInfra";

    public async Task<LlmProviderResult> GenerateAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DeepInfraApiKey))
        {
            return LlmProviderResult.Failed("missing_api_key", "DEEPINFRA_API_KEY is not configured.", retryable: false);
        }

        var request = new DeepInfraChatRequest(
            Model: _options.DeepInfraModel,
            Messages: messages.Select(message => new DeepInfraChatMessage(
                Role: NormalizeRole(message.Role),
                Content: message.Content)).ToArray(),
            Stream: false);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri())
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.DeepInfraApiKey);

        try
        {
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var error = ParseError(body);
                var retryable = IsRetryable(response.StatusCode, error.Code, error.Message);
                _logger.LogDebug(
                    "DeepInfra HTTP failure. Status: {Status}. ErrorCode: {ErrorCode}. Retryable: {Retryable}. ElapsedMs: {ElapsedMs}.",
                    (int)response.StatusCode,
                    error.Code,
                    retryable,
                    stopwatch.ElapsedMilliseconds);
                return LlmProviderResult.Failed(error.Code ?? ((int)response.StatusCode).ToString(), error.Message, retryable);
            }

            var result = JsonSerializer.Deserialize<DeepInfraChatResponse>(body, JsonOptions);
            var content = result?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return LlmProviderResult.Failed("empty_response", "DeepInfra returned an empty chat completion.", retryable: false);
            }

            return LlmProviderResult.Succeeded(content.Trim());
        }
        catch (HttpRequestException exception)
        {
            return LlmProviderResult.Failed("network_error", exception.Message, retryable: true);
        }
        catch (TaskCanceledException exception)
        {
            return LlmProviderResult.Failed("timeout", exception.Message, retryable: true);
        }
        catch (OperationCanceledException exception)
        {
            return LlmProviderResult.Failed("timeout", exception.Message, retryable: true);
        }
        catch (JsonException exception)
        {
            return LlmProviderResult.Failed("bad_json", exception.Message, retryable: false);
        }
    }

    private Uri BuildChatCompletionsUri()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.DeepInfraBaseUrl)
            ? "https://api.deepinfra.com/v1/openai"
            : _options.DeepInfraBaseUrl.TrimEnd('/');
        return new Uri($"{baseUrl}/chat/completions");
    }

    private static string NormalizeRole(string role)
    {
        return role.Trim().ToLowerInvariant() switch
        {
            "system" => "system",
            "assistant" => "assistant",
            "user" => "user",
            _ => "user"
        };
    }

    private static DeepInfraError ParseError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new DeepInfraError(null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                var code = TryGetString(errorElement, "code") ?? TryGetString(errorElement, "type");
                var message = TryGetString(errorElement, "message") ?? errorElement.ToString();
                return new DeepInfraError(code, message);
            }

            return new DeepInfraError(TryGetString(document.RootElement, "code"), TryGetString(document.RootElement, "message") ?? body);
        }
        catch (JsonException)
        {
            return new DeepInfraError(null, body);
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool IsRetryable(HttpStatusCode statusCode, string? errorCode, string? message)
    {
        if (statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
        {
            return true;
        }

        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
        {
            return false;
        }

        var reason = $"{errorCode} {message}".ToLowerInvariant();
        if (reason.Contains("engine_overloaded", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("model busy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (reason.Contains("invalid api key", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("insufficient credit", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("insufficient balance", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("invalid model", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("validation", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("policy", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("content", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return false;
    }

    private sealed record DeepInfraChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<DeepInfraChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record DeepInfraChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record DeepInfraChatResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<DeepInfraChoice>? Choices);

    private sealed record DeepInfraChoice(
        [property: JsonPropertyName("message")] DeepInfraChatMessage? Message);

    private sealed record DeepInfraError(string? Code, string? Message);
}
