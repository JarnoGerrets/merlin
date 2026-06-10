using Merlin.Backend.Services;
using Merlin.Backend.WebSocket;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class WebSocketHandlerTests
{
    [Fact]
    public async Task ProcessMessageAsync_WhenJsonIsInvalid_ReturnsInvalidJsonErrorCode()
    {
        var runtimeStateService = new RuntimeStateService();
        var handler = new WebSocketHandler(
            new CommandRouter(
                new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
                new ToolRegistry([]),
                NullLogger<CommandRouter>.Instance,
                runtimeStateService,
                new NoOpResponsePolisher()),
            NullLogger<WebSocketHandler>.Instance,
            runtimeStateService);

        var response = await handler.ProcessMessageAsync("{bad json", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Invalid JSON.", response.Message);
        Assert.Equal("INVALID_JSON", response.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Null(response.ToolName);
        Assert.Null(response.Intent);
    }
}
