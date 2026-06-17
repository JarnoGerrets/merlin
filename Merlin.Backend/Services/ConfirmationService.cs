using System.Collections.Concurrent;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class ConfirmationService : IConfirmationService
{
    private readonly ConcurrentDictionary<string, PendingConfirmation> _confirmations = new();

    public ConfirmationService()
        : this(TimeSpan.FromMinutes(2))
    {
    }

    public ConfirmationService(TimeSpan expiryDuration)
    {
        ExpiryDuration = expiryDuration;
    }

    public TimeSpan ExpiryDuration { get; }

    public int PendingCount
    {
        get
        {
            RemoveExpired();
            return _confirmations.Count;
        }
    }

    public PendingConfirmation Create(
        string action,
        string target,
        string displayName,
        string requestedAlias,
        string originalUserCommand,
        string intent,
        string normalizedCommand,
        string toolName,
        IReadOnlyList<ApplicationCandidate>? candidates = null)
    {
        RemoveExpired();

        var confirmation = new PendingConfirmation
        {
            ConfirmationId = Guid.NewGuid().ToString("N"),
            Action = action,
            Target = target,
            DisplayName = displayName,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ExpiryDuration),
            RequestedAlias = requestedAlias,
            OriginalUserCommand = originalUserCommand,
            Intent = intent,
            NormalizedCommand = normalizedCommand,
            ToolName = toolName,
            Candidates = candidates ?? []
        };

        _confirmations[confirmation.ConfirmationId] = confirmation;
        return confirmation;
    }

    public PendingConfirmation? GetLatestPending()
    {
        RemoveExpired();
        return _confirmations.Values
            .OrderByDescending(confirmation => confirmation.ExpiresAtUtc)
            .FirstOrDefault();
    }

    public PendingConfirmation? ConsumeLatestPending()
    {
        var pending = GetLatestPending();
        if (pending is null)
        {
            return null;
        }

        return _confirmations.TryRemove(pending.ConfirmationId, out var removed)
            ? removed
            : null;
    }

    public PendingConfirmation? SelectChoice(int choiceNumber)
    {
        var pending = GetLatestPending();
        if (pending is null || choiceNumber < 1 || choiceNumber > pending.Candidates.Count)
        {
            return null;
        }

        return SelectCandidate(pending, pending.Candidates[choiceNumber - 1]);
    }

    public PendingConfirmation? SelectCandidateName(string candidateName)
    {
        var normalizedCandidateName = NormalizeName(candidateName);
        var pending = GetLatestPending();
        if (pending is null || string.IsNullOrWhiteSpace(normalizedCandidateName))
        {
            return null;
        }

        var selected = pending.Candidates
            .FirstOrDefault(candidate => string.Equals(
                NormalizeName(candidate.DisplayName),
                normalizedCandidateName,
                StringComparison.OrdinalIgnoreCase));
        return selected is null
            ? null
            : SelectCandidate(pending, selected);
    }

    private PendingConfirmation? SelectCandidate(PendingConfirmation pending, ApplicationCandidate selected)
    {
        var updated = new PendingConfirmation
        {
            ConfirmationId = pending.ConfirmationId,
            Action = pending.Action,
            Target = selected.ExecutablePath,
            DisplayName = selected.DisplayName,
            ExpiresAtUtc = pending.ExpiresAtUtc,
            RequestedAlias = pending.RequestedAlias,
            OriginalUserCommand = pending.OriginalUserCommand,
            Intent = pending.Intent,
            NormalizedCommand = pending.NormalizedCommand,
            ToolName = pending.ToolName,
            Candidates = [selected]
        };

        return _confirmations.TryUpdate(pending.ConfirmationId, updated, pending)
            ? updated
            : null;
    }

    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var confirmation in _confirmations.Values)
        {
            if (confirmation.ExpiresAtUtc <= now)
            {
                _confirmations.TryRemove(confirmation.ConfirmationId, out _);
            }
        }
    }

    private static string NormalizeName(string value)
    {
        return string.Join(
            ' ',
            value.Trim()
                .TrimEnd('.', '!', '?', ';', ':', ',')
                .ToLowerInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
