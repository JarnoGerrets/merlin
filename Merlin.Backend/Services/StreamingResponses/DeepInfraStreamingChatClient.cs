using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.StreamingResponses;

public sealed class DeepInfraStreamingChatClient : IAssistantTextGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;
    private readonly ILogger<DeepInfraStreamingChatClient> _logger;

    public DeepInfraStreamingChatClient(
        HttpClient httpClient,
        IOptions<LlmOptions> options,
        ILogger<DeepInfraStreamingChatClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<ModelTextDelta> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.DeepInfraApiKey))
        {
            throw new InvalidOperationException("DEEPINFRA_API_KEY is not configured.");
        }

        var request = new DeepInfraChatRequest(
            _options.DeepInfraModel,
            messages.Select(message => new DeepInfraChatMessage(NormalizeRole(message.Role), message.Content)).ToArray(),
            Stream: true);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri())
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.DeepInfraApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var stopwatch = Stopwatch.StartNew();
        var deltaCount = 0;
        var charCount = 0;
        var firstDeltaLogged = false;

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ModelTextDelta(string.Empty, IsFinal: true, Provider: "DeepInfra", Model: _options.DeepInfraModel, SequenceNumber: deltaCount);
                break;
            }

            var text = TryParseDelta(data);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (!firstDeltaLogged)
            {
                firstDeltaLogged = true;
                _logger.LogInformation(
                    "ModelStreamFirstDeltaReceived Provider: DeepInfra. Model: {Model}. FirstDeltaMs: {FirstDeltaMs}.",
                    _options.DeepInfraModel,
                    stopwatch.ElapsedMilliseconds);
            }

            deltaCount++;
            charCount += text.Length;
            yield return new ModelTextDelta(text, Provider: "DeepInfra", Model: _options.DeepInfraModel, SequenceNumber: deltaCount);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "ModelStreamCompleted Provider: DeepInfra. Model: {Model}. Deltas: {Deltas}. Chars: {Chars}. ElapsedMs: {ElapsedMs}.",
            _options.DeepInfraModel,
            deltaCount,
            charCount,
            stopwatch.ElapsedMilliseconds);
    }

    public static string? TryParseDelta(string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data);
            var choice = document.RootElement.GetProperty("choices").EnumerateArray().FirstOrDefault();
            if (choice.ValueKind is JsonValueKind.Undefined)
            {
                return null;
            }

            if (choice.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("content", out var content)
                && content.ValueKind is JsonValueKind.String)
            {
                return content.GetString();
            }

            if (choice.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var messageContent)
                && messageContent.ValueKind is JsonValueKind.String)
            {
                return messageContent.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
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

    private sealed record DeepInfraChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<DeepInfraChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record DeepInfraChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);
}
