using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpeechCommandNormalizerTests
{
    [Theory]
    [InlineData("open terminal dot nl", "open terminal.nl")]
    [InlineData("go to github dot io", "go to github.io")]
    [InlineData("open bbc dot co dot uk", "open bbc.co.uk")]
    [InlineData("open terminal point nl", "open terminal.nl")]
    [InlineData("open terminal period nl", "open terminal.nl")]
    [InlineData("open terminal dot and l", "open terminal.nl")]
    [InlineData("open example dot c o dot u k", "open example.co.uk")]
    [InlineData("open terminal.nl", "open terminal.nl")]
    [InlineData("open terminal . nl", "open terminal.nl")]
    [InlineData("Can you change terminal browser mapping to terminal.nl?", "can you change terminal browser mapping to terminal.nl?")]
    public void Normalize_WhenTranscriptContainsSpokenDomain_ReturnsDottedDomain(
        string transcript,
        string expected)
    {
        var normalizer = new SpeechCommandNormalizer();

        var normalized = normalizer.Normalize(transcript);

        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("launch visual studio code", "launch vscode")]
    [InlineData("open v s code", "open vscode")]
    [InlineData("open power shell", "open powershell")]
    public void Normalize_WhenTranscriptContainsCommonCommandMishear_ReturnsCommandTerm(
        string transcript,
        string expected)
    {
        var normalizer = new SpeechCommandNormalizer();

        var normalized = normalizer.Normalize(transcript);

        Assert.Equal(expected, normalized);
    }
}
