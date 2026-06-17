using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class DeterministicIntentClassifier : IIntentClassifier
{
    public Task<IntentClassificationResult> ClassifyAsync(
        NormalizedInput input,
        IReadOnlyList<CapabilityCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = input.Text;
        var selected = SelectBest(input, candidates);
        return Task.FromResult(selected);
    }

    private static IntentClassificationResult SelectBest(
        NormalizedInput input,
        IReadOnlyList<CapabilityCandidate> candidates)
    {
        var text = input.Text;
        if (TryCandidate(candidates, "system.get_timezone", out var timezone)
            && DomainRouter.ContainsWholePhrase(text, "timezone"))
        {
            return Selected(timezone, 0.94, "Timezone request matched.");
        }

        if (TryCandidate(candidates, "system.get_date", out var date)
            && (DomainRouter.ContainsWholePhrase(text, "date") || DomainRouter.ContainsWholePhrase(text, "today")))
        {
            return Selected(date, 0.92, "Current date request matched.");
        }

        if (TryCandidate(candidates, "system.get_time", out var time)
            && (DomainRouter.ContainsWholePhrase(text, "time") || DomainRouter.ContainsWholePhrase(text, "clock"))
            && !DomainRouter.ContainsAny(text, ["time complexity", "remember the time", "last time", "great time", "good time"]))
        {
            return Selected(time, 0.92, "Current time request matched.");
        }

        if (TryCandidate(candidates, "system.get_memory", out var memory)
            && DomainRouter.ContainsAny(text, ["ram", "system memory", "pc memory", "memory usage", "memory do i have left"]))
        {
            return Selected(memory, 0.88, "System memory request matched.");
        }

        if (TryCandidate(candidates, "system.get_cpu", out var cpu)
            && DomainRouter.ContainsAny(text, ["cpu", "processor"]))
        {
            return Selected(cpu, 0.88, "CPU request matched.");
        }

        if (TryCandidate(candidates, "system.get_disk", out var disk)
            && DomainRouter.ContainsAny(text, ["disk", "drive space", "storage"]))
        {
            return Selected(disk, 0.88, "Disk request matched.");
        }

        if (TryCandidate(candidates, "audio.unmute", out var unmute)
            && DomainRouter.ContainsWholePhrase(text, "unmute"))
        {
            return Selected(unmute, 0.9, "Unmute request matched.");
        }

        if (TryCandidate(candidates, "audio.mute", out var mute)
            && DomainRouter.ContainsWholePhrase(text, "mute"))
        {
            return Selected(mute, 0.9, "Mute request matched.");
        }

        if (TryCandidate(candidates, "audio.set_volume", out var setVolume)
            && DomainRouter.ContainsAny(text, ["set volume", "volume to", "louder", "quieter"]))
        {
            return Selected(setVolume, 0.86, "Volume adjustment request matched.");
        }

        if (TryCandidate(candidates, "url.open", out var urlOpen)
            && DomainRouter.ContainsNavigationVerb(text)
            && (DomainRouter.ContainsUrlLikeTarget(input.OriginalText) || DomainRouter.ContainsBrowserTarget(text)))
        {
            return Selected(urlOpen, 0.9, "URL open request matched.");
        }

        if (TryCandidate(candidates, "app.open", out var appOpen)
            && DomainRouter.ContainsAny(text, ["open", "launch", "start", "pull up"]))
        {
            return Selected(appOpen, 0.86, "Application open request matched.");
        }

        if (TryCandidate(candidates, "app.close", out var appClose)
            && DomainRouter.StartsWithAny(text, ["close ", "quit "]))
        {
            return Selected(appClose, 0.84, "Application close request matched.");
        }

        if (TryCandidate(candidates, "memory.search", out var memorySearch)
            && DomainRouter.ContainsAny(text, ["remember", "memory of", "last time we talked"]))
        {
            return Selected(memorySearch, 0.82, "Personal memory request matched.");
        }

        var best = candidates
            .Where(candidate => !string.Equals(candidate.Id, "no_tool", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();

        if (best is not null && best.Score >= Math.Max(best.MinimumConfidence, 0.84))
        {
            return Selected(best, best.Score, "Best candidate exceeded confidence threshold.");
        }

        return new IntentClassificationResult("no_tool", 0.7, "No candidate exceeded deterministic classifier threshold.");
    }

    private static bool TryCandidate(
        IReadOnlyList<CapabilityCandidate> candidates,
        string id,
        out CapabilityCandidate candidate)
    {
        candidate = candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))!;
        return candidate is not null;
    }

    private static IntentClassificationResult Selected(
        CapabilityCandidate candidate,
        double confidence,
        string reason)
    {
        return new IntentClassificationResult(candidate.Id, Math.Min(confidence, candidate.Score + 0.12), reason);
    }
}
