namespace Merlin.Backend.Infrastructure.TrustedRegistry;

public static class TrustedRegistryNormalizers
{
    public static string NormalizeApplicationAlias(string alias)
    {
        return string.Join(
            ' ',
            alias.Trim()
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormalizeCommand(string command)
    {
        var trimmed = command.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        return string.Join(
            ' ',
            trimmed
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string NormalizeUrlAlias(string alias)
    {
        var normalized = alias.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        foreach (var suffix in new[]
        {
            " to the browser",
            " in the browser",
            " from the browser",
            " as a website",
            " website",
            " browser mapping",
            " mapping"
        })
        {
            if (normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[..^suffix.Length].Trim();
                break;
            }
        }

        return string.Join(
            ' ',
            normalized
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
