using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ResponsePolisherTests
{
    [Fact]
    public async Task PolishMessageAsync_WhenUnsupportedAction_OnlyChangesMessage()
    {
        var response = CreateResponse(
            "Unsupported action.",
            "UNSUPPORTED_ACTION",
            "unsupported_action",
            capabilityId: "destructive_file_action");
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Contains("delete files", message);
        Assert.Contains("protects your data", message);
        Assert.DoesNotContain("UNSUPPORTED_ACTION", message);
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
            "can you pull up the newsfeed for me?",
            "news");
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Contains("News capability", message);
        Assert.Contains("NewsTool or WebSearch capability", message);
        Assert.DoesNotContain("MISSING_CAPABILITY", message);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
    }

    [Fact]
    public async Task PolishMessageAsync_WhenFileAccessMissing_ExplainsLimitationWithoutInternalCode()
    {
        var response = CreateResponse(
            "Missing capability.",
            "MISSING_CAPABILITY",
            "missing_capability",
            "check my folders",
            "file_access");
        var polisher = CreatePolisher();

        var message = await polisher.PolishMessageAsync(response);

        Assert.Contains("inspect folders", message);
        Assert.Contains("file access capability", message);
        Assert.DoesNotContain("MISSING_CAPABILITY", message);
        Assert.Equal("file_access", response.CapabilityId);
        Assert.Equal("limitation", response.ResponseType);
    }

    [Fact]
    public async Task PolishMessageAsync_PreservesStructuredMetadataOnOriginalResponse()
    {
        var response = CreateResponse(
            "Missing capability.",
            "MISSING_CAPABILITY",
            "missing_capability",
            "can you pull up the newsfeed?",
            "news");
        var polisher = CreatePolisher();

        _ = await polisher.PolishMessageAsync(response);

        Assert.False(response.Success);
        Assert.Equal("test-id", response.CorrelationId);
        Assert.Equal("MISSING_CAPABILITY", response.ErrorCode);
        Assert.Equal("missing_capability", response.Intent);
        Assert.Equal("news", response.CapabilityId);
        Assert.Equal("News", response.CapabilityName);
        Assert.Equal("limitation", response.ResponseType);
        Assert.Equal("test-parser", response.ParserUsed);
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
        string originalMessage = "test",
        string? capabilityId = null)
    {
        return new AssistantResponse
        {
            Success = false,
            Message = message,
            CorrelationId = "test-id",
            ErrorCode = errorCode,
            ToolName = "General Conversation",
            Intent = intent,
            CapabilityId = capabilityId,
            CapabilityName = capabilityId switch
            {
                "news" => "News",
                "file_access" => "File Access",
                "destructive_file_action" => "Destructive File Action",
                _ => null
            },
            IntentConfidence = 0.9,
            OriginalMessage = originalMessage,
            ParserUsed = "test-parser",
            ResponseType = intent switch
            {
                "missing_capability" => "limitation",
                "unsupported_action" => "safety",
                "unknown_input" => "error",
                _ => null
            }
        };
    }

    private static ResponsePolisher CreatePolisher()
    {
        return new ResponsePolisher(TestCapabilityOptions.Create());
    }
}
