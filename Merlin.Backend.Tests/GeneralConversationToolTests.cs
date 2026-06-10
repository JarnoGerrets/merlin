using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class GeneralConversationToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsModelResponse()
    {
        var tool = new GeneralConversationTool(new FakeLocalAIChatService("Here is a small joke."));

        var result = await tool.ExecuteAsync("chat tell me a joke");

        Assert.True(result.Success);
        Assert.Equal("Here is a small joke.", result.Message);
        Assert.Equal("General Conversation", result.ToolName);
        Assert.Equal("general_conversation", result.Intent);
    }

    [Fact]
    public async Task RouteAsync_WhenTellMeAJoke_RoutesToGeneralConversation()
    {
        var router = CreateRouter("Here is a joke.");

        var response = await router.RouteAsync("tell me a joke");

        Assert.True(response.Success);
        Assert.Equal("Here is a joke.", response.Message);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Equal("general_conversation", response.Intent);
    }

    [Fact]
    public async Task RouteAsync_WhenWhoAreYou_RoutesToGeneralConversation()
    {
        var router = CreateRouter("I am Merlin, a local desktop assistant.");

        var response = await router.RouteAsync("who are you");

        Assert.True(response.Success);
        Assert.Equal("I am Merlin, a local desktop assistant.", response.Message);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Equal("general_conversation", response.Intent);
    }

    [Theory]
    [InlineData("delete my files")]
    [InlineData("install chrome")]
    [InlineData("download software")]
    [InlineData("open powershell")]
    public async Task RouteAsync_WhenDangerousPrompt_IsUnsupportedActionOnly(string command)
    {
        var router = CreateRouter("I cannot perform that action through conversation.");

        var response = await router.RouteAsync(command);

        Assert.False(response.Success);
        Assert.Equal("General Conversation", response.ToolName);
        Assert.Equal("unsupported_action", response.Intent);
        Assert.Equal("UNSUPPORTED_ACTION", response.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLocalAIUnavailable_ReturnsUnavailableError()
    {
        var tool = new GeneralConversationTool(new FakeLocalAIChatService(
            LocalAIChatService.UnavailableErrorCode,
            success: false,
            errorCode: LocalAIChatService.UnavailableErrorCode));

        var result = await tool.ExecuteAsync("chat tell me a joke");

        Assert.False(result.Success);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, result.Message);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, result.ErrorCode);
    }

    private static CommandRouter CreateRouter(string chatResponse)
    {
        return new CommandRouter(
            new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
            new ToolRegistry([new GeneralConversationTool(new FakeLocalAIChatService(chatResponse))]),
            NullLogger<CommandRouter>.Instance,
            new RuntimeStateService(),
            new NoOpResponsePolisher());
    }

    private sealed class FakeLocalAIChatService : ILocalAIChatService
    {
        private readonly string? _errorCode;
        private readonly string _message;
        private readonly bool _success;

        public FakeLocalAIChatService(
            string message,
            bool success = true,
            string? errorCode = null)
        {
            _message = message;
            _success = success;
            _errorCode = errorCode;
        }

        public Task<LocalAIChatResult> GenerateResponseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LocalAIChatResult
            {
                Success = _success,
                Message = _message,
                ErrorCode = _errorCode
            });
        }
    }
}
