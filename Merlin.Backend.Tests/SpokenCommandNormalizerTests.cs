using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpokenCommandNormalizerTests
{
    [Theory]
    [InlineData("Merlin, show chat", "show chat")]
    [InlineData("Hey Merlin, show chat", "show chat")]
    [InlineData("Hi Merlin, close chat", "close chat")]
    [InlineData("Hello Merlin, open chat", "open chat")]
    [InlineData("Okay Merlin, start gesture mode", "start gesture mode")]
    [InlineData("Ok Merlin, stop gesture mode", "stop gesture mode")]
    [InlineData("Yo Merlin, let me control the UI", "let me control the ui")]
    public void Normalize_StripsLeadingInvocation(string text, string expected)
    {
        var result = SpokenCommandNormalizer.Normalize(text);

        Assert.Equal(text, result.OriginalText);
        Assert.Equal(expected, result.CommandText);
        Assert.True(result.HadInvocation);
        Assert.False(result.WasWakeOnly);
    }

    [Theory]
    [InlineData("please show chat", "show chat")]
    [InlineData("can you show chat please", "show chat")]
    [InlineData("could you please open chat", "open chat")]
    [InlineData("would you close chat please", "close chat")]
    [InlineData("Merlin, can you show the chat please", "show the chat")]
    public void Normalize_StripsPoliteScaffolding(string text, string expected)
    {
        var result = SpokenCommandNormalizer.Normalize(text);

        Assert.Equal(expected, result.CommandText);
    }

    [Theory]
    [InlineData("Merlin")]
    [InlineData("Hey Merlin")]
    [InlineData("Okay Merlin, please")]
    public void Normalize_DetectsWakeOnlyPhrases(string text)
    {
        var result = SpokenCommandNormalizer.Normalize(text);

        Assert.True(result.HadInvocation);
        Assert.True(result.WasWakeOnly);
        Assert.Equal(string.Empty, result.CommandText);
    }

    [Theory]
    [InlineData("what is Merlin")]
    [InlineData("tell me about Merlin")]
    [InlineData("is Merlin a wizard")]
    [InlineData("the Merlin project needs a chat log")]
    [InlineData("Merlin is the name of my assistant")]
    public void Normalize_DoesNotStripNonInvocationMerlin(string text)
    {
        var result = SpokenCommandNormalizer.Normalize(text);

        Assert.Equal(text.ToLowerInvariant(), result.CommandText);
        Assert.False(result.HadInvocation);
        Assert.False(result.WasWakeOnly);
    }

    [Theory]
    [InlineData("Hey Merlin, show chat", ChatLogCommandAction.Show)]
    [InlineData("Okay Merlin, open chat", ChatLogCommandAction.Show)]
    [InlineData("Hi Merlin, close chat", ChatLogCommandAction.Hide)]
    [InlineData("Merlin, please show chat", ChatLogCommandAction.Show)]
    [InlineData("Merlin, can you show the chat please", ChatLogCommandAction.Show)]
    [InlineData("Can you show chat please", ChatLogCommandAction.Show)]
    public void ChatLogCommandMatcher_AcceptsNaturalVoicePrefixes(string text, ChatLogCommandAction expectedAction)
    {
        var matched = ChatLogCommandMatcher.TryMatch(text, out var action);

        Assert.True(matched);
        Assert.Equal(expectedAction, action);
    }

    [Theory]
    [InlineData("Hey Merlin, let me control the UI", UiControlModeCommandAction.Start)]
    [InlineData("Okay Merlin, start gesture mode", UiControlModeCommandAction.Start)]
    [InlineData("Hi Merlin, start UI control", UiControlModeCommandAction.Start)]
    [InlineData("Merlin, let me control UI", UiControlModeCommandAction.Start)]
    [InlineData("Hey Merlin, give me control of the UI", UiControlModeCommandAction.Start)]
    [InlineData("give me control of the ui", UiControlModeCommandAction.Start)]
    [InlineData("give me ui control", UiControlModeCommandAction.Start)]
    [InlineData("let me take control of the ui", UiControlModeCommandAction.Start)]
    [InlineData("i want control of the ui", UiControlModeCommandAction.Start)]
    [InlineData("i want ui control", UiControlModeCommandAction.Start)]
    [InlineData("switch to ui control", UiControlModeCommandAction.Start)]
    [InlineData("activate ui control", UiControlModeCommandAction.Start)]
    [InlineData("turn on ui control", UiControlModeCommandAction.Start)]
    [InlineData("enter ui control mode", UiControlModeCommandAction.Start)]
    [InlineData("use gesture mode", UiControlModeCommandAction.Start)]
    [InlineData("activate gesture mode", UiControlModeCommandAction.Start)]
    [InlineData("Hey Merlin, I am done with the UI", UiControlModeCommandAction.Stop)]
    [InlineData("Okay Merlin, stop gesture mode", UiControlModeCommandAction.Stop)]
    [InlineData("Merlin, close UI control", UiControlModeCommandAction.Stop)]
    [InlineData("stop controlling the ui", UiControlModeCommandAction.Stop)]
    [InlineData("turn off ui control", UiControlModeCommandAction.Stop)]
    [InlineData("exit ui control mode", UiControlModeCommandAction.Stop)]
    [InlineData("leave ui control mode", UiControlModeCommandAction.Stop)]
    [InlineData("disable gesture mode", UiControlModeCommandAction.Stop)]
    [InlineData("leave gesture mode", UiControlModeCommandAction.Stop)]
    [InlineData("i am done controlling the ui", UiControlModeCommandAction.Stop)]
    public void UiControlModeCommandMatcher_AcceptsNaturalVoicePrefixes(string text, UiControlModeCommandAction expectedAction)
    {
        var matched = UiControlModeCommandMatcher.TryMatch(text, out var action);

        Assert.True(matched);
        Assert.Equal(expectedAction, action);
    }

    [Theory]
    [InlineData("what is Merlin")]
    [InlineData("tell me about Merlin")]
    [InlineData("is Merlin a wizard")]
    [InlineData("the Merlin project needs a chat log")]
    [InlineData("Merlin is the name of my assistant")]
    public void DeterministicMatchers_DoNotMatchNonInvocationMerlinSentences(string text)
    {
        Assert.False(ChatLogCommandMatcher.TryMatch(text, out _));
        Assert.False(UiControlModeCommandMatcher.TryMatch(text, out _));
    }

    [Theory]
    [InlineData("Merlin, how does UI control work?")]
    [InlineData("Merlin, explain UI control")]
    [InlineData("Merlin, should UI control use gestures?")]
    [InlineData("What is UI control?")]
    [InlineData("Could it use gesture mode?")]
    [InlineData("Would it be better to use UI control?")]
    [InlineData("can it enable ui control")]
    [InlineData("tell me about ui control")]
    [InlineData("describe gesture mode")]
    [InlineData("do you think ui control should use gestures")]
    public void UiControlModeCommandMatcher_RejectsQuestionsAndDiscussion(string text)
    {
        Assert.False(UiControlModeCommandMatcher.TryMatch(text, out _));
    }

    [Theory]
    [InlineData("turn on ui control then stop ui control")]
    [InlineData("start ui control and then turn off ui control")]
    [InlineData("enable gesture mode but stop gesture mode")]
    public void UiControlModeCommandMatcher_StopIntentWinsOverStartIntent(string text)
    {
        var matched = UiControlModeCommandMatcher.TryMatch(text, out var action);

        Assert.True(matched);
        Assert.Equal(UiControlModeCommandAction.Stop, action);
    }

    [Theory]
    [InlineData("could you enable ui control")]
    [InlineData("would you please activate gesture mode")]
    [InlineData("can you turn on ui control please")]
    public void UiControlModeCommandMatcher_AcceptsPoliteCommands(string text)
    {
        var matched = UiControlModeCommandMatcher.TryMatch(text, out var action);

        Assert.True(matched);
        Assert.Equal(UiControlModeCommandAction.Start, action);
    }
}
