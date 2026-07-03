using Merlin.Backend.Services.StreamingResponses;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class StreamedTextDetokenizerTests
{
    public static TheoryData<string, string> RepairCases => new()
    {
        { "it 's", "it's" },
        { "don 't", "don't" },
        { "can 't", "can't" },
        { "you 're", "you're" },
        { "we 're", "we're" },
        { "I 'm", "I'm" },
        { "they 've", "they've" },
        { "wouldn 't", "wouldn't" },
        { "word .", "word." },
        { "word ,", "word," },
        { "word ?", "word?" },
        { "word !", "word!" },
        { "word :", "word:" },
        { "word ;", "word;" },
        { "( word", "(word" },
        { "word )", "word)" },
        { "cult - ivating", "cult-ivating" },
        { "t ending", "tending" },
        { "cult ivating", "cultivating" },
        { "st irs", "stirs" },
        { "ask ed", "asked" },
        { "learn ing", "learning" },
        { "build ing", "building" },
        { "creat ing", "creating" },
        { "mean ing", "meaning" },
        { "stream ing", "streaming" }
    };

    [Theory]
    [MemberData(nameof(RepairCases))]
    public void Detokenize_RepairsKnownStreamingArtifacts(string raw, string expected)
    {
        var detokenizer = new StreamedTextDetokenizer();

        var result = detokenizer.Detokenize(raw);

        Assert.Equal(expected, result.Text);
        Assert.True(result.RepairCount > 0);
    }

    [Theory]
    [InlineData("the T ending is uppercase")]
    [InlineData("ask Ed about it")]
    [InlineData("The answer is tending toward this.")]
    public void Detokenize_DoesNotOvercorrectValidPhrases(string text)
    {
        var detokenizer = new StreamedTextDetokenizer();

        var result = detokenizer.Detokenize(text);

        Assert.Equal(text, result.Text);
        Assert.Equal(0, result.RepairCount);
    }
}
