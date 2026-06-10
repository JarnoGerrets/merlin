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
        var polisher = CreatePolisher();

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
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Equal("I couldn't determine a supported action from your request.", message);
        Assert.Equal("UNKNOWN_COMMAND", response.ErrorCode);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenBlockedUrlScheme_UsesSafetyTemplate()
    {
        var response = CreateResponse("Blocked URL scheme.", "BLOCKED_URL_SCHEME", "open_url");
        var polisher = CreatePolisher();

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
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Contains("time tool", message);
        Assert.Equal(LocalAIChatService.UnavailableErrorCode, response.ErrorCode);
        Assert.Equal("general_conversation", response.Intent);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenMissingCapability_UsesConfiguredMessage()
    {
        var response = CreateResponse(
            "Missing capability.",
            "MISSING_CAPABILITY",
            "missing_capability",
            "can you pull up the newsfeed for me?");
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Contains("NewsTool", message);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenUnknownInput_UsesUnknownInputTemplate()
    {
        var response = CreateResponse(
            "Unknown input.",
            "UNKNOWN_INPUT",
            "unknown_input",
            "asdfghjkl qwerty");
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Equal("I couldn't understand that request.", message);
        Assert.Equal("UNKNOWN_INPUT", response.ErrorCode);
        Assert.Equal("unknown_input", response.Intent);
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

    private static ResponsePolisher CreatePolisher()
    {
        return new ResponsePolisher(TestCapabilityOptions.Create());
    }
}
