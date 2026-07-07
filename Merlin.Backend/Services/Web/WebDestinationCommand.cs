namespace Merlin.Backend.Services.Web;

using Merlin.Backend.Services.BrowserWorkspace;

public sealed record WebDestinationCommand(
    WebDestinationAction Action,
    string? Url = null,
    string? SearchQuery = null,
    string? SiteHint = null,
    string? ClickQuery = null,
    string? TargetKind = null,
    string? CommonAction = null,
    int? Ordinal = null,
    BrowserScrollDirection? ScrollDirection = null,
    BrowserScrollAmount? ScrollAmount = null,
    string Reason = "");
