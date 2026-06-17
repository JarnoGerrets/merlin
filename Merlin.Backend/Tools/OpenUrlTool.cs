using Merlin.Backend.Models;

namespace Merlin.Backend.Tools;

public sealed class OpenUrlTool : ITool
{
    private const string IntentName = "open_url";
    private readonly IProcessLauncher _processLauncher;

    private static readonly string[] SupportedPrefixes =
    [
        "go to ",
        "open ",
        "browse ",
        "visit ",
        "pull up "
    ];

    private static readonly HashSet<string> BlockedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file",
        "ftp",
        "javascript",
        "data",
        "cmd",
        "powershell"
    };

    public OpenUrlTool(IProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher;
    }

    public string Name => "Open URL";

    public string Description => "Opens safe HTTP/HTTPS URLs in the default browser.";

    public IReadOnlyCollection<string> Examples { get; } =
    [
        "open google.com",
        "open https://google.com",
        "go to google.com",
        "browse google.com",
        "visit google.com",
        "pull up facebook.com",
        "open facebook in the browser"
    ];

    public bool CanHandle(string command)
    {
        if (!TryExtractTarget(command, out var target))
        {
            return false;
        }

        return LooksLikeUrlInput(target) || TryNormalizeBrowserTarget(target, out _, allowBareTarget: false);
    }

    public async Task<ToolResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        if (!TryExtractTarget(command, out var target))
        {
            return InvalidUrl();
        }

        var normalizationResult = NormalizeUrl(target);
        if (!normalizationResult.Success)
        {
            return normalizationResult.ErrorCode == "BLOCKED_URL_SCHEME"
                ? BlockedUrlScheme()
                : InvalidUrl();
        }

        try
        {
            await _processLauncher.LaunchAsync(normalizationResult.Url, cancellationToken);

            return new ToolResult
            {
                Success = true,
                Message = $"Opening {normalizationResult.Url}...",
                ToolName = Name,
                Intent = IntentName
            };
        }
        catch (Exception exception)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Failed to open URL: {exception.Message}",
                ErrorCode = "TOOL_EXECUTION_FAILED",
                ToolName = Name,
                Intent = IntentName
            };
        }
    }

    internal static UrlNormalizationResult NormalizeUrl(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return UrlNormalizationResult.InvalidUrl();
        }

        var trimmedTarget = target.Trim();
        if (IsLocalWindowsPath(trimmedTarget) || IsUncPath(trimmedTarget))
        {
            return UrlNormalizationResult.InvalidUrl();
        }

        if (TryGetExplicitScheme(trimmedTarget, out var scheme))
        {
            if (BlockedSchemes.Contains(scheme))
            {
                return UrlNormalizationResult.BlockedScheme();
            }

            if (!IsAllowedScheme(scheme))
            {
                return UrlNormalizationResult.BlockedScheme();
            }
        }
        else
        {
            if (!trimmedTarget.Contains('.', StringComparison.Ordinal)
                && TryNormalizeBrowserTarget(trimmedTarget, out var browserTarget, allowBareTarget: false))
            {
                trimmedTarget = browserTarget;
            }

            trimmedTarget = $"https://{trimmedTarget}";
        }

        if (!Uri.TryCreate(trimmedTarget, UriKind.Absolute, out var uri))
        {
            return UrlNormalizationResult.InvalidUrl();
        }

        if (!IsAllowedScheme(uri.Scheme))
        {
            return BlockedSchemes.Contains(uri.Scheme)
                ? UrlNormalizationResult.BlockedScheme()
                : UrlNormalizationResult.InvalidUrl();
        }

        if (string.IsNullOrWhiteSpace(uri.Host) || !uri.Host.Contains('.'))
        {
            return UrlNormalizationResult.InvalidUrl();
        }

        return UrlNormalizationResult.Valid(uri.AbsoluteUri.TrimEnd('/'));
    }

    private static bool TryExtractTarget(string command, out string target)
    {
        target = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var normalizedCommand = command.Trim();
        foreach (var prefix in SupportedPrefixes)
        {
            if (!normalizedCommand.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            target = normalizedCommand[prefix.Length..].Trim();
            return !string.IsNullOrWhiteSpace(target);
        }

        return false;
    }

    internal static bool TryNormalizeBrowserTarget(
        string target,
        out string normalizedTarget,
        bool allowBareTarget = true)
    {
        normalizedTarget = string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var cleaned = target.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        var hadBrowserSuffix = false;
        foreach (var suffix in new[]
        {
            " in the browser",
            " in browser",
            " on the web",
            " as a website",
            " website",
            " web site"
        })
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^suffix.Length].Trim();
                hadBrowserSuffix = true;
                break;
            }
        }

        if (!hadBrowserSuffix && !allowBareTarget)
        {
            return false;
        }

        if (cleaned.Contains(' ')
            || cleaned.Contains('.')
            || cleaned.Contains("://", StringComparison.Ordinal)
            || cleaned.Contains('\\')
            || cleaned.Contains('/'))
        {
            return false;
        }

        if (!cleaned.All(character => char.IsLetterOrDigit(character) || character == '-')
            || cleaned.StartsWith("-" , StringComparison.Ordinal)
            || cleaned.EndsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        normalizedTarget = $"{cleaned.ToLowerInvariant()}.com";
        return true;
    }

    private static bool LooksLikeUrlInput(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (IsLocalWindowsPath(target) || IsUncPath(target))
        {
            return true;
        }

        if (TryGetExplicitScheme(target, out _))
        {
            return true;
        }

        return target.Contains('.') && !target.Contains(' ');
    }

    private static bool TryGetExplicitScheme(string target, out string scheme)
    {
        scheme = string.Empty;
        var schemeSeparatorIndex = target.IndexOf(':');

        if (schemeSeparatorIndex <= 0)
        {
            return false;
        }

        scheme = target[..schemeSeparatorIndex];
        return scheme.All(character => char.IsLetterOrDigit(character)
            || character == '+'
            || character == '-'
            || character == '.');
    }

    private static bool IsAllowedScheme(string scheme)
    {
        return string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalWindowsPath(string target)
    {
        return target.Length >= 3
            && char.IsLetter(target[0])
            && target[1] == ':'
            && (target[2] == '\\' || target[2] == '/');
    }

    private static bool IsUncPath(string target)
    {
        return target.StartsWith(@"\\", StringComparison.Ordinal);
    }

    private ToolResult InvalidUrl()
    {
        return new ToolResult
        {
            Success = false,
            Message = "Invalid URL.",
            ErrorCode = "INVALID_URL",
            ToolName = Name,
            Intent = IntentName
        };
    }

    private ToolResult BlockedUrlScheme()
    {
        return new ToolResult
        {
            Success = false,
            Message = "Blocked URL scheme.",
            ErrorCode = "BLOCKED_URL_SCHEME",
            ToolName = Name,
            Intent = IntentName
        };
    }

    internal readonly record struct UrlNormalizationResult(
        bool Success,
        string Url,
        string? ErrorCode)
    {
        public static UrlNormalizationResult Valid(string url)
        {
            return new UrlNormalizationResult(true, url, null);
        }

        public static UrlNormalizationResult InvalidUrl()
        {
            return new UrlNormalizationResult(false, string.Empty, "INVALID_URL");
        }

        public static UrlNormalizationResult BlockedScheme()
        {
            return new UrlNormalizationResult(false, string.Empty, "BLOCKED_URL_SCHEME");
        }
    }
}
