using Merlin.Backend.Services.Context.ActiveSurface;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class BrowserMediaCommandNormalizerTests
{
    private readonly BrowserMediaCommandNormalizer _normalizer = new();

    [Theory]
    [InlineData("pause video", ActiveSurfaceCapabilities.BrowserMediaPause)]
    [InlineData("pause the video", ActiveSurfaceCapabilities.BrowserMediaPause)]
    [InlineData("play video", ActiveSurfaceCapabilities.BrowserMediaPlay)]
    [InlineData("fullscreen", ActiveSurfaceCapabilities.BrowserMediaFullscreen)]
    [InlineData("go fullscreen", ActiveSurfaceCapabilities.BrowserMediaFullscreen)]
    [InlineData("skip ad", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("skip add", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("skip advertentie", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("overslaan", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("overslan", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("over slaan", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("advertentie overslaan", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("klik overslaan", ActiveSurfaceCapabilities.BrowserMediaSkipAd)]
    [InlineData("click pause", ActiveSurfaceCapabilities.BrowserMediaPause)]
    [InlineData("click fullscreen", ActiveSurfaceCapabilities.BrowserMediaFullscreen)]
    public void TryMatchExplicit_MatchesBrowserMediaPhrases(string text, string expectedCapability)
    {
        var match = _normalizer.TryMatchExplicit(text);

        Assert.NotNull(match);
        Assert.Equal(expectedCapability, match.Capability);
    }

    [Fact]
    public void TryMatchAmbiguous_PauseRoutesOnlyWhenBrowserWorkspaceActive()
    {
        var browser = KnownSurfaces.BrowserWorkspace(DateTimeOffset.UtcNow);
        var dashboard = KnownSurfaces.Dashboard(DateTimeOffset.UtcNow);

        Assert.Equal(
            ActiveSurfaceCapabilities.BrowserMediaPause,
            _normalizer.TryMatchAmbiguous("pause", browser)?.Capability);
        Assert.Null(_normalizer.TryMatchAmbiguous("pause", dashboard));
    }
}
