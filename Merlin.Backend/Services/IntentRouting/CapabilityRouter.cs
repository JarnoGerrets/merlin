using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class CapabilityRouter
{
    public IReadOnlyList<CapabilityCandidate> GetCandidates(
        NormalizedInput input,
        IReadOnlyList<DomainScore> topDomains,
        CapabilityRegistry registry)
    {
        var selectedDomains = topDomains.Select(score => score.Domain).ToHashSet();
        var domainScores = topDomains.ToDictionary(score => score.Domain, score => score.Score);
        var candidates = registry.GetAll()
            .Where(capability => selectedDomains.Contains(capability.Domain))
            .Select(capability => ToCandidate(input, capability, domainScores[capability.Domain]))
            .Where(candidate => candidate.Score > 0.15)
            .OrderByDescending(candidate => candidate.Score)
            .Take(7)
            .ToList();

        candidates.Add(new CapabilityCandidate(
            "no_tool",
            IntentDomain.GeneralChat,
            "Use normal chat without a local tool.",
            "DeepInfra.Chat",
            0.25,
            0));

        return candidates;
    }

    private static CapabilityCandidate ToCandidate(
        NormalizedInput input,
        CapabilityDefinition capability,
        double domainScore)
    {
        var score = domainScore;
        var text = input.Text;

        score += capability.TriggerHints.Count(hint => DomainRouter.ContainsWholePhrase(text, hint)) * 0.16;
        score -= capability.RejectHints.Count(hint => DomainRouter.ContainsWholePhrase(text, hint)) * 0.22;

        return new CapabilityCandidate(
            capability.Id,
            capability.Domain,
            capability.Description,
            capability.HandlerName,
            Math.Clamp(score, 0, 1),
            capability.MinimumConfidence);
    }
}
