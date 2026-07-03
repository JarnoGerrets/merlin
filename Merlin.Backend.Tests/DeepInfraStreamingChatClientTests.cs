using Merlin.Backend.Services.StreamingResponses;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class DeepInfraStreamingChatClientTests
{
    [Fact]
    public void TryParseDelta_ReturnsContentDelta()
    {
        var delta = DeepInfraStreamingChatClient.TryParseDelta(
            "{\"choices\":[{\"delta\":{\"content\":\"hello\"}}]}");

        Assert.Equal("hello", delta);
    }

    [Fact]
    public void TryParseDelta_IgnoresEmptyOrMalformedPayloads()
    {
        Assert.Null(DeepInfraStreamingChatClient.TryParseDelta("{\"choices\":[{\"delta\":{}}]}"));
        Assert.Null(DeepInfraStreamingChatClient.TryParseDelta("not json"));
    }
}
