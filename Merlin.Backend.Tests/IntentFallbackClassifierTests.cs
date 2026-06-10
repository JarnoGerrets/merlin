using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class IntentFallbackClassifierTests
{
    [Theory]
    [InlineData("what time is it")]
    [InlineData("what is object oriented programming")]
    [InlineData("what is the latest Godot version")]
    [InlineData("who won today's match")]
    public void Classify_WhenMessageIsQuestion_ReturnsGeneralConversation(string message)
    {
        var classifier = new IntentFallbackClassifier();

        var result = classifier.Classify(message);

        Assert.Equal("general_conversation", result.Intent);
        Assert.StartsWith("chat ", result.NormalizedCommand);
        Assert.True(result.Confidence > 0);
    }

    [Theory]
    [InlineData("can you check my folders")]
    [InlineData("search my hard drive")]
    [InlineData("delete my files")]
    [InlineData("install chrome")]
    [InlineData("update windows")]
    [InlineData("clean my downloads folder")]
    public void Classify_WhenMessageLooksLikeUnsupportedAction_ReturnsUnsupportedAction(string message)
    {
        var classifier = new IntentFallbackClassifier();

        var result = classifier.Classify(message);

        Assert.Equal("unsupported_action", result.Intent);
        Assert.Equal(message, result.NormalizedCommand);
        Assert.True(result.Confidence >= 0.9);
    }
}
