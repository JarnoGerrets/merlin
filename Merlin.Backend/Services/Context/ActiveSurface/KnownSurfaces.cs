namespace Merlin.Backend.Services.Context.ActiveSurface;

public static class KnownSurfaces
{
    public static ActiveSurfaceSnapshot Dashboard(
        DateTimeOffset now,
        ActiveSurfaceSource source = ActiveSurfaceSource.StartupDefault,
        string reason = "startup_default") => new(
        Kind: ActiveSurfaceKind.Dashboard,
        SurfaceId: "dashboard.main",
        DisplayName: "Merlin Dashboard",
        Confidence: 1.0,
        Source: source,
        UpdatedUtc: now,
        Capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ActiveSurfaceCapabilities.AssistantChat,
            ActiveSurfaceCapabilities.AssistantPlaybackPause,
            ActiveSurfaceCapabilities.AssistantPlaybackResume,
            ActiveSurfaceCapabilities.AssistantPlaybackStop,
            ActiveSurfaceCapabilities.AssistantPlaybackCancel
        },
        Metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["reason"] = reason
        });

    public static ActiveSurfaceSnapshot BrowserWorkspace(
        DateTimeOffset now,
        IReadOnlyDictionary<string, string>? metadata = null,
        ActiveSurfaceSource source = ActiveSurfaceSource.BrowserWorkspace) => new(
        Kind: ActiveSurfaceKind.BrowserWorkspace,
        SurfaceId: "browser.workspace.main",
        DisplayName: "Browser Workspace",
        Confidence: 1.0,
        Source: source,
        UpdatedUtc: now,
        Capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ActiveSurfaceCapabilities.BrowserNavigate,
            ActiveSurfaceCapabilities.BrowserBack,
            ActiveSurfaceCapabilities.BrowserForward,
            ActiveSurfaceCapabilities.BrowserRefresh,
            ActiveSurfaceCapabilities.BrowserPageClick,
            ActiveSurfaceCapabilities.BrowserPageSearch,
            ActiveSurfaceCapabilities.BrowserPageScroll,
            ActiveSurfaceCapabilities.BrowserMediaPlay,
            ActiveSurfaceCapabilities.BrowserMediaPause,
            ActiveSurfaceCapabilities.BrowserMediaStop,
            ActiveSurfaceCapabilities.BrowserMediaFullscreen,
            ActiveSurfaceCapabilities.BrowserMediaSkipAd
        },
        Metadata: metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase));

    public static ActiveSurfaceSnapshot Unknown(DateTimeOffset now) => new(
        Kind: ActiveSurfaceKind.Unknown,
        SurfaceId: "unknown",
        DisplayName: "Unknown",
        Confidence: 0.0,
        Source: ActiveSurfaceSource.Unknown,
        UpdatedUtc: now,
        Capabilities: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        Metadata: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
}
