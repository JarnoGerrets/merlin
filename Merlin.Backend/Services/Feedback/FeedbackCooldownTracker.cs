using System.Collections.Concurrent;
using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackCooldownTracker : IFeedbackCooldownTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastCardUse = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastTextUse = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _immediateCountsByTurn = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _interruptionCountsByTurn = new(StringComparer.OrdinalIgnoreCase);
    private readonly ResponsiveFeedbackOptions _options;
    private readonly object _globalSync = new();
    private DateTimeOffset? _lastGlobalUseUtc;

    public FeedbackCooldownTracker(IOptions<ResponsiveFeedbackOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAllowed(FeedbackCard card, FeedbackContext context, DateTimeOffset now)
    {
        PruneOldEntries(now);

        var turnKey = TurnKey(context);
        if (!context.IsInterruptionFeedback
            && _immediateCountsByTurn.TryGetValue(turnKey, out var immediateCount)
            && immediateCount >= Math.Max(0, _options.MaxImmediateFeedbackPerTurn))
        {
            return false;
        }

        if (context.IsInterruptionFeedback
            && _interruptionCountsByTurn.TryGetValue(turnKey, out var interruptionCount)
            && interruptionCount >= Math.Max(0, _options.MaxInterruptionBridgeFeedbackPerInterruption))
        {
            return false;
        }

        lock (_globalSync)
        {
            if (_lastGlobalUseUtc is not null
                && now - _lastGlobalUseUtc.Value < TimeSpan.FromMilliseconds(Math.Max(0, _options.GlobalCooldownMs)))
            {
                return false;
            }
        }

        if (_lastCardUse.TryGetValue(card.Id, out var lastCardUse)
            && now - lastCardUse < EffectiveCooldown(card))
        {
            return false;
        }

        if (_lastTextUse.TryGetValue(card.Text, out var lastTextUse)
            && now - lastTextUse < TimeSpan.FromSeconds(Math.Max(0, _options.SameTextCooldownSeconds)))
        {
            return false;
        }

        return true;
    }

    public void MarkUsed(FeedbackCard card, FeedbackContext context, DateTimeOffset now)
    {
        _lastCardUse[card.Id] = now;
        _lastTextUse[card.Text] = now;
        lock (_globalSync)
        {
            _lastGlobalUseUtc = now;
        }

        var turnKey = TurnKey(context);
        if (context.IsInterruptionFeedback)
        {
            _interruptionCountsByTurn.AddOrUpdate(turnKey, 1, (_, count) => count + 1);
            return;
        }

        _immediateCountsByTurn.AddOrUpdate(turnKey, 1, (_, count) => count + 1);
    }

    private TimeSpan EffectiveCooldown(FeedbackCard card)
    {
        if (card.Cooldown > TimeSpan.Zero)
        {
            return card.Cooldown;
        }

        return TimeSpan.FromSeconds(Math.Max(0, _options.DefaultCardCooldownSeconds));
    }

    private static string TurnKey(FeedbackContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.TurnId))
        {
            return context.TurnId;
        }

        return string.IsNullOrWhiteSpace(context.CorrelationId)
            ? "unknown"
            : context.CorrelationId;
    }

    private void PruneOldEntries(DateTimeOffset now)
    {
        var cutoff = now - TimeSpan.FromMinutes(30);
        foreach (var (key, timestamp) in _lastCardUse)
        {
            if (timestamp < cutoff)
            {
                _lastCardUse.TryRemove(key, out _);
            }
        }

        foreach (var (key, timestamp) in _lastTextUse)
        {
            if (timestamp < cutoff)
            {
                _lastTextUse.TryRemove(key, out _);
            }
        }

        if (_immediateCountsByTurn.Count > 2048)
        {
            _immediateCountsByTurn.Clear();
        }

        if (_interruptionCountsByTurn.Count > 2048)
        {
            _interruptionCountsByTurn.Clear();
        }
    }
}
