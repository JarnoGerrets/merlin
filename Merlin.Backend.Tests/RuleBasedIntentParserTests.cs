using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class RuleBasedIntentParserTests
{
    [Theory]
    [InlineData("open notepad", "open notepad")]
    [InlineData("could you open notepad", "open notepad")]
    [InlineData("please open calculator", "open calculator")]
    [InlineData("launch vscode", "open vscode")]
    [InlineData("launch visual studio code", "open vscode")]
    [InlineData("start calc", "open calculator")]
    public async Task ParseAsync_WhenMessageOpensApplication_ReturnsOpenApplicationIntent(
        string message,
        string expectedCommand)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("open_application", result.Intent);
        Assert.Equal(expectedCommand, result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
        Assert.Equal(message, result.OriginalMessage);
    }

    [Theory]
    [InlineData("open google.com", "open google.com")]
    [InlineData("go to google.com", "open google.com")]
    [InlineData("visit github.com", "open github.com")]
    [InlineData("browse microsoft.com", "open microsoft.com")]
    [InlineData("take me to google.com", "open google.com")]
    public async Task ParseAsync_WhenMessageOpensUrl_ReturnsOpenUrlIntent(
        string message,
        string expectedCommand)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("open_url", result.Intent);
        Assert.Equal(expectedCommand, result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
        Assert.Equal(message, result.OriginalMessage);
    }

    [Theory]
    [InlineData("list tools")]
    [InlineData("what tools do you have")]
    [InlineData("show available tools")]
    [InlineData("what can you do")]
    public async Task ParseAsync_WhenMessageRequestsToolDiscovery_ReturnsToolDiscoveryIntent(string message)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("tool_discovery", result.Intent);
        Assert.Equal("list tools", result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
        Assert.Equal(message, result.OriginalMessage);
    }

    [Theory]
    [InlineData("show status")]
    [InlineData("system status")]
    [InlineData("diagnostics")]
    [InlineData("health check")]
    [InlineData("merlin status")]
    [InlineData("show diagnostics")]
    public async Task ParseAsync_WhenMessageRequestsDiagnostics_ReturnsDiagnosticsIntent(string message)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("diagnostics", result.Intent);
        Assert.Equal("show status", result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
        Assert.Equal(message, result.OriginalMessage);
    }

    [Theory]
    [InlineData("what time is it", "system resource current_time", "system_time")]
    [InlineData("what is today's date", "system resource current_date", "system_date")]
    [InlineData("what timezone am I in", "system resource timezone", "system_timezone")]
    public async Task ParseAsync_WhenMessageRequestsSystemResource_ReturnsSystemResourceIntent(
        string message,
        string expectedCommand,
        string expectedCapabilityId)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("system_resource_query", result.Intent);
        Assert.Equal(expectedCommand, result.NormalizedCommand);
        Assert.Equal(expectedCapabilityId, result.CapabilityId);
        Assert.True(result.Confidence >= 0.9);
    }

    [Theory]
    [InlineData("tell me a joke")]
    [InlineData("who are you")]
    [InlineData("how do you work")]
    public async Task ParseAsync_WhenMessageIsConversation_ReturnsGeneralConversationIntent(string message)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("general_conversation", result.Intent);
        Assert.Equal($"chat {message}", result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
        Assert.Equal(message, result.OriginalMessage);
    }

    [Theory]
    [InlineData("can you check my folders")]
    [InlineData("can you check my folders?")]
    [InlineData("can you check my files")]
    [InlineData("search my hard drive")]
    [InlineData("clean my downloads folder")]
    public async Task ParseAsync_WhenMessageNeedsMissingCapability_ReturnsMissingCapability(string message)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("missing_capability", result.Intent);
        Assert.Equal(message.TrimEnd('?'), result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
    }

    [Theory]
    [InlineData("delete my files")]
    [InlineData("delete all my files")]
    [InlineData("wipe my hard drive")]
    [InlineData("disable windows defender")]
    [InlineData("install chrome")]
    [InlineData("update windows")]
    public async Task ParseAsync_WhenMessageIsUnsupportedAction_ReturnsUnsupportedAction(string message)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal("unsupported_action", result.Intent);
        Assert.Equal(message.TrimEnd('?'), result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
        Assert.NotEqual("diagnostics", result.Intent);
    }

    [Theory]
    [InlineData("what is the weather")]
    public async Task ParseAsync_WhenMessageIsUnknown_ReturnsUnknownResult(string message)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Null(result.Intent);
        Assert.Equal(message, result.NormalizedCommand);
        Assert.Equal(0, result.Confidence);
        Assert.Equal(message, result.OriginalMessage);
    }

    [Fact]
    public async Task ParseAsync_WhenMessageOpensUnconfiguredApplication_ReturnsOpenApplicationIntent()
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync("open paint");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open paint", result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.7);
    }
}
