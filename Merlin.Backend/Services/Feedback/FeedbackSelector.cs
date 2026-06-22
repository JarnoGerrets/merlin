using Merlin.Backend.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackSelector : IFeedbackSelector
{
    private readonly IFeedbackCardProvider _cardProvider;
    private readonly IFeedbackCooldownTracker _cooldownTracker;
    private readonly ILogger<FeedbackSelector> _logger;
    private readonly ResponsiveFeedbackOptions _options;
    private readonly IFeedbackVectorBuilder _vectorBuilder;

    public FeedbackSelector(
        IFeedbackCardProvider cardProvider,
        IFeedbackVectorBuilder vectorBuilder,
        IFeedbackCooldownTracker cooldownTracker,
        IOptions<ResponsiveFeedbackOptions> options,
        ILogger<FeedbackSelector> logger)
    {
        _cardProvider = cardProvider;
        _vectorBuilder = vectorBuilder;
        _cooldownTracker = cooldownTracker;
        _options = options.Value;
        _logger = logger;
    }

    public FeedbackSelection? Select(FeedbackContext context)
    {
        if (!_options.Enabled)
        {
            LogSuppressed(context, "disabled");
            return null;
        }

        var contextVector = _vectorBuilder.Build(context);
        var now = DateTimeOffset.UtcNow;
        var best = _cardProvider.GetCards()
            .Where(card => IsSelectable(card, context, contextVector, now))
            .Select(card => new
            {
                Card = card,
                Score = Similarity(contextVector, card.Vector) + card.Priority * 0.05
            })
            .Where(candidate => candidate.Score >= _options.MinimumSelectionScore)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Card.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (best is null)
        {
            LogSuppressed(context, "no_matching_cards");
            return null;
        }

        _cooldownTracker.MarkUsed(best.Card, context, now);
        return new FeedbackSelection
        {
            Card = best.Card,
            Score = best.Score,
            Reason = "highest_weighted_card_match",
            SelectedAtUtc = now
        };
    }

    private bool IsSelectable(
        FeedbackCard card,
        FeedbackContext context,
        IReadOnlyDictionary<string, double> contextVector,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(card.Id) || string.IsNullOrWhiteSpace(card.Text))
        {
            return false;
        }

        if (card.OutputMode.HasFlag(FeedbackOutputMode.Speech) && !context.AllowSpeech)
        {
            return false;
        }

        if (card.RequiresConfirmationContext && !context.NeedsConfirmation)
        {
            return false;
        }

        if (!card.RequiresConfirmationContext
            && context.NeedsConfirmation
            && _options.PreferTaskAwareFeedback
            && !card.Tags.Contains("confirmation", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (card.RequiresInterruptionContext && !context.IsInterruptionFeedback)
        {
            return false;
        }

        if (!card.RequiresInterruptionContext
            && context.IsInterruptionFeedback
            && !card.Tags.Contains("interruption", StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!IsDomainCompatible(card, context, contextVector))
        {
            return false;
        }

        return _cooldownTracker.IsAllowed(card, context, now);
    }

    private static bool IsDomainCompatible(
        FeedbackCard card,
        FeedbackContext context,
        IReadOnlyDictionary<string, double> contextVector)
    {
        var cardDomains = card.Vector.Keys
            .Where(key => key.StartsWith("domain.", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cardDomains.Length == 0)
        {
            return true;
        }

        if (cardDomains.Any(domain => IsDomainMatch(domain, context, contextVector)))
        {
            return true;
        }

        if (cardDomains.Length == 1
            && string.Equals(cardDomains[0], "domain.general", StringComparison.OrdinalIgnoreCase))
        {
            return IsGeneralFallbackCompatible(contextVector);
        }

        return false;
    }

    private static bool IsDomainMatch(
        string cardDomain,
        FeedbackContext context,
        IReadOnlyDictionary<string, double> contextVector)
    {
        if (string.Equals(cardDomain, "domain.confirmation", StringComparison.OrdinalIgnoreCase))
        {
            return context.NeedsConfirmation
                || contextVector.ContainsKey("domain.confirmation")
                || contextVector.ContainsKey("risk.confirmation");
        }

        if (string.Equals(cardDomain, "domain.interruption", StringComparison.OrdinalIgnoreCase))
        {
            return context.IsInterruptionFeedback
                || contextVector.ContainsKey("domain.interruption");
        }

        return contextVector.ContainsKey(cardDomain);
    }

    private static bool IsGeneralFallbackCompatible(IReadOnlyDictionary<string, double> contextVector)
    {
        return contextVector.ContainsKey("domain.general")
            || contextVector.ContainsKey("domain.local_tool")
            || contextVector.ContainsKey("domain.system")
            || contextVector.ContainsKey("domain.voice");
    }

    private void LogSuppressed(FeedbackContext context, string reason)
    {
        if (!_options.EnableDiagnosticsLogging)
        {
            return;
        }

        _logger.LogInformation(
            "Responsive feedback suppressed. CorrelationId: {CorrelationId}. TurnId: {TurnId}. Phase: {Phase}. Domain: {Domain}. Reason: {Reason}.",
            context.CorrelationId,
            context.TurnId,
            context.Phase,
            context.Domain,
            reason);
    }

    private static double Similarity(
        IReadOnlyDictionary<string, double> context,
        IReadOnlyDictionary<string, double> card)
    {
        double score = 0;
        foreach (var (key, cardWeight) in card)
        {
            if (context.TryGetValue(key, out var contextWeight))
            {
                score += contextWeight * cardWeight;
            }
        }

        return score;
    }
}
