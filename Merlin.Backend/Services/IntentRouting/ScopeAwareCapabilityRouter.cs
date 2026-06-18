using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.IntentRouting;

public sealed class ScopeAwareCapabilityRouter
{
    private readonly CapabilityOptions _capabilityOptions;
    private readonly ICapabilitySafetyClassifier _safetyClassifier;
    private readonly ITargetScopeDetector _targetScopeDetector;

    public ScopeAwareCapabilityRouter(
        ITargetScopeDetector targetScopeDetector,
        ICapabilitySafetyClassifier safetyClassifier,
        IOptions<CapabilityOptions> capabilityOptions)
    {
        _targetScopeDetector = targetScopeDetector;
        _safetyClassifier = safetyClassifier;
        _capabilityOptions = MergeWithDefaults(capabilityOptions.Value);
    }

    public CapabilityRouteResult Route(string userText)
    {
        var detection = _targetScopeDetector.Detect(userText);
        var capabilityId = SelectCapability(userText, detection);
        var domain = FindDomain(capabilityId);
        var availability = GetAvailability(domain, capabilityId);
        var shouldExecute = availability == CapabilityAvailability.Implemented
            && string.Equals(capabilityId, "web_search", StringComparison.OrdinalIgnoreCase);
        var intent = shouldExecute
            ? "web_search"
            : availability == CapabilityAvailability.Unsupported
                ? "unsupported_action"
                : capabilityId is "general_conversation"
                    ? "general_conversation"
                    : "missing_capability";
        var normalizedCommand = shouldExecute
            ? $"web_search {detection.ExtractedTarget ?? userText}".Trim()
            : intent == "general_conversation"
                ? $"chat {Normalize(userText)}"
                : Normalize(userText);
        var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(detection.ExtractedTarget))
        {
            arguments["query"] = detection.ExtractedTarget!;
        }

        var route = new CapabilityRouteResult(
            intent,
            detection.Action,
            detection.TargetScope,
            capabilityId,
            detection.Confidence,
            RequiresExternalInfo(capabilityId, detection),
            detection.TargetScope == TargetScopes.ProjectRepo,
            CapabilitySafetyLevel.SafeReadonly,
            null,
            AddRecommendedCandidate(detection.ScopeScores, capabilityId, detection.TargetScope, detection.Reason),
            normalizedCommand,
            arguments,
            shouldExecute,
            detection.Reason,
            domain?.Name ?? ToName(capabilityId),
            availability);

        return route with { SafetyLevel = _safetyClassifier.Classify(route) };
    }

    public IntentParseResult ToIntentParseResult(string userText)
    {
        var route = Route(userText);
        return new IntentParseResult
        {
            Intent = route.Intent,
            NormalizedCommand = route.NormalizedCommand ?? Normalize(userText),
            Confidence = route.Confidence,
            OriginalMessage = userText,
            ParserUsed = nameof(ScopeAwareCapabilityRouter),
            CapabilityId = route.RecommendedCapability,
            CapabilityName = route.CapabilityName,
            Route = route
        };
    }

    private static string SelectCapability(string userText, TargetScopeDetectionResult detection)
    {
        var text = Normalize(userText);
        if (detection.TargetScope == TargetScopes.ProjectRepo)
        {
            return detection.Action == "implement" ? "codex_implementation" : "codex_research";
        }

        return detection.TargetScope switch
        {
            TargetScopes.Web => SelectWebCapability(text, detection.Action),
            TargetScopes.LocalFiles => "file_access",
            TargetScopes.Calendar => "calendar",
            TargetScopes.Email => "email",
            TargetScopes.Memory => "memory_lookup",
            TargetScopes.System => "system_resource",
            TargetScopes.Application => "open_app",
            _ => "general_conversation"
        };
    }

    private static string SelectWebCapability(string text, string action)
    {
        if (ContainsAny(text, ["search the web", "search web", "search the internet"])
            && !ContainsAny(text, ["whether", "compare", "verify", "check if", "summarize"]))
        {
            return "web_search";
        }

        if (ContainsAny(text, ["official docs", "documentation", "current", "latest", "pricing", "whether", "known issue"])
            || action is "verify" or "find" or "lookup")
        {
            return "web_research";
        }

        return action == "search" ? "web_search" : "web_research";
    }

    private static bool RequiresExternalInfo(string capabilityId, TargetScopeDetectionResult detection)
    {
        return capabilityId is "web_search" or "web_research"
            || capabilityId is "codex_research" or "codex_implementation"
                && detection.ScopeScores.Any(score => score.TargetScope == TargetScopes.Web && score.Score >= 0.35);
    }

    private CapabilityDomain? FindDomain(string capabilityId)
    {
        return _capabilityOptions.CapabilityDomains.FirstOrDefault(domain =>
            string.Equals(domain.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
    }

    private static CapabilityAvailability GetAvailability(CapabilityDomain? domain, string capabilityId)
    {
        if (capabilityId is "general_conversation" or "system_resource" or "open_app")
        {
            return CapabilityAvailability.Unknown;
        }

        if (domain is null)
        {
            return CapabilityAvailability.Missing;
        }

        if (domain.IsImplemented)
        {
            return CapabilityAvailability.Implemented;
        }

        return string.Equals(domain.SafetyLevel, "unsupported", StringComparison.OrdinalIgnoreCase)
            ? CapabilityAvailability.Unsupported
            : CapabilityAvailability.Missing;
    }

    private static IReadOnlyList<CapabilityScore> AddRecommendedCandidate(
        IReadOnlyList<CapabilityScore> scores,
        string capabilityId,
        string scope,
        string reason)
    {
        if (scores.Any(score => string.Equals(score.CapabilityId, capabilityId, StringComparison.OrdinalIgnoreCase)))
        {
            return scores;
        }

        return scores.Prepend(new CapabilityScore(capabilityId, scope, 0.88, reason)).ToList();
    }

    private static CapabilityOptions MergeWithDefaults(CapabilityOptions configuredOptions)
    {
        if (configuredOptions.CapabilityDomains.Count == 0)
        {
            configuredOptions.CapabilityDomains = CapabilityOptions.CreateDefault().CapabilityDomains;
        }

        return configuredOptions;
    }

    private static string Normalize(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsAny(string value, IReadOnlyCollection<string> terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToName(string capabilityId)
    {
        return string.Join(' ', capabilityId.Split('_').Select(word =>
            char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
