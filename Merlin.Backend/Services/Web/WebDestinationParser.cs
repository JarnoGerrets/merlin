using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.Context.ActiveSurface;
using Merlin.Backend.Tools;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Web;

public sealed class WebDestinationParser : IWebDestinationParser
{
    private static readonly string[] NavigatePrefixes =
    [
        "open ",
        "please open ",
        "go to ",
        "please go to ",
        "navigate to ",
        "please navigate to ",
        "browse ",
        "visit ",
        "pull up ",
        "start "
    ];

    private static readonly string[] LeadingCommandWrappers =
    [
        "please, ",
        "please ",
        "merlin, ",
        "merlin ",
        "hey merlin, ",
        "hey merlin ",
        "okay merlin, ",
        "okay merlin ",
        "ok merlin, ",
        "ok merlin ",
        "can you please ",
        "could you please ",
        "would you please ",
        "can you ",
        "could you ",
        "would you "
    ];

    private static readonly string[] TrailingControlWrappers =
    [
        " please",
        " for me",
        " thanks",
        " thank you"
    ];

    private static readonly HashSet<string> GenericOpenCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "open browser",
        "open the browser",
        "open your browser",
        "open web browser",
        "open the web browser",
        "open browser workspace",
        "open the browser workspace",
        "open your browser workspace",
        "open merlin browser",
        "open the browser in merlin",
        "open browser in merlin",
        "use your browser",
        "show your browser",
        "show browser",
        "use browser",
        "use the browser",
        "start browser",
        "open website",
        "open the website",
        "open internet",
        "open the internet"
    };

    private static readonly HashSet<string> CloseCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "close browser",
        "close the browser",
        "close your browser",
        "close browser workspace",
        "close the browser workspace",
        "close merlin browser",
        "hide browser",
        "hide the browser",
        "hide your browser",
        "exit browser",
        "exit browser workspace"
    };

    private static readonly HashSet<string> RefreshCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "refresh",
        "refresh browser",
        "refresh the browser",
        "reload",
        "reload browser",
        "reload the browser",
        "reload page",
        "reload the page",
        "refresh page"
    };

    private static readonly HashSet<string> BackCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "back",
        "go back",
        "browser back",
        "back browser",
        "go back in browser",
        "go back one page",
        "previous page"
    };

    private static readonly HashSet<string> ForwardCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "forward",
        "go forward",
        "browser forward",
        "forward browser",
        "go forward in browser",
        "next page"
    };

    private static readonly HashSet<string> ScrollToTopCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "scroll to top",
        "scroll top",
        "go to top",
        "page top"
    };

    private static readonly HashSet<string> ScrollToBottomCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "scroll to bottom",
        "scroll bottom",
        "go to bottom",
        "page bottom"
    };

    private static readonly HashSet<string> ZoomInCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "zoom in",
        "browser zoom in",
        "zoom browser in"
    };

    private static readonly HashSet<string> ZoomOutCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "zoom out",
        "browser zoom out",
        "zoom browser out"
    };

    private static readonly HashSet<string> ResetZoomCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "reset zoom",
        "reset browser zoom",
        "normal zoom",
        "browser normal zoom"
    };

    private static readonly HashSet<string> InspectPageCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "inspect page",
        "inspect the page",
        "show page snapshot",
        "show the page snapshot",
        "what can you see on this page",
        "what do you see on this page"
    };

    private static readonly HashSet<string> PageInfoCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "what page am i on",
        "what page is this",
        "which page is this",
        "what website am i on",
        "where am i",
        "where am i now",
        "tell me what page this is"
    };

    private static readonly HashSet<string> SummarizePageCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "summarize page",
        "summarize the page",
        "summarize this page",
        "read page",
        "read the page",
        "read this page",
        "what does this page say",
        "what is this page about"
    };

    private static readonly HashSet<string> EnableBrowserMotionOverlayCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "start browser hand control",
        "start browser pointer",
        "show browser pointer",
        "enable browser motion",
        "enable browser pointer",
        "turn on browser pointer",
        "turn on browser motion",
        "browser hand control",
        "browser pointer"
    };

    private static readonly HashSet<string> DisableBrowserMotionOverlayCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "stop browser hand control",
        "hide browser pointer",
        "stop browser pointer",
        "disable browser motion",
        "disable browser pointer",
        "turn off browser pointer",
        "turn off browser motion"
    };

    private static readonly Dictionary<string, string> CommonPageActionCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["close popup"] = "close_popup",
        ["close the popup"] = "close_popup",
        ["close this popup"] = "close_popup",
        ["dismiss popup"] = "close_popup",
        ["dismiss this"] = "close_popup",
        ["click close"] = "close_popup",
        ["accept cookies"] = "accept_cookies",
        ["accept cookie"] = "accept_cookies",
        ["reject cookies"] = "reject_cookies",
        ["decline cookies"] = "reject_cookies",
        ["no thanks"] = "no_thanks",
        ["skip ad"] = "skip_ad",
        ["skip the ad"] = "skip_ad",
        ["skip youtube ad"] = "skip_ad",
        ["skip advertisement"] = "skip_ad",
        ["play video"] = "play_video",
        ["play the video"] = "play_video",
        ["click play"] = "play_video",
        ["click the play button"] = "play_video",
        ["click play button"] = "play_video",
        ["press play"] = "play_video",
        ["press the play button"] = "play_video",
        ["resume video"] = "play_video",
        ["resume the video"] = "play_video",
        ["pause video"] = "pause_video",
        ["pause the video"] = "pause_video",
        ["click pause"] = "pause_video",
        ["click the pause button"] = "pause_video",
        ["click pause button"] = "pause_video",
        ["press pause"] = "pause_video",
        ["press the pause button"] = "pause_video",
        ["tap pause"] = "pause_video",
        ["tap the pause button"] = "pause_video",
        ["hit pause"] = "pause_video",
        ["hit the pause button"] = "pause_video",
        ["mute"] = "mute_video",
        ["mute video"] = "mute_video",
        ["mute the video"] = "mute_video",
        ["click mute"] = "mute_video",
        ["click the mute button"] = "mute_video",
        ["press mute"] = "mute_video",
        ["unmute"] = "unmute_video",
        ["unmute video"] = "unmute_video",
        ["unmute the video"] = "unmute_video",
        ["click unmute"] = "unmute_video",
        ["click the unmute button"] = "unmute_video",
        ["press unmute"] = "unmute_video",
        ["fullscreen"] = "fullscreen",
        ["full screen"] = "fullscreen",
        ["click fullscreen"] = "fullscreen",
        ["click full screen"] = "fullscreen",
        ["click the fullscreen button"] = "fullscreen",
        ["click the full screen button"] = "fullscreen",
        ["exit fullscreen"] = "exit_fullscreen"
    };

    private static readonly string[] PageSearchPrefixes =
    [
        "search this page for ",
        "search on this page for ",
        "search here for ",
        "search current page for ",
        "use this page to search for "
    ];

    private static readonly string[] PageFindPrefixes =
    [
        "find on this page ",
        "find on page ",
        "find this page for ",
        "find the page for ",
        "find in this page ",
        "find in page ",
        "look for on this page ",
        "look for in this page "
    ];

    private static readonly (string Prefix, string Suffix)[] SearchFieldInsertionPatterns =
    [
        ("type ", " into the search field"),
        ("type ", " into search field"),
        ("type ", " into the search box"),
        ("type ", " into search box"),
        ("type ", " in the search field"),
        ("type ", " in the search box"),
        ("enter ", " in the search field"),
        ("enter ", " in the search box"),
        ("put ", " in the search field"),
        ("put ", " in the search box")
    ];

    private static readonly (string Prefix, string TargetKind)[] ClickTargetPrefixes =
    [
        ("click the link called ", "link"),
        ("click link called ", "link"),
        ("open the link called ", "link"),
        ("open link called ", "link"),
        ("click the button called ", "button"),
        ("click button called ", "button"),
        ("click the button that says ", "button"),
        ("click button that says ", "button"),
        ("click the result called ", "result"),
        ("click result called ", "result"),
        ("open the result called ", "result"),
        ("open result called ", "result"),
        ("open the result about ", "result"),
        ("open result about ", "result"),
        ("click the result about ", "result"),
        ("click result about ", "result"),
        ("click the result titled ", "result"),
        ("click result titled ", "result"),
        ("open the result titled ", "result"),
        ("open result titled ", "result")
    ];

    private static readonly (string Phrase, int Ordinal)[] OrdinalWords =
    [
        ("first", 1),
        ("second", 2),
        ("third", 3),
        ("fourth", 4),
        ("fifth", 5)
    ];

    private static readonly string[] SearchPrefixes =
    [
        "search web for ",
        "search the web for ",
        "search for ",
        "search ",
        "google ",
        "look up ",
        "find "
    ];

    private static readonly string[] ScrollDownPrefixes =
    [
        "scroll down",
        "page down",
        "scroll further down",
        "scroll a bit down"
    ];

    private static readonly string[] ScrollUpPrefixes =
    [
        "scroll up",
        "page up",
        "scroll further up",
        "scroll a bit up"
    ];

    private static readonly HashSet<string> NavigationControlCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "back",
        "forward",
        "refresh",
        "reload"
    };

    private readonly WebDestinationOptions _options;
    private readonly ITrustedUrlStore _trustedUrlStore;
    private readonly IBrowserMediaCommandNormalizer _browserMediaCommandNormalizer;

    public WebDestinationParser(
        IOptions<WebDestinationOptions> options,
        ITrustedUrlStore? trustedUrlStore = null,
        IBrowserMediaCommandNormalizer? browserMediaCommandNormalizer = null)
    {
        _options = options.Value;
        _trustedUrlStore = trustedUrlStore ?? NullTrustedUrlStore.Instance;
        _browserMediaCommandNormalizer = browserMediaCommandNormalizer ?? new BrowserMediaCommandNormalizer();
    }

    public WebDestinationCommand? TryParse(string text)
    {
        return TryParse(text, activeSurface: null);
    }

    public WebDestinationCommand? TryParse(string text, ActiveSurfaceSnapshot? activeSurface)
    {
        var normalized = NormalizeForCommandMatching(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var commandText = StripTrailingControlWrappers(StripLeadingCommandWrappers(normalized));
        var extractionText = StripLeadingCommandWrappers(normalized);

        if (GenericOpenCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.OpenWorkspace, Reason: "generic_browser_open");
        }

        if (CloseCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.CloseWorkspace, Reason: "generic_browser_close");
        }

        if (RefreshCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.Refresh, Reason: "browser_refresh");
        }

        if (BackCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.Back, Reason: "browser_back");
        }

        if (ForwardCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.Forward, Reason: "browser_forward");
        }

        if (ScrollToTopCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.ScrollToTop, Reason: "browser_scroll_top");
        }

        if (ScrollToBottomCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.ScrollToBottom, Reason: "browser_scroll_bottom");
        }

        if (TryParseScroll(commandText, out var direction, out var amount))
        {
            return new WebDestinationCommand(
                WebDestinationAction.Scroll,
                ScrollDirection: direction,
                ScrollAmount: amount,
                Reason: "browser_scroll");
        }

        if (ZoomInCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.ZoomIn, Reason: "browser_zoom_in");
        }

        if (ZoomOutCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.ZoomOut, Reason: "browser_zoom_out");
        }

        if (ResetZoomCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.ResetZoom, Reason: "browser_zoom_reset");
        }

        if (InspectPageCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.InspectPage, Reason: "browser_page_snapshot");
        }

        if (PageInfoCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.PageInfo, Reason: "browser_page_info");
        }

        if (SummarizePageCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.SummarizePage, Reason: "browser_page_summary");
        }

        if (EnableBrowserMotionOverlayCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.EnableBrowserMotionOverlay, Reason: "browser_motion_overlay_start");
        }

        if (DisableBrowserMotionOverlayCommands.Contains(commandText))
        {
            return new WebDestinationCommand(WebDestinationAction.DisableBrowserMotionOverlay, Reason: "browser_motion_overlay_stop");
        }

        var mediaMatch = _browserMediaCommandNormalizer.TryMatchExplicit(commandText)
            ?? (activeSurface is null
                ? null
                : _browserMediaCommandNormalizer.TryMatchAmbiguous(commandText, activeSurface));
        if (mediaMatch is not null)
        {
            return new WebDestinationCommand(
                WebDestinationAction.CommonPageAction,
                CommonAction: BrowserMediaCommandNormalizer.CommonActionForCapability(mediaMatch.Capability),
                Reason: mediaMatch.Reason);
        }

        if (CommonPageActionCommands.TryGetValue(commandText, out var commonAction))
        {
            return new WebDestinationCommand(
                WebDestinationAction.CommonPageAction,
                CommonAction: commonAction,
                Reason: "browser_common_action");
        }

        if (TryExtractPageSearch(extractionText, out var pageSearchQuery, out var siteHint, out var siteUrl))
        {
            return new WebDestinationCommand(
                WebDestinationAction.SearchCurrentPage,
                Url: siteUrl,
                SearchQuery: pageSearchQuery,
                SiteHint: siteHint,
                Reason: siteHint is null ? "browser_page_search" : "browser_site_search");
        }

        if (TryExtractPageFind(extractionText, out var pageFindQuery))
        {
            return new WebDestinationCommand(
                WebDestinationAction.FindOnPage,
                SearchQuery: pageFindQuery,
                Reason: "browser_page_find");
        }

        if (TryExtractClickCommand(extractionText, out var clickQuery, out var targetKind, out var ordinal))
        {
            return new WebDestinationCommand(
                WebDestinationAction.ClickVisibleElement,
                ClickQuery: clickQuery,
                TargetKind: targetKind,
                Ordinal: ordinal,
                Reason: ordinal is null ? "browser_page_click" : "browser_page_click_ordinal");
        }

        if (TryExtractSearchQuery(extractionText, out var query))
        {
            return new WebDestinationCommand(
                WebDestinationAction.Search,
                SearchQuery: query,
                Reason: "browser_search");
        }

        if (!TryExtractNavigationTarget(extractionText, out var target))
        {
            return null;
        }

        if (IsExternalBrowserProduct(target))
        {
            return null;
        }

        if (TryResolveKnownDestination(target, out var knownUrl))
        {
            return new WebDestinationCommand(WebDestinationAction.Navigate, knownUrl, Reason: "known_web_destination");
        }

        var trustedUrl = _trustedUrlStore.FindByAlias(target);
        if (trustedUrl is not null)
        {
            return new WebDestinationCommand(WebDestinationAction.Navigate, trustedUrl.Url, Reason: "trusted_url_alias");
        }

        if (TryNormalizeExplicitUrl(target, out var explicitUrl))
        {
            return new WebDestinationCommand(WebDestinationAction.Navigate, explicitUrl, Reason: "explicit_url");
        }

        return null;
    }

    private static bool TryExtractClickCommand(
        string normalized,
        out string? query,
        out string? targetKind,
        out int? ordinal)
    {
        query = null;
        targetKind = null;
        ordinal = null;

        foreach (var (word, value) in OrdinalWords)
        {
            foreach (var phrase in new[]
            {
                $"click {word} result",
                $"click the {word} result",
                $"open {word} result",
                $"open the {word} result",
                $"select {word} result",
                $"select the {word} result"
            })
            {
                if (!string.Equals(normalized, phrase, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                targetKind = "result";
                ordinal = value;
                return true;
            }
        }

        foreach (var (prefix, kind) in ClickTargetPrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = CleanPayload(normalized[prefix.Length..]);
            targetKind = kind;
            return !string.IsNullOrWhiteSpace(query);
        }

        foreach (var prefix in new[]
        {
            "open the results",
            "open results",
            "open the result",
            "open result",
            "click the results",
            "click results",
            "click the result",
            "click result",
            "select the result",
            "select result"
        })
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = CleanPayload(normalized[prefix.Length..].TrimStart(',', ' '));
            foreach (var filler in new[] { "called ", "about ", "titled ", "named " })
            {
                if (target.StartsWith(filler, StringComparison.OrdinalIgnoreCase))
                {
                    target = CleanPayload(target[filler.Length..]);
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            query = target;
            targetKind = "result";
            return true;
        }

        foreach (var prefix in new[]
        {
            "click the ",
            "click ",
            "press the ",
            "press ",
            "select the ",
            "select ",
            "choose the ",
            "choose ",
            "tap the ",
            "tap ",
            "hit the ",
            "hit "
        })
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = CleanPayload(normalized[prefix.Length..]);
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (target.EndsWith(" button", StringComparison.OrdinalIgnoreCase))
            {
                query = CleanPayload(target[..^" button".Length]);
                targetKind = "button";
                return !string.IsNullOrWhiteSpace(query);
            }

            if (target.EndsWith(" link", StringComparison.OrdinalIgnoreCase))
            {
                query = CleanPayload(target[..^" link".Length]);
                targetKind = "link";
                return !string.IsNullOrWhiteSpace(query);
            }

            if (target.EndsWith(" result", StringComparison.OrdinalIgnoreCase))
            {
                query = CleanPayload(target[..^" result".Length]);
                targetKind = "result";
                return !string.IsNullOrWhiteSpace(query);
            }

            query = target;
            return true;
        }

        return false;
    }

    private static bool TryExtractPageFind(string normalized, out string query)
    {
        query = string.Empty;
        foreach (var prefix in PageFindPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                query = CleanPayload(normalized[prefix.Length..]);
                return !string.IsNullOrWhiteSpace(query);
            }
        }

        const string onThisPageSuffix = " on this page";
        if (normalized.StartsWith("find ", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(onThisPageSuffix, StringComparison.OrdinalIgnoreCase))
        {
            query = CleanPayload(normalized["find ".Length..^onThisPageSuffix.Length]);
            return !string.IsNullOrWhiteSpace(query);
        }

        const string inThisPageSuffix = " in this page";
        if (normalized.StartsWith("find ", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(inThisPageSuffix, StringComparison.OrdinalIgnoreCase))
        {
            query = CleanPayload(normalized["find ".Length..^inThisPageSuffix.Length]);
            return !string.IsNullOrWhiteSpace(query);
        }

        return false;
    }

    private bool TryExtractPageSearch(
        string normalized,
        out string query,
        out string? siteHint,
        out string? siteUrl)
    {
        query = string.Empty;
        siteHint = null;
        siteUrl = null;

        foreach (var prefix in PageSearchPrefixes)
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                query = CleanPayload(normalized[prefix.Length..]);
                return !string.IsNullOrWhiteSpace(query);
            }
        }

        const string onThisPageSuffix = " on this page";
        if (normalized.StartsWith("search for ", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(onThisPageSuffix, StringComparison.OrdinalIgnoreCase))
        {
            query = CleanPayload(normalized["search for ".Length..^onThisPageSuffix.Length]);
            return !string.IsNullOrWhiteSpace(query);
        }

        foreach (var (prefix, suffix) in SearchFieldInsertionPatterns)
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = CleanPayload(normalized[prefix.Length..^suffix.Length]);
            return !string.IsNullOrWhiteSpace(query);
        }

        if (!normalized.StartsWith("search ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = normalized["search ".Length..];
        var separatorIndex = remainder.IndexOf(" for ", StringComparison.OrdinalIgnoreCase);
        if (separatorIndex <= 0)
        {
            return false;
        }

        siteHint = CleanTarget(remainder[..separatorIndex]);
        query = CleanPayload(remainder[(separatorIndex + " for ".Length)..]);
        if (string.IsNullOrWhiteSpace(siteHint)
            || string.IsNullOrWhiteSpace(query)
            || siteHint is "web" or "the web" or "internet" or "the internet")
        {
            siteHint = null;
            query = string.Empty;
            return false;
        }

        if (TryResolveKnownDestination(siteHint, out var knownUrl))
        {
            siteUrl = knownUrl;
            return true;
        }

        var trustedUrl = _trustedUrlStore.FindByAlias(siteHint);
        if (trustedUrl is not null)
        {
            siteUrl = trustedUrl.Url;
            return true;
        }

        siteHint = null;
        query = string.Empty;
        return false;
    }

    private static bool TryParseScroll(
        string normalized,
        out BrowserScrollDirection direction,
        out BrowserScrollAmount amount)
    {
        direction = BrowserScrollDirection.Down;
        amount = BrowserScrollAmount.Normal;
        if (TryMatchScrollPrefix(normalized, ScrollDownPrefixes, out amount))
        {
            direction = BrowserScrollDirection.Down;
            return true;
        }

        if (TryMatchScrollPrefix(normalized, ScrollUpPrefixes, out amount))
        {
            direction = BrowserScrollDirection.Up;
            return true;
        }

        return false;
    }

    private static bool TryMatchScrollPrefix(
        string normalized,
        IReadOnlyCollection<string> prefixes,
        out BrowserScrollAmount amount)
    {
        amount = BrowserScrollAmount.Normal;
        foreach (var prefix in prefixes)
        {
            if (!normalized.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            amount = normalized.Contains("a bit", StringComparison.OrdinalIgnoreCase)
                ? BrowserScrollAmount.Small
                : normalized.Contains("further", StringComparison.OrdinalIgnoreCase)
                    ? BrowserScrollAmount.Large
                    : BrowserScrollAmount.Normal;
            return true;
        }

        return false;
    }

    private static bool TryExtractSearchQuery(string normalized, out string query)
    {
        query = string.Empty;
        foreach (var prefix in SearchPrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            query = CleanPayload(normalized[prefix.Length..]);
            return !string.IsNullOrWhiteSpace(query)
                && !NavigationControlCommands.Contains(query)
                && !IsExternalBrowserProduct(query);
        }

        return false;
    }

    internal static string NormalizeForCommandMatching(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim().ToLowerInvariant();
        normalized = string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        normalized = normalized.Replace(" .", ".", StringComparison.Ordinal);
        normalized = normalized.Replace(" ?", "?", StringComparison.Ordinal);
        normalized = normalized.Replace(" !", "!", StringComparison.Ordinal);
        normalized = normalized.Replace(" ,", ",", StringComparison.Ordinal);
        normalized = normalized.Replace(" ;", ";", StringComparison.Ordinal);
        normalized = normalized.Replace(" :", ":", StringComparison.Ordinal);
        normalized = normalized.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private bool TryResolveKnownDestination(string target, out string url)
    {
        var key = NormalizeAlias(target);
        if (_options.KnownDestinations.TryGetValue(key, out url!)
            && OpenUrlTool.NormalizeUrl(url).Success)
        {
            return true;
        }

        url = string.Empty;
        return false;
    }

    private static bool TryNormalizeExplicitUrl(string target, out string url)
    {
        var result = OpenUrlTool.NormalizeUrl(target);
        url = result.Url;
        return result.Success && LooksLikeExplicitUrlTarget(target);
    }

    private static bool TryExtractNavigationTarget(string normalized, out string target)
    {
        target = string.Empty;
        foreach (var prefix in NavigatePrefixes)
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target = CleanTarget(normalized[prefix.Length..]);
            return !string.IsNullOrWhiteSpace(target);
        }

        return false;
    }

    private static string CleanTarget(string target)
    {
        var cleaned = target.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var suffix in new[]
        {
            " inside you",
            " inside merlin",
            " in merlin",
            " in the browser",
            " in browser",
            " on the web",
            " as a website",
            " website",
            " for me please",
            " for me sir",
            " for me",
            " please",
            " sir"
        })
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^suffix.Length].Trim();
                break;
            }
        }

        foreach (var prefix in new[] { "the ", "my " })
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[prefix.Length..].Trim();
                break;
            }
        }

        return cleaned;
    }

    private static string CleanPayload(string target)
    {
        return target.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
    }

    private static string StripLeadingCommandWrappers(string normalized)
    {
        var current = normalized;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in LeadingCommandWrappers.OrderByDescending(static value => value.Length))
            {
                if (!current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                current = current[prefix.Length..].Trim();
                changed = true;
                break;
            }
        }

        return current;
    }

    private static string StripTrailingControlWrappers(string normalized)
    {
        var current = normalized;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var suffix in TrailingControlWrappers.OrderByDescending(static value => value.Length))
            {
                if (!current.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                current = current[..^suffix.Length].Trim();
                changed = true;
                break;
            }
        }

        return current;
    }

    private static string NormalizeAlias(string alias)
    {
        return string.Join(' ', alias.Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool LooksLikeExplicitUrlTarget(string target)
    {
        return target.Contains("://", StringComparison.Ordinal)
            || target.Contains('.') && !target.Contains(' ') && !target.Contains('\\');
    }

    private static bool IsExternalBrowserProduct(string target)
    {
        return target is "chrome" or "google chrome" or "edge" or "microsoft edge" or "firefox" or "mozilla firefox";
    }
}
