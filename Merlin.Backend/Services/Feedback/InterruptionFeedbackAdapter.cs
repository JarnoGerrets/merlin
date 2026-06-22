namespace Merlin.Backend.Services.Feedback;

public sealed class InterruptionFeedbackAdapter : IInterruptionFeedbackAdapter
{
    public FeedbackContext CreateBridgeContext(InterruptionFeedbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tags = BuildTags(request);

        return new FeedbackContext
        {
            CorrelationId = request.CorrelationId,
            TurnId = request.TurnId,
            Phase = ResolvePhase(request),
            Domain = FeedbackDomain.Interruption,
            DurationEstimate = request.DurationEstimate,
            Confidence = request.Confidence,
            Urgency = request.IsUnclear ? FeedbackUrgency.Normal : FeedbackUrgency.High,
            AllowSpeech = request.RequiresBridgeFeedback,
            IsInterruptionFeedback = true,
            IsRecompositionFeedback = request.IsRecompositionFeedback || request.IsWaitBridge,
            InterruptionType = NullIfWhiteSpace(request.InterruptionType),
            InterruptionStrategy = NullIfWhiteSpace(request.Strategy),
            SuppressNormalProgressFeedback = true,
            Tags = tags
        };
    }

    private static FeedbackPhase ResolvePhase(InterruptionFeedbackRequest request)
    {
        if (request.PhaseHint is { } phaseHint)
        {
            return phaseHint;
        }

        if (request.IsRedirectOrCorrection)
        {
            return FeedbackPhase.Redirecting;
        }

        if (request.IsQueueFollowUp)
        {
            return FeedbackPhase.QueueingFollowUp;
        }

        if (request.IsRecompositionFeedback || request.IsWaitBridge)
        {
            return FeedbackPhase.RecomposingContinuation;
        }

        return FeedbackPhase.HandlingInterruption;
    }

    private static IReadOnlyList<string> BuildTags(InterruptionFeedbackRequest request)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "interruption"
        };

        AddNormalized(tags, request.Tags);
        AddInterruptionHints(tags, request.InterruptionType);
        AddInterruptionHints(tags, request.Strategy);

        if (request.IsRedirectOrCorrection)
        {
            tags.Add("correction");
            tags.Add("redirect");
        }

        if (request.IsRecompositionFeedback)
        {
            tags.Add("recompose");
        }

        if (request.IsWaitBridge)
        {
            tags.Add("waiting");
            tags.Add("recompose");
        }

        if (request.IsQueueFollowUp)
        {
            tags.Add("follow_up");
            tags.Add("queue");
        }

        if (request.IsUnclear)
        {
            tags.Add("unclear");
        }

        return tags.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddNormalized(HashSet<string> tags, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            var normalized = Normalize(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                tags.Add(normalized);
            }
        }
    }

    private static void AddInterruptionHints(HashSet<string> tags, string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        if (normalized.Contains("correction", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("correct", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("correction");
        }

        if (normalized.Contains("redirect", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("switch", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("redirect");
        }

        if (normalized.Contains("clarification", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("clarify", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("clarification");
        }

        if (normalized.Contains("recompose", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("include", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("recompose");
        }

        if (normalized.Contains("wait", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("waiting");
        }

        if (normalized.Contains("follow_up", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("followup", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("follow_up");
        }

        if (normalized.Contains("queue", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("queue");
        }

        if (normalized.Contains("unclear", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("unclear");
        }

        if (normalized.Contains("side_comment", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("side_comment");
        }

        if (normalized.Contains("additional_context", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add("additional_context");
        }
    }

    private static string? NullIfWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
    }
}
