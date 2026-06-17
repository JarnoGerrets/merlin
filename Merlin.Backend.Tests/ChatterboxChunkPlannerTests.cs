using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ChatterboxChunkPlannerTests
{
    [Fact]
    public void Plan_ForLongAnswer_KeepsFirstChunkConversational()
    {
        var text = "Of course, I can explain that. The useful way to think about it is that Merlin should start speaking after a short phrase, not after a paragraph. Once that first phrase is playing, the next phrases can be generated in the background. That keeps the conversation feeling alive while still allowing a richer answer. The later chunks can be longer because playback is already covering the wait. This should reduce the silence before speech without changing the model, the frontend, or the playback contract.";

        var chunks = ChatterboxChunkPlanner.Plan(text, CreateOptions());

        Assert.True(chunks.Count > 1);
        Assert.InRange(chunks[0].Length, 40, 120);
        Assert.All(chunks, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk)));
        Assert.All(chunks.Skip(1), chunk => Assert.True(chunk.Length <= 140));
    }

    [Fact]
    public void Plan_ForVeryShortAnswer_KeepsSingleChunk()
    {
        var chunks = ChatterboxChunkPlanner.Plan("Done, I found it.", CreateOptions());

        var chunk = Assert.Single(chunks);
        Assert.Equal("Done, I found it.", chunk);
    }

    [Fact]
    public void Plan_AvoidsSplittingDecimalAndUrlTokens()
    {
        var text = "Version 2.7.1 is installed from https://download.pytorch.org/whl/cu128, and the CUDA test passed. That means the next thing to measure is first audio latency with a small phrase-first chunk.";

        var chunks = ChatterboxChunkPlanner.Plan(text, CreateOptions());

        Assert.DoesNotContain(chunks, chunk => chunk.EndsWith("2.", StringComparison.Ordinal));
        Assert.True(chunks[0].Length <= 120);
        Assert.Equal(text, string.Join(' ', chunks));
    }

    [Fact]
    public void Plan_WhenInteractiveChunkingDisabled_UsesLegacyMaxSize()
    {
        var options = CreateOptions();
        options.ChatterboxEnableInteractiveChunking = false;
        options.ChatterboxMaxTextCharsPerChunk = 350;
        var text = string.Join(' ', Enumerable.Repeat("Merlin should keep this in a larger legacy-style chunk.", 8));

        var chunks = ChatterboxChunkPlanner.Plan(text, options);

        Assert.True(chunks[0].Length > 120);
        Assert.True(chunks[0].Length <= 350);
    }

    private static TtsOptions CreateOptions()
    {
        return new TtsOptions
        {
            ChatterboxEnableInteractiveChunking = true,
            ChatterboxFirstChunkTargetChars = 70,
            ChatterboxFirstChunkMaxChars = 120,
            ChatterboxNextChunkTargetChars = 95,
            ChatterboxNextChunkMaxChars = 140
        };
    }
}
