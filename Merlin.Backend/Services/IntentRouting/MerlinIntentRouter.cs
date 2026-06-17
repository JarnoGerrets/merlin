using Merlin.Backend.Models;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class MerlinIntentRouter
{
    private readonly CapabilityRegistry _capabilityRegistry;
    private readonly CapabilityRouter _capabilityRouter;
    private readonly DomainRouter _domainRouter;
    private readonly EmergencyIntentRouter _emergencyIntentRouter;
    private readonly IIntentClassifier _intentClassifier;
    private readonly ILogger<MerlinIntentRouter> _logger;
    private readonly TextNormalizer _textNormalizer;

    public MerlinIntentRouter(
        TextNormalizer textNormalizer,
        EmergencyIntentRouter emergencyIntentRouter,
        DomainRouter domainRouter,
        CapabilityRegistry capabilityRegistry,
        CapabilityRouter capabilityRouter,
        IIntentClassifier intentClassifier,
        ILogger<MerlinIntentRouter> logger)
    {
        _textNormalizer = textNormalizer;
        _emergencyIntentRouter = emergencyIntentRouter;
        _domainRouter = domainRouter;
        _capabilityRegistry = capabilityRegistry;
        _capabilityRouter = capabilityRouter;
        _intentClassifier = intentClassifier;
        _logger = logger;
    }

    public async Task<RouteDecision> RouteAsync(string userText, CancellationToken cancellationToken = default)
    {
        var input = _textNormalizer.Normalize(userText);
        _logger.LogInformation(
            "Intent routing started. OriginalText: {OriginalText}. NormalizedText: {NormalizedText}",
            input.OriginalText,
            input.Text);

        var emergency = _emergencyIntentRouter.TryRoute(input);
        if (emergency is not null)
        {
            _logger.LogInformation("Emergency route matched: {CapabilityId}", emergency.CapabilityId);
            return emergency;
        }

        var domainScores = _domainRouter.ScoreDomains(input);
        _logger.LogInformation(
            "Top domains: {TopDomains}",
            string.Join(", ", domainScores.Take(4).Select(score => $"{score.Domain}={score.Score:0.00}")));

        var topDomains = domainScores
            .Where(score => score.Score >= 0.35)
            .OrderByDescending(score => score.Score)
            .Take(2)
            .ToList();

        if (topDomains.Count == 0)
        {
            _logger.LogInformation("Routing to DeepInfra chat fallback. Reason: No strong domain match.");
            return RouteDecision.NoTool(IntentDomain.GeneralChat, "No strong domain match.");
        }

        var candidates = _capabilityRouter.GetCandidates(input, topDomains, _capabilityRegistry);
        _logger.LogInformation(
            "Capability candidates: {Candidates}",
            string.Join(", ", candidates.Select(candidate => candidate.Id)));

        if (candidates.Count == 0)
        {
            _logger.LogInformation("Routing to DeepInfra chat fallback. Reason: No capability candidates.");
            return RouteDecision.NoTool(IntentDomain.GeneralChat, "No capability candidates.");
        }

        var classification = await _intentClassifier.ClassifyAsync(input, candidates, cancellationToken);
        _logger.LogInformation(
            "Intent classifier selected {CapabilityId} confidence={Confidence:0.00}. Reason: {Reason}",
            classification.CapabilityId,
            classification.Confidence,
            classification.Reason);

        var selectedCandidate = candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, classification.CapabilityId, StringComparison.OrdinalIgnoreCase));
        if (selectedCandidate is null
            || string.Equals(classification.CapabilityId, "no_tool", StringComparison.OrdinalIgnoreCase)
            || classification.Confidence < selectedCandidate.MinimumConfidence)
        {
            _logger.LogInformation("Routing to DeepInfra chat fallback. Reason: {Reason}", classification.Reason);
            return RouteDecision.NoTool(IntentDomain.GeneralChat, classification.Reason, classification.Confidence);
        }

        var decision = RouteDecision.Tool(
            classification.CapabilityId,
            selectedCandidate.Domain,
            classification.Confidence,
            classification.Reason);

        _logger.LogInformation("Final route decision: {CapabilityId}. ExecuteTool: {ExecuteTool}", decision.CapabilityId, decision.ShouldExecuteTool);
        return decision;
    }
}
