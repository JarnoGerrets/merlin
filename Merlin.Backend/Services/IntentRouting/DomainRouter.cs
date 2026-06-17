using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class DomainRouter
{
    public IReadOnlyList<DomainScore> ScoreDomains(NormalizedInput input)
    {
        var text = input.Text;
        var scores = new Dictionary<IntentDomain, (double Score, List<string> Reasons)>
        {
            [IntentDomain.LocalSystem] = (0, []),
            [IntentDomain.Audio] = (0, []),
            [IntentDomain.AppControl] = (0, []),
            [IntentDomain.Memory] = (0, []),
            [IntentDomain.WebSearch] = (0, []),
            [IntentDomain.GeneralChat] = (0.18, ["default fallback"])
        };

        AddLocalSystemScore(text, scores);
        AddAudioScore(text, scores);
        AddAppControlScore(text, scores);
        AddWebNavigationScore(input, scores);
        AddMemoryScore(text, scores);

        if (LooksLikeGeneralQuestion(text))
        {
            Add(scores, IntentDomain.GeneralChat, 0.35, "general question");
        }

        return scores
            .Select(score => new DomainScore(
                score.Key,
                Math.Clamp(score.Value.Score, 0, 1),
                string.Join(", ", score.Value.Reasons.Distinct())))
            .OrderByDescending(score => score.Score)
            .ToArray();
    }

    private static void AddLocalSystemScore(
        string text,
        IDictionary<IntentDomain, (double Score, List<string> Reasons)> scores)
    {
        if (ContainsAny(text, ["timezone", "clock", "current time", "current date", "today's date", "todays date"]))
        {
            Add(scores, IntentDomain.LocalSystem, 0.7, "current time/date/timezone language");
        }

        if ((ContainsWholePhrase(text, "time") || ContainsWholePhrase(text, "date"))
            && ContainsAny(text, ["what", "which", "tell me", "check", "current", "today", "now"]))
        {
            Add(scores, IntentDomain.LocalSystem, 0.45, "system clock request shape");
        }

        if (ContainsAny(text, ["cpu", "processor", "disk", "drive space", "battery", "network", "ram", "system memory", "pc memory", "memory usage"]))
        {
            Add(scores, IntentDomain.LocalSystem, 0.72, "system resource language");
        }

        if (ContainsWholePhrase(text, "memory") && ContainsAny(text, ["available", "left", "usage", "pc", "system", "ram"]))
        {
            Add(scores, IntentDomain.LocalSystem, 0.65, "device memory context");
        }

        if (ContainsAny(text, ["remember the time", "last time", "time complexity", "great time", "good time", "memory of backpacking", "memory of childhood", "memory of my trip"]))
        {
            Add(scores, IntentDomain.LocalSystem, -0.7, "personal or abstract time/memory context");
        }
    }

    private static void AddAudioScore(
        string text,
        IDictionary<IntentDomain, (double Score, List<string> Reasons)> scores)
    {
        if (ContainsAny(text, ["volume", "sound", "speaker", "mute", "unmute", "louder", "quieter"]))
        {
            Add(scores, IntentDomain.Audio, 0.65, "audio control language");
        }

        if (ContainsAny(text, ["current volume", "system volume", "speaker volume", "spotify volume", "chrome volume"]))
        {
            Add(scores, IntentDomain.Audio, 0.25, "audio target context");
        }

        if (ContainsAny(text, ["volume of a cylinder", "volume of a sphere", "volume of water", "calculate volume", "formula for volume"]))
        {
            Add(scores, IntentDomain.Audio, -0.75, "mathematical or measurement volume context");
        }
    }

    private static void AddAppControlScore(
        string text,
        IDictionary<IntentDomain, (double Score, List<string> Reasons)> scores)
    {
        if (ContainsAny(text, ["newsfeed", "news feed"]))
        {
            return;
        }

        if (StartsWithAny(text, ["open ", "launch ", "start ", "pull up ", "close ", "focus ", "switch to "])
            || ContainsAny(text, ["open", "launch", "start", "pull up", "close", "focus", "switch to"]))
        {
            Add(scores, IntentDomain.AppControl, 0.7, "application command verb");
        }
    }

    private static void AddWebNavigationScore(
        NormalizedInput input,
        IDictionary<IntentDomain, (double Score, List<string> Reasons)> scores)
    {
        var original = input.OriginalText.Trim().TrimEnd('.', '!', '?', ';', ':', ',');
        if (ContainsNavigationVerb(input.Text) && (ContainsUrlLikeTarget(original) || ContainsBrowserTarget(input.Text)))
        {
            Add(scores, IntentDomain.WebSearch, 0.82, "URL navigation request");
        }
    }

    private static void AddMemoryScore(
        string text,
        IDictionary<IntentDomain, (double Score, List<string> Reasons)> scores)
    {
        if (ContainsAny(text, ["remember", "memory of", "last time we talked", "save this", "forget that"]))
        {
            Add(scores, IntentDomain.Memory, 0.65, "personal memory language");
        }

        if (ContainsAny(text, ["remember the time", "last time we talked", "memory of backpacking", "memory of childhood", "memory of my trip"]))
        {
            Add(scores, IntentDomain.Memory, 0.25, "semantic memory context");
        }

        if (ContainsAny(text, ["ram", "system memory", "pc memory", "memory usage"]))
        {
            Add(scores, IntentDomain.Memory, -0.45, "device memory context");
        }
    }

    private static bool LooksLikeGeneralQuestion(string text)
    {
        return StartsWithAny(text, ["what is ", "how ", "why ", "explain ", "help me ", "tell me about "]);
    }

    private static void Add(
        IDictionary<IntentDomain, (double Score, List<string> Reasons)> scores,
        IntentDomain domain,
        double score,
        string reason)
    {
        var current = scores[domain];
        current.Score += score;
        current.Reasons.Add(reason);
        scores[domain] = current;
    }

    internal static bool ContainsAny(string value, IReadOnlyCollection<string> terms)
    {
        return terms.Any(term => term.EndsWith(' ')
            ? value.StartsWith(term, StringComparison.OrdinalIgnoreCase)
            : ContainsWholePhrase(value, term));
    }

    internal static bool StartsWithAny(string value, IReadOnlyCollection<string> prefixes)
    {
        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool ContainsNavigationVerb(string value)
    {
        return ContainsAny(value, ["open", "go to", "take me to", "browse", "visit", "launch", "start", "pull up"]);
    }

    internal static bool ContainsBrowserTarget(string value)
    {
        return ContainsAny(value, ["in browser", "in the browser", "website", "web site"]);
    }

    internal static bool ContainsUrlLikeTarget(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Any(word =>
        {
            var trimmed = word.Trim('\'', '"', '(', ')', '[', ']', '{', '}', ',', ';', ':', '!', '?');
            return trimmed.Contains("://", StringComparison.Ordinal)
                || trimmed.Contains('.') && !trimmed.Contains('\\') && !trimmed.Contains('/');
        });
    }

    internal static bool ContainsWholePhrase(string value, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var beforeIsBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        var afterIndex = index + phrase.Length;
        var afterIsBoundary = afterIndex >= value.Length || !char.IsLetterOrDigit(value[afterIndex]);

        return beforeIsBoundary && afterIsBoundary;
    }
}
