namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackVectorBuilder : IFeedbackVectorBuilder
{
    public IReadOnlyDictionary<string, double> Build(FeedbackContext context)
    {
        var vector = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        Add(vector, PhaseKey(context.Phase), 1.0);
        Add(vector, DomainKey(context.Domain), 1.0);
        Add(vector, DurationKey(context.DurationEstimate), 1.0);
        Add(vector, ConfidenceKey(context.Confidence), 1.0);

        if (context.NeedsConfirmation)
        {
            Add(vector, "risk.confirmation", 1.0);
        }

        if (context.IsVoiceInteraction)
        {
            Add(vector, "interaction.voice", 1.0);
        }

        if (context.IsOrbClient)
        {
            Add(vector, "interaction.orb", 1.0);
        }

        if (context.IsExternalAction)
        {
            Add(vector, "action.external", 1.0);
        }

        var isInterruptionContext = context.Domain == FeedbackDomain.Interruption || context.IsInterruptionFeedback;

        if (isInterruptionContext)
        {
            Add(vector, "domain.interruption", 1.0);
        }

        if (isInterruptionContext && context.IsRecompositionFeedback)
        {
            Add(vector, "interruption.recompose", 1.0);
        }

        if (isInterruptionContext)
        {
            AddInterruptionHint(vector, context.InterruptionType);
            AddInterruptionHint(vector, context.InterruptionStrategy);
        }

        foreach (var tag in context.Tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                var normalizedTag = Normalize(tag);
                Add(vector, $"tag.{normalizedTag}", 0.5);
                if (isInterruptionContext)
                {
                    AddInterruptionTag(vector, normalizedTag);
                }
            }
        }

        return vector;
    }

    private static void Add(Dictionary<string, double> vector, string? key, double weight)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        vector[key] = vector.TryGetValue(key, out var existing)
            ? existing + weight
            : weight;
    }

    private static void AddInterruptionHint(Dictionary<string, double> vector, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = Normalize(value);
        if (normalized.Contains("correction", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.correction", 1.0);
        }

        if (normalized.Contains("correct", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.correction", 1.0);
        }

        if (normalized.Contains("redirect", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.redirect", 1.0);
        }

        if (normalized.Contains("switch", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.redirect", 1.0);
        }

        if (normalized.Contains("clarification", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.clarification", 1.0);
        }

        if (normalized.Contains("clarify", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.clarification", 1.0);
        }

        if (normalized.Contains("recompose", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("include", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.recompose", 1.0);
        }

        if (normalized.Contains("wait", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.waiting", 1.0);
        }

        if (normalized.Contains("follow_up", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("followup", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.follow_up", 1.0);
        }

        if (normalized.Contains("queue", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.queue", 1.0);
        }

        if (normalized.Contains("unclear", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.unclear", 1.0);
        }

        if (normalized.Contains("side_comment", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.side_comment", 1.0);
        }

        if (normalized.Contains("additional_context", StringComparison.OrdinalIgnoreCase))
        {
            Add(vector, "interruption.additional_context", 1.0);
        }
    }

    private static void AddInterruptionTag(Dictionary<string, double> vector, string normalizedTag)
    {
        switch (normalizedTag)
        {
            case "correction":
            case "redirect":
            case "clarification":
            case "recompose":
            case "waiting":
            case "follow_up":
            case "queue":
            case "unclear":
            case "side_comment":
            case "additional_context":
                Add(vector, $"interruption.{normalizedTag}", 1.0);
                break;
        }
    }

    private static string? PhaseKey(FeedbackPhase phase)
    {
        return phase switch
        {
            FeedbackPhase.Starting => "phase.starting",
            FeedbackPhase.Interpreting => "phase.interpreting",
            FeedbackPhase.Planning => "phase.planning",
            FeedbackPhase.Executing => "phase.executing",
            FeedbackPhase.Waiting => "phase.waiting",
            FeedbackPhase.StillWorking => "phase.still_working",
            FeedbackPhase.NeedsConfirmation => "phase.needs_confirmation",
            FeedbackPhase.Completing => "phase.completing",
            FeedbackPhase.Failed => "phase.failed",
            FeedbackPhase.HandlingInterruption => "phase.handling_interruption",
            FeedbackPhase.ClarifyingInterruption => "phase.clarifying_interruption",
            FeedbackPhase.RecomposingContinuation => "phase.recomposing_continuation",
            FeedbackPhase.Redirecting => "phase.redirecting",
            FeedbackPhase.QueueingFollowUp => "phase.queueing_followup",
            _ => null
        };
    }

    private static string? DomainKey(FeedbackDomain domain)
    {
        return domain switch
        {
            FeedbackDomain.General => "domain.general",
            FeedbackDomain.Conversation => "domain.conversation",
            FeedbackDomain.Interruption => "domain.interruption",
            FeedbackDomain.LocalTool => "domain.local_tool",
            FeedbackDomain.ExternalApp => "domain.external_app",
            FeedbackDomain.FileSearch => "domain.file_search",
            FeedbackDomain.WebSearch => "domain.web_search",
            FeedbackDomain.Memory => "domain.memory",
            FeedbackDomain.Calendar => "domain.calendar",
            FeedbackDomain.Messaging => "domain.messaging",
            FeedbackDomain.Voice => "domain.voice",
            FeedbackDomain.System => "domain.system",
            FeedbackDomain.Confirmation => "domain.confirmation",
            _ => null
        };
    }

    private static string? DurationKey(FeedbackDurationEstimate estimate)
    {
        return estimate switch
        {
            FeedbackDurationEstimate.Instant => "duration.instant",
            FeedbackDurationEstimate.Short => "duration.short",
            FeedbackDurationEstimate.Medium => "duration.medium",
            FeedbackDurationEstimate.Long => "duration.long",
            _ => null
        };
    }

    private static string? ConfidenceKey(FeedbackConfidence confidence)
    {
        return confidence switch
        {
            FeedbackConfidence.Low => "confidence.low",
            FeedbackConfidence.Medium => "confidence.medium",
            FeedbackConfidence.High => "confidence.high",
            _ => null
        };
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}
