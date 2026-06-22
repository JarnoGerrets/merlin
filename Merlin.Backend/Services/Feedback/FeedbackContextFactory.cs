using Merlin.Backend.Models;
using Merlin.Backend.Tools;

namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackContextFactory : IFeedbackContextFactory
{
    public FeedbackContext CreateInitial(
        AssistantRequest request,
        string correlationId,
        string normalizedUserText,
        DateTimeOffset createdAtUtc)
    {
        var isVoiceInteraction = IsVoiceInteraction(request);
        var isOrbClient = string.Equals(request.ClientMode, "orb", StringComparison.OrdinalIgnoreCase);
        return new FeedbackContext
        {
            CorrelationId = correlationId,
            TurnId = correlationId,
            RawUserText = request.Message,
            NormalizedUserText = normalizedUserText,
            Phase = FeedbackPhase.Interpreting,
            Domain = FeedbackDomain.General,
            DurationEstimate = FeedbackDurationEstimate.Unknown,
            Confidence = FeedbackConfidence.Unknown,
            IsVoiceInteraction = isVoiceInteraction,
            IsOrbClient = isOrbClient,
            AllowSpeech = request.SpeechEventSender is not null && isVoiceInteraction,
            AllowVisualFeedback = false,
            CreatedAtUtc = createdAtUtc
        };
    }

    public FeedbackContext EnrichWithRouting(
        FeedbackContext context,
        IntentParseResult intentResult,
        ITool? tool,
        FeedbackPhase phase = FeedbackPhase.Executing)
    {
        var domain = MapDomain(intentResult, tool);
        var needsConfirmation = NeedsConfirmation(intentResult, tool);
        if (needsConfirmation)
        {
            domain = FeedbackDomain.Confirmation;
            phase = FeedbackPhase.NeedsConfirmation;
        }

        return new FeedbackContext
        {
            CorrelationId = context.CorrelationId,
            TurnId = context.TurnId,
            RawUserText = string.IsNullOrWhiteSpace(intentResult.OriginalMessage)
                ? context.RawUserText
                : intentResult.OriginalMessage,
            NormalizedUserText = string.IsNullOrWhiteSpace(intentResult.NormalizedCommand)
                ? context.NormalizedUserText
                : intentResult.NormalizedCommand,
            Phase = phase,
            Domain = domain,
            DurationEstimate = EstimateDuration(domain, intentResult),
            Confidence = EstimateConfidence(intentResult.Confidence),
            Urgency = context.Urgency,
            Intent = intentResult.Intent,
            ToolName = tool?.Name,
            TargetName = null,
            IsVoiceInteraction = context.IsVoiceInteraction,
            IsOrbClient = context.IsOrbClient,
            IsExternalAction = IsExternalAction(domain, tool),
            NeedsConfirmation = needsConfirmation,
            IsUserWaiting = context.IsUserWaiting,
            AllowSpeech = context.AllowSpeech,
            AllowVisualFeedback = context.AllowVisualFeedback,
            IsInterruptionFeedback = context.IsInterruptionFeedback,
            InterruptionType = context.InterruptionType,
            InterruptionStrategy = context.InterruptionStrategy,
            IsRecompositionFeedback = context.IsRecompositionFeedback,
            SuppressNormalProgressFeedback = context.SuppressNormalProgressFeedback,
            Tags = BuildTags(domain, intentResult, tool),
            CreatedAtUtc = context.CreatedAtUtc
        };
    }

    private static bool IsVoiceInteraction(AssistantRequest request)
    {
        return string.Equals(request.InteractionSource, "voice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "voice_stream", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "voice_correction", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.InteractionSource, "backend_idle_voice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.ClientMode, "voice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.ClientMode, "orb", StringComparison.OrdinalIgnoreCase);
    }

    private static FeedbackDomain MapDomain(IntentParseResult intentResult, ITool? tool)
    {
        var routeScope = intentResult.Route?.TargetScope;
        if (string.Equals(routeScope, TargetScopes.LocalFiles, StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackDomain.FileSearch;
        }

        if (string.Equals(routeScope, TargetScopes.Memory, StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackDomain.Memory;
        }

        if (string.Equals(routeScope, TargetScopes.Web, StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackDomain.WebSearch;
        }

        if (string.Equals(routeScope, TargetScopes.Calendar, StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackDomain.Calendar;
        }

        if (string.Equals(routeScope, TargetScopes.Email, StringComparison.OrdinalIgnoreCase))
        {
            return FeedbackDomain.Messaging;
        }

        if (IsAny(intentResult.Intent, "confirmation")
            || IsAny(intentResult.CapabilityId, "confirmation")
            || IsAny(tool?.Name, "Confirmation"))
        {
            return FeedbackDomain.Confirmation;
        }

        if (IsAny(tool?.Name, "Web Search")
            || IsAny(intentResult.Intent, "web_search")
            || IsAny(intentResult.CapabilityId, "web_search"))
        {
            return FeedbackDomain.WebSearch;
        }

        if (IsAny(tool?.Name, "Open Application", "Open URL")
            || IsAny(intentResult.Intent, "open_application", "open_url")
            || IsAny(intentResult.CapabilityId, "application_launch", "url_opening"))
        {
            return FeedbackDomain.ExternalApp;
        }

        if (IsAny(tool?.Name, "System Resource")
            || IsAny(intentResult.Intent, "system_resource_query")
            || IsAny(intentResult.CapabilityId, "system_time", "system_date", "system_timezone"))
        {
            return FeedbackDomain.System;
        }

        if (IsAny(tool?.Name, "General Conversation")
            || IsAny(intentResult.Intent, "general_conversation")
            || IsAny(intentResult.CapabilityId, "general_conversation"))
        {
            return FeedbackDomain.Conversation;
        }

        if (ContainsAny(intentResult.NormalizedCommand, "memory", "remember"))
        {
            return FeedbackDomain.Memory;
        }

        return tool is null ? FeedbackDomain.General : FeedbackDomain.LocalTool;
    }

    private static bool NeedsConfirmation(IntentParseResult intentResult, ITool? tool)
    {
        return IsAny(intentResult.Intent, "confirmation")
            || IsAny(intentResult.CapabilityId, "confirmation")
            || IsAny(tool?.Name, "Confirmation")
            || intentResult.Route?.SafetyLevel is CapabilitySafetyLevel.RequiresConfirmation;
    }

    private static FeedbackDurationEstimate EstimateDuration(
        FeedbackDomain domain,
        IntentParseResult intentResult)
    {
        if (domain is FeedbackDomain.System)
        {
            return FeedbackDurationEstimate.Instant;
        }

        if (domain is FeedbackDomain.ExternalApp or FeedbackDomain.Memory)
        {
            return FeedbackDurationEstimate.Short;
        }

        if (domain is FeedbackDomain.WebSearch or FeedbackDomain.FileSearch)
        {
            return FeedbackDurationEstimate.Medium;
        }

        if (domain is FeedbackDomain.Conversation
            && IsAny(intentResult.CapabilityId, "general_conversation"))
        {
            return FeedbackDurationEstimate.Medium;
        }

        return FeedbackDurationEstimate.Unknown;
    }

    private static FeedbackConfidence EstimateConfidence(double confidence)
    {
        if (confidence >= 0.85)
        {
            return FeedbackConfidence.High;
        }

        if (confidence >= 0.55)
        {
            return FeedbackConfidence.Medium;
        }

        return confidence > 0 ? FeedbackConfidence.Low : FeedbackConfidence.Unknown;
    }

    private static bool IsExternalAction(FeedbackDomain domain, ITool? tool)
    {
        return domain is FeedbackDomain.ExternalApp or FeedbackDomain.WebSearch
            || IsAny(tool?.Name, "Open Application", "Open URL", "Web Search");
    }

    private static IReadOnlyList<string> BuildTags(
        FeedbackDomain domain,
        IntentParseResult intentResult,
        ITool? tool)
    {
        var tags = new List<string>();
        tags.Add(domain.ToString());
        if (!string.IsNullOrWhiteSpace(intentResult.Intent))
        {
            tags.Add(intentResult.Intent);
        }

        if (!string.IsNullOrWhiteSpace(intentResult.CapabilityId))
        {
            tags.Add(intentResult.CapabilityId);
        }

        if (!string.IsNullOrWhiteSpace(tool?.Name))
        {
            tags.Add(tool.Name);
        }

        return tags;
    }

    private static bool IsAny(string? value, params string[] candidates)
    {
        return !string.IsNullOrWhiteSpace(value)
            && candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));
    }
}
