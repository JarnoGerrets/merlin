using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AssistantSpeechResponseFormatterTests
{
    private readonly AssistantSpeechResponseFormatter _formatter = new(NullLogger<AssistantSpeechResponseFormatter>.Instance);

    [Fact]
    public void Format_WhenAppOpenSucceeds_UsesGenericReplayableSpeech()
    {
        var response = new AssistantResponse
        {
            Success = true,
            Message = "Opening PowerPoint...",
            ToolName = "Open Application",
            Intent = "open_application"
        };

        var presentation = _formatter.Format(response);

        Assert.NotNull(presentation);
        Assert.Equal("Opening the app for you, sir.", presentation.SpokenText);
        Assert.Equal("Opening PowerPoint...", presentation.DisplayText);
        Assert.Equal("tool.app.open.success.generic", presentation.CacheKey);
        Assert.True(presentation.PreferPhraseCache);
        Assert.True(presentation.IsReplayable);
        Assert.DoesNotContain("PowerPoint", presentation.SpokenText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_WhenUrlOpenSucceeds_UsesGenericReplayableSpeech()
    {
        var response = new AssistantResponse
        {
            Success = true,
            Message = "Opening https://facebook.com...",
            ToolName = "Open URL",
            Intent = "open_url"
        };

        var presentation = _formatter.Format(response);

        Assert.NotNull(presentation);
        Assert.Equal("Opening the website for you, sir.", presentation.SpokenText);
        Assert.Equal("tool.url.open.success.generic", presentation.CacheKey);
        Assert.DoesNotContain("google", presentation.CacheKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("facebook", presentation.SpokenText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_WhenConfirmationIsRequired_UsesShortConfirmationSpeech()
    {
        var response = new AssistantResponse
        {
            Success = false,
            Message = "I found PowerPoint, but I have not handled this specific application before. Please confirm before I open it.",
            ErrorCode = "CONFIRMATION_REQUIRED",
            ToolName = "Open Application",
            Intent = "open_application",
            ResponseType = "confirmation",
            Confirmation = new PendingConfirmation
            {
                DisplayName = "PowerPoint",
                Target = "powerpnt.exe"
            }
        };

        var presentation = _formatter.Format(response);

        Assert.NotNull(presentation);
        Assert.Equal("I need your confirmation first.", presentation.SpokenText);
        Assert.Contains("PowerPoint", presentation.DisplayText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("tool.confirmation.required.generic", presentation.CacheKey);
        Assert.DoesNotContain("PowerPoint", presentation.SpokenText, StringComparison.OrdinalIgnoreCase);
        Assert.True(presentation.SpokenText.Length <= 100);
    }

    [Fact]
    public void Format_WhenToolFails_UsesGenericFailureSpeech()
    {
        var response = new AssistantResponse
        {
            Success = false,
            Message = "I could not open PowerPoint because the file was unavailable.",
            ErrorCode = "OPEN_FAILED",
            ToolName = "Open Application",
            Intent = "open_application",
            ResponseType = "error"
        };

        var presentation = _formatter.Format(response);

        Assert.NotNull(presentation);
        Assert.Equal("I couldn't open that from here.", presentation.SpokenText);
        Assert.Equal("tool.open.failure.generic", presentation.CacheKey);
        Assert.DoesNotContain("PowerPoint", presentation.SpokenText, StringComparison.OrdinalIgnoreCase);
        Assert.True(presentation.SpokenText.Length <= 120);
    }

    [Fact]
    public void Format_WhenDifferentAppsOpen_UsesStableCacheKeyAndSpeech()
    {
        var first = _formatter.Format(new AssistantResponse
        {
            Success = true,
            Message = "Opening PowerPoint...",
            ToolName = "Open Application",
            Intent = "open_application"
        });
        var second = _formatter.Format(new AssistantResponse
        {
            Success = true,
            Message = "Opening Steam...",
            ToolName = "Open Application",
            Intent = "open_application"
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.CacheKey, second.CacheKey);
        Assert.Equal(first.SpokenText, second.SpokenText);
    }

    [Fact]
    public void Format_WhenAmbiguousApplication_UsesGenericSpeechAndKeepsSpecificDisplay()
    {
        var response = new AssistantResponse
        {
            Success = false,
            Message = "I found multiple apps matching Steam. Please choose which app you want to open.",
            ErrorCode = "AMBIGUOUS_APPLICATION",
            ToolName = "Open Application",
            Intent = "open_application",
            ResponseType = "confirmation",
            ApplicationCandidates =
            [
                new ApplicationCandidate { DisplayName = "Steam", ExecutablePath = "steam.exe", Source = "StartMenu", Confidence = 0.85 },
                new ApplicationCandidate { DisplayName = "Steam Link", ExecutablePath = "steamlink.exe", Source = "StartMenu", Confidence = 0.85 }
            ]
        };

        var presentation = _formatter.Format(response);

        Assert.NotNull(presentation);
        Assert.Equal("I need one detail first.", presentation.SpokenText);
        Assert.Contains("Steam", presentation.DisplayText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("tool.ambiguous.generic", presentation.CacheKey);
        Assert.DoesNotContain("Steam", presentation.SpokenText, StringComparison.OrdinalIgnoreCase);
        Assert.True(presentation.SpokenText.Length <= 120);
    }

    [Fact]
    public void BuildPresentation_WhenSpokenTextExceedsLimit_FallsBackToGenericTemplate()
    {
        var presentation = AssistantSpeechResponseFormatter.BuildPresentation(
            new string('x', 121),
            "PowerPoint could not be opened. Error: some detailed reason.",
            "tool.open.failure.generic",
            120,
            ToolSpeechTemplates.GenericFailure);

        Assert.Equal(ToolSpeechTemplates.GenericFailure, presentation.SpokenText);
        Assert.Equal("PowerPoint could not be opened. Error: some detailed reason.", presentation.DisplayText);
        Assert.Equal("tool.open.failure.generic", presentation.CacheKey);
        Assert.True(presentation.IsReplayable);
    }
}
