using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services;

public sealed class OllamaLocalAIClient : ILocalAIClient
{
    private readonly HttpClient _httpClient;
    private readonly LocalAIOptions _options;

    public OllamaLocalAIClient(HttpClient httpClient, IOptions<LocalAIOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string?> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaGenerateRequest(
            Model: _options.Model,
            Prompt: prompt,
            Stream: false,
            KeepAlive: _options.KeepAlive);

        using var response = await _httpClient.PostAsJsonAsync(
            _options.Endpoint,
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
            cancellationToken: cancellationToken);

        return result?.Response;
    }

    private sealed record OllamaGenerateRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("keep_alive")] string KeepAlive);

    private sealed record OllamaGenerateResponse(
        [property: JsonPropertyName("response")] string? Response);
}
