using System.Text.RegularExpressions;

namespace Merlin.Backend.Services.Context.ActiveSurface;

public sealed partial class BrowserMediaCommandNormalizer : IBrowserMediaCommandNormalizer
{
    private static readonly Dictionary<string, string> ExplicitPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pause video"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["pause the video"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["pause browser video"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["pause youtube"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["click pause"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["click the pause button"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["click pause button"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["press pause"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["press the pause button"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["tap pause"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["tap the pause button"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["hit pause"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["hit the pause button"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["stop the video"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["stop video"] = ActiveSurfaceCapabilities.BrowserMediaPause,

        ["play video"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["play the video"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["resume video"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["resume the video"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["click play"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["click the play button"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["click play button"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["press play"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["press the play button"] = ActiveSurfaceCapabilities.BrowserMediaPlay,

        ["fullscreen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["full screen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["go fullscreen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["go full screen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["make it fullscreen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["make it full screen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["click fullscreen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["click full screen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["click the fullscreen button"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["click the full screen button"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["exit fullscreen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["exit full screen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,

        ["skip ad"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["skip add"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["skip the ad"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["skip advertentie"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["skip advertisement"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["overslaan"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["overslan"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["over slaan"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["advertentie overslaan"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["klik overslaan"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["click skip"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd,
        ["click skip ad"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd
    };

    private static readonly Dictionary<string, string> AmbiguousPhrases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pause"] = ActiveSurfaceCapabilities.BrowserMediaPause,
        ["play"] = ActiveSurfaceCapabilities.BrowserMediaPlay,
        ["fullscreen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["full screen"] = ActiveSurfaceCapabilities.BrowserMediaFullscreen,
        ["skip"] = ActiveSurfaceCapabilities.BrowserMediaSkipAd
    };

    public BrowserMediaCommandMatch? TryMatchExplicit(string normalizedText)
    {
        var text = Normalize(normalizedText);
        return ExplicitPhrases.TryGetValue(text, out var capability)
            ? new BrowserMediaCommandMatch(capability, 0.96, "explicit_browser_media_phrase")
            : null;
    }

    public BrowserMediaCommandMatch? TryMatchAmbiguous(
        string normalizedText,
        ActiveSurfaceSnapshot activeSurface)
    {
        var text = Normalize(normalizedText);
        if (!AmbiguousPhrases.TryGetValue(text, out var capability))
        {
            return null;
        }

        if (activeSurface.Kind is not ActiveSurfaceKind.BrowserWorkspace
            || !activeSurface.Capabilities.Contains(capability))
        {
            return null;
        }

        return new BrowserMediaCommandMatch(capability, 0.88, "ambiguous_browser_media_phrase_resolved_by_active_surface");
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim().ToLowerInvariant();
        normalized = WhitespaceRegex().Replace(normalized, " ");
        normalized = normalized.Replace(" .", ".", StringComparison.Ordinal);
        normalized = normalized.Replace(" ?", "?", StringComparison.Ordinal);
        normalized = normalized.Replace(" !", "!", StringComparison.Ordinal);
        normalized = normalized.Replace(" ,", ",", StringComparison.Ordinal);
        normalized = normalized.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        normalized = StripLeadingWrappers(normalized);
        normalized = StripTrailingWrappers(normalized);
        return WhitespaceRegex().Replace(normalized, " ").Trim();
    }

    public static string CommonActionForCapability(string capability) =>
        capability switch
        {
            ActiveSurfaceCapabilities.BrowserMediaPause => "pause_video",
            ActiveSurfaceCapabilities.BrowserMediaPlay => "play_video",
            ActiveSurfaceCapabilities.BrowserMediaFullscreen => "fullscreen",
            ActiveSurfaceCapabilities.BrowserMediaSkipAd => "skip_ad",
            ActiveSurfaceCapabilities.BrowserMediaStop => "pause_video",
            _ => string.Empty
        };

    private static string StripLeadingWrappers(string normalized)
    {
        var current = normalized;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in new[]
            {
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
            }.OrderByDescending(static value => value.Length))
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

    private static string StripTrailingWrappers(string normalized)
    {
        var current = normalized;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var suffix in new[] { " please", " for me", " thanks", " thank you" }
                         .OrderByDescending(static value => value.Length))
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
