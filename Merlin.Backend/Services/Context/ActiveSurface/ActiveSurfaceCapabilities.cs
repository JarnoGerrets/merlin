namespace Merlin.Backend.Services.Context.ActiveSurface;

public static class ActiveSurfaceCapabilities
{
    public const string AssistantChat = "assistant.chat";
    public const string AssistantPlaybackPause = "assistant.playback.pause";
    public const string AssistantPlaybackResume = "assistant.playback.resume";
    public const string AssistantPlaybackStop = "assistant.playback.stop";
    public const string AssistantPlaybackCancel = "assistant.playback.cancel";

    public const string BrowserNavigate = "browser.navigate";
    public const string BrowserBack = "browser.back";
    public const string BrowserForward = "browser.forward";
    public const string BrowserRefresh = "browser.refresh";

    public const string BrowserPageClick = "browser.page.click";
    public const string BrowserPageSearch = "browser.page.search";
    public const string BrowserPageScroll = "browser.page.scroll";

    public const string BrowserMediaPlay = "browser.media.play";
    public const string BrowserMediaPause = "browser.media.pause";
    public const string BrowserMediaStop = "browser.media.stop";
    public const string BrowserMediaFullscreen = "browser.media.fullscreen";
    public const string BrowserMediaSkipAd = "browser.media.skip_ad";
}
