using Merlin.Backend.Configuration;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class StopConfirmationPhraseSelectorTests
{
    [Fact]
    public void DefaultPhrases_ContainRequiredStopConfirmations()
    {
        Assert.Contains("Got it, I'll stop.", StopConfirmationPhraseSelector.DefaultPhrases);
        Assert.Contains("Okay, stopping.", StopConfirmationPhraseSelector.DefaultPhrases);
        Assert.Contains("Understood, I'll be quiet.", StopConfirmationPhraseSelector.DefaultPhrases);
        Assert.Contains("I'll shut up, sir.", StopConfirmationPhraseSelector.DefaultPhrases);
    }

    [Fact]
    public void SelectPhrase_RotatesWithoutImmediateRepeatWhenMultiplePhrasesExist()
    {
        var selector = new StopConfirmationPhraseSelector(Options.Create(new InterruptionHandlingOptions
        {
            StopConfirmationPhrases =
            [
                "First.",
                "Second."
            ]
        }));

        var first = selector.SelectPhrase();
        var second = selector.SelectPhrase();

        Assert.NotEqual(first, second);
    }
}
