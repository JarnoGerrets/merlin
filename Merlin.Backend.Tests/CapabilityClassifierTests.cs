using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class CapabilityClassifierTests
{
    [Theory]
    [InlineData("open paint", "open_application")]
    [InlineData("show status", "diagnostics")]
    public async Task Classify_WhenKnownToolIntentMatches_ReturnsExistingIntent(string message, string expectedIntent)
    {
        var parser = new RuleBasedIntentParser(TestApplicationLaunchOptions.Create());

        var result = await parser.ParseAsync(message);

        Assert.Equal(expectedIntent, result.Intent);
    }

    [Theory]
    [InlineData("show news")]
    [InlineData("can you pull up the newsfeed for me")]
    [InlineData("search web")]
    [InlineData("search the internet for Godot tutorials")]
    [InlineData("check folders")]
    [InlineData("check my folders")]
    [InlineData("show emails")]
    [InlineData("search my hard drive")]
    public void Classify_WhenCapabilityIsMissing_ReturnsMissingCapability(string message)
    {
        var classifier = CreateClassifier();

        var result = classifier.Classify(message);

        Assert.Equal("missing_capability", result.Intent);
        Assert.True(result.Confidence >= 0.8);
    }

    [Theory]
    [InlineData("delete files")]
    [InlineData("delete all my files")]
    [InlineData("wipe drive")]
    [InlineData("wipe my hard drive")]
    [InlineData("disable windows defender")]
    public void Classify_WhenActionIsUnsupported_ReturnsUnsupportedAction(string message)
    {
        var classifier = CreateClassifier();

        var result = classifier.Classify(message);

        Assert.Equal("unsupported_action", result.Intent);
        Assert.True(result.Confidence >= 0.9);
    }

    [Fact]
    public void Classify_WhenInputIsGibberish_ReturnsUnknownInput()
    {
        var classifier = CreateClassifier();

        var result = classifier.Classify("asdfghjkl qwerty");

        Assert.Equal("unknown_input", result.Intent);
        Assert.Equal("asdfghjkl qwerty", result.NormalizedCommand);
    }

    private static CapabilityClassifier CreateClassifier()
    {
        return new CapabilityClassifier(new ToolRegistry([new FakeTool()]));
    }

    private sealed class FakeTool : ITool
    {
        public string Name => "General Conversation";

        public string Description => "Handles conversation.";

        public IReadOnlyCollection<string> Examples { get; } = ["hello"];

        public bool CanHandle(string command)
        {
            return false;
        }

        public Task<Merlin.Backend.Models.ToolResult> ExecuteAsync(
            string command,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
