using Merlin.Backend.Configuration;
using Merlin.Backend.Services.StreamingResponses;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpeakableTextSanitizerTests
{
    [Fact]
    public void Sanitize_CleansMarkdownBulletsAndHeadings()
    {
        var sanitizer = CreateSanitizer();

        var text = sanitizer.Sanitize(
            "## Why this matters\n- Add streaming\n- Add segmenting",
            new SpeakableTextSanitizationContext());

        Assert.Equal("Why this matters. Add streaming. Add segmenting.", text);
    }

    [Fact]
    public void Sanitize_CleansNumberedLists()
    {
        var sanitizer = CreateSanitizer();

        var text = sanitizer.Sanitize(
            "1. Add streaming.\n2. Add cancellation.",
            new SpeakableTextSanitizationContext());

        Assert.Equal("Add streaming. Add cancellation.", text);
    }

    [Fact]
    public void Sanitize_ReplacesCodeFences()
    {
        var sanitizer = CreateSanitizer();

        var text = sanitizer.Sanitize(
            "Use this:\n```csharp\nvar x = 1;\n```",
            new SpeakableTextSanitizationContext());

        Assert.Equal("Use this: There is a code example here.", text);
    }

    [Fact]
    public void Sanitize_DefendsAgainstRawJson()
    {
        var sanitizer = CreateSanitizer();

        var text = sanitizer.Sanitize("{\"intent\":\"open_url\",\"confidence\":0.92}", new SpeakableTextSanitizationContext());

        Assert.Equal("I found structured data.", text);
    }

    private static SpeakableTextSanitizer CreateSanitizer()
    {
        return new SpeakableTextSanitizer(new StreamingResponseOptions());
    }
}
