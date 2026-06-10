using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ResponsePolisherTests
{
    [Fact]
    public async Task PolishMessageAsync_WhenUnsupportedAction_OnlyChangesMessage()
    {
        var response = CreateResponse("Unsupported action.", "UNSUPPORTED_ACTION", "unsupported_action");
        var polisher = new ResponsePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.NotEqual(response.Message, message);
        Assert.False(response.Success);
        Assert.Equal("UNSUPPORTED_ACTION", response.ErrorCode);
        Assert.Equal("unsupported_action", response.Intent);
        Assert.Equal("General Conversation", response.ToolName);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenUnknownCommand_UsesUnknownTemplate()
    {
        var response = CreateResponse("Unknown command.", "UNKNOWN_COMMAND", null);
        var polisher = new ResponsePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Equal("I couldn't determine a supported action from your request.", message);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenBlockedUrlScheme_UsesSafetyTemplate()
    {
        var response = CreateResponse("Blocked URL scheme.", "BLOCKED_URL_SCHEME", "open_url");
        var polisher = new ResponsePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Equal("I can only open HTTP and HTTPS links. Other URL schemes are blocked for safety.", message);
        Assert.Equal("BLOCKED_URL_SCHEME", response.ErrorCode);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenCurrentInformationQuestionAndLocalAIUnavailable_ExplainsMissingTool()
    {
        var response = CreateResponse(
            LocalAIChatService.UnavailableErrorCode,
            LocalAIChatService.UnavailableErrorCode,
            "general_conversation",
            "what time is it?");
        var polisher = new ResponsePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Contains("time tool", message);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, response.ErrorCode);
        Assert.Equal("general_conversation", response.Intent);
    }

    private static AssistantResponse CreateResponse(
        string message,
        string? errorCode,
        string? intent,
        string originalMessage = "test")
    {
        return new AssistantResponse
        {
            Success = false,
            Message = message,
            CorrelationId = "test-id",
            ErrorCode = errorCode,
            ToolName = "General Conversation",
            Intent = intent,
            IntentConfidence = 0.9,
            OriginalMessage = originalMessage,
            ParserUsed = "test-parser"
        };
    }
}
