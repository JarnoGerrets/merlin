using System.Net;
using System.Text;
using System.Text.Json;
using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class OllamaLocalAIClientTests
{
    [Fact]
    public async Task GenerateAsync_SendsConfiguredModelEndpointAndKeepAlive()
    {
        var handler = new CapturingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new LocalAIOptions
        {
            Endpoint = "http://localhost:11434/api/generate",
            Model = "llama3.1:8b",
            KeepAlive = "10m"
        });
        var client = new OllamaLocalAIClient(httpClient, options);

        var response = await client.GenerateAsync("hello");

        Assert.Equal("""{"intent":"unknown","normalizedCommand":"","confidence":0.0}""", response);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("http://localhost:11434/api/generate", handler.Request?.RequestUri?.ToString());

        using var document = JsonDocument.Parse(handler.RequestBody);
        var root = document.RootElement;
        Assert.Equal("llama3.1:8b", root.GetProperty("model").GetString());
        Assert.Equal("hello", root.GetProperty("prompt").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal("10m", root.GetProperty("keep_alive").GetString());
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"response":"{\"intent\":\"unknown\",\"normalizedCommand\":\"\",\"confidence\":0.0}"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
