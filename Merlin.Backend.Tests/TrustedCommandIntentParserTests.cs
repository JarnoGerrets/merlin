using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class TrustedCommandIntentParserTests
{
    [Fact]
    public async Task ParseAsync_WhenTrustedCommandExists_ReturnsTrustedIntent()
    {
        var store = new FakeTrustedCommandStore();
        store.SaveMapping(CreateMapping("open paint"));
        var parser = new TrustedCommandIntentParser(store);

        var result = await parser.ParseAsync("open paint");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open paint", result.NormalizedCommand);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal("open paint", result.OriginalMessage);
        Assert.Equal(nameof(TrustedCommandIntentParser), result.ParserUsed);
    }

    [Fact]
    public async Task ParseAsync_WhenNaturalPhraseWasTrusted_ReturnsNormalizedCommand()
    {
        var store = new FakeTrustedCommandStore();
        store.SaveMapping(CreateMapping("could you open paint for me"));
        var parser = new TrustedCommandIntentParser(store);

        var result = await parser.ParseAsync("could you open paint for me?");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open paint", result.NormalizedCommand);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public async Task ParseAsync_WhenNoMappingExists_ReturnsUnknownShape()
    {
        var parser = new TrustedCommandIntentParser(new FakeTrustedCommandStore());

        var result = await parser.ParseAsync("open vs");

        Assert.Null(result.Intent);
        Assert.Equal(0, result.Confidence);
        Assert.Equal(string.Empty, result.NormalizedCommand);
    }

    private static TrustedCommandMapping CreateMapping(string originalCommand)
    {
        return new TrustedCommandMapping
        {
            OriginalCommand = originalCommand,
            Intent = "open_application",
            NormalizedCommand = "open paint",
            ToolName = "Open Application",
            Target = "mspaint.exe",
            DisplayName = "Paint",
            UseCount = 1
        };
    }
}
