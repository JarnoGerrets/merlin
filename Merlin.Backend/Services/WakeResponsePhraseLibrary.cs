using System.Collections.Concurrent;

namespace Merlin.Backend.Services;

public sealed class WakeResponsePhraseLibrary
{
    private static readonly IReadOnlyList<WakeResponsePhrase> Phrases =
    [
        new("wake.response.01", "I'm awake."),
        new("wake.response.02", "I'm here."),
        new("wake.response.03", "I'm listening."),
        new("wake.response.04", "Ready."),
        new("wake.response.05", "I'm with you."),
        new("wake.response.06", "Here."),
        new("wake.response.07", "Awake and listening."),
        new("wake.response.08", "I'm here. Go ahead.")
    ];

    private static readonly IReadOnlyList<WakeResponsePhrase> SleepPhrases =
    [
        new("sleep.response.01", "Going to sleep."),
        new("sleep.response.02", "Sleeping now."),
        new("sleep.response.03", "I'll stand by."),
        new("sleep.response.04", "Resting until you need me.")
    ];

    private readonly ConcurrentDictionary<string, PhraseUsage> _usage = new(StringComparer.OrdinalIgnoreCase);
    private string? _lastPhraseId;

    public IReadOnlyCollection<string> CommonPhrases => Phrases.Concat(SleepPhrases).Select(phrase => phrase.Text).ToArray();

    public WakeResponsePhrase Select(DateTimeOffset now)
    {
        return SelectFrom(Phrases, now);
    }

    public WakeResponsePhrase SelectSleep(DateTimeOffset now)
    {
        return SelectFrom(SleepPhrases, now);
    }

    private WakeResponsePhrase SelectFrom(IReadOnlyList<WakeResponsePhrase> phrases, DateTimeOffset now)
    {
        var selected = phrases
            .Where(phrase => !string.Equals(phrase.Id, _lastPhraseId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(phrase => UsageFor(phrase.Id).LastUsedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(phrase => UsageFor(phrase.Id).RecentUseCount)
            .ThenBy(phrase => phrase.Id, StringComparer.Ordinal)
            .DefaultIfEmpty(phrases[0])
            .First();

        _usage.AddOrUpdate(
            selected.Id,
            _ => new PhraseUsage(now, 1),
            (_, existing) => existing with
            {
                LastUsedUtc = now,
                RecentUseCount = existing.RecentUseCount + 1
            });
        _lastPhraseId = selected.Id;
        return selected;
    }

    private PhraseUsage UsageFor(string phraseId)
    {
        return _usage.GetValueOrDefault(phraseId) ?? new PhraseUsage(null, 0);
    }

    private sealed record PhraseUsage(DateTimeOffset? LastUsedUtc, int RecentUseCount);
}

public sealed record WakeResponsePhrase(string Id, string Text);
