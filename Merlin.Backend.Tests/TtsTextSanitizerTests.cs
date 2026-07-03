using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class TtsTextSanitizerTests
{
    private readonly TtsTextSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_BoldHeadingAndNumberedList_ProducesSpeakableProse()
    {
        var result = _sanitizer.Sanitize("""
            **Better (Advantages of EVs):**
            1. Lower running costs.
            2. Quiet driving.
            """);

        Assert.Contains("Better: advantages of electric vehicles.", result.Text);
        Assert.Contains("First, lower running costs.", result.Text);
        Assert.Contains("Second, quiet driving.", result.Text);
        Assert.DoesNotContain("**", result.Text);
        Assert.DoesNotContain("1.", result.Text);
        Assert.DoesNotContain("2.", result.Text);
    }

    [Fact]
    public void Sanitize_ProblematicInlineFragment_RemovesMarkdownAndMalformedMarker()
    {
        var result = _sanitizer.Sanitize("compared to petrol cars for a typical family in the Netherlands. Let's break it down practically: **Better (Advantages of EVs):** 1..");

        Assert.Contains("compared to petrol cars for a typical family in the Netherlands. Let's break it down practically. Better: advantages of electric vehicles.", result.Text);
        Assert.DoesNotContain("**", result.Text);
        Assert.DoesNotContain("1..", result.Text);
    }

    [Fact]
    public void Sanitize_InlineMalformedListWithText_ConvertsOrdinal()
    {
        var result = _sanitizer.Sanitize("**Better (Advantages of EVs):** 1.. Lower running costs. 2. Quiet driving. - Less maintenance.");

        Assert.Contains("Better: advantages of electric vehicles.", result.Text);
        Assert.Contains("First, lower running costs.", result.Text);
        Assert.Contains("Second, quiet driving.", result.Text);
        Assert.Contains("Less maintenance.", result.Text);
        Assert.DoesNotContain("1..", result.Text);
        Assert.DoesNotContain("- ", result.Text);
    }

    [Fact]
    public void Sanitize_Bullets_RemovesBulletMarkers()
    {
        var result = _sanitizer.Sanitize("""
            - Safer for school runs
            - More boot space
            - Lower monthly cost
            """);

        Assert.Equal("Safer for school runs. More boot space. Lower monthly cost.", result.Text);
    }

    [Fact]
    public void Sanitize_MarkdownLinks_RemovesRawUrl()
    {
        var result = _sanitizer.Sanitize("See [ANWB advice](https://example.com/very/long/url) before buying.");

        Assert.Equal("See ANWB advice before buying.", result.Text);
        Assert.DoesNotContain("https://", result.Text);
        Assert.DoesNotContain("example.com", result.Text);
    }

    [Fact]
    public void Sanitize_CodeBlock_RemovesCodeBlock()
    {
        var result = _sanitizer.Sanitize("""
            Here is the answer:

            ```json
            {"test": true}
            ```

            That means it worked.
            """);

        Assert.Equal("Here is the answer. That means it worked.", result.Text);
        Assert.DoesNotContain("```", result.Text);
        Assert.DoesNotContain("\"test\"", result.Text);
    }

    [Theory]
    [InlineData("Meaning is built—not found.", "Meaning is built, not found.")]
    [InlineData("Meaning is built — not found.", "Meaning is built, not found.")]
    [InlineData("A fixed truth —revised daily.", "A fixed truth, revised daily.")]
    public void Sanitize_EmDash_ReplacesWithCommaPause(string raw, string expected)
    {
        var result = _sanitizer.Sanitize(raw);

        Assert.Equal(expected, result.Text);
        Assert.DoesNotContain("—", result.Text);
    }

    [Fact]
    public void Sanitize_NormalHyphen_IsPreserved()
    {
        var result = _sanitizer.Sanitize("Use a well-known phrase.");

        Assert.Equal("Use a well-known phrase.", result.Text);
        Assert.Contains("-", result.Text);
    }

    [Fact]
    public void Sanitize_BeforeChunkPlanning_PreventsMarkdownFragmentsInChunks()
    {
        var raw = "Compared to petrol cars, **Better (Advantages of EVs):** 1.. Lower running costs. 2. Quiet driving. - Less maintenance.";
        var sanitized = _sanitizer.Sanitize(raw).Text;
        var chunks = ChatterboxChunkPlanner.Plan(sanitized, new TtsOptions
        {
            ChatterboxEnableInteractiveChunking = true,
            ChatterboxFirstChunkTargetChars = 70,
            ChatterboxFirstChunkMaxChars = 120,
            ChatterboxNextChunkTargetChars = 95,
            ChatterboxNextChunkMaxChars = 140
        });

        Assert.All(chunks, chunk =>
        {
            Assert.DoesNotContain("**", chunk);
            Assert.DoesNotContain("1..", chunk);
            Assert.False(StartsWithMarkdownOrListSyntax(chunk), chunk);
        });
    }

    private static bool StartsWithMarkdownOrListSyntax(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith("**", StringComparison.Ordinal)
            || trimmed.StartsWith('*')
            || trimmed.StartsWith('-')
            || trimmed.StartsWith('#')
            || trimmed.StartsWith("1.", StringComparison.Ordinal)
            || trimmed.StartsWith("1..", StringComparison.Ordinal);
    }
}
