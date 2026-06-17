using System.Collections.Concurrent;
using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class PendingInteractionService : IPendingInteractionService
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(45);
    private readonly ConcurrentDictionary<string, PendingInteraction> _pending = new();

    public int PendingCount
    {
        get
        {
            RemoveExpired();
            return _pending.Count;
        }
    }

    public PendingInteraction Create(
        string type,
        string prompt,
        IReadOnlyDictionary<string, string> context,
        string originalUserCommand)
    {
        RemoveExpired();
        foreach (var existing in _pending.Values.Where(item => string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase)))
        {
            _pending.TryRemove(existing.InteractionId, out _);
        }

        var createdAtUtc = DateTimeOffset.UtcNow;
        var interaction = new PendingInteraction
        {
            InteractionId = Guid.NewGuid().ToString("N"),
            Type = type,
            Prompt = prompt,
            Context = new Dictionary<string, string>(context, StringComparer.OrdinalIgnoreCase),
            OriginalUserCommand = originalUserCommand,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.Add(DefaultExpiry)
        };

        _pending[interaction.InteractionId] = interaction;
        return interaction;
    }

    public PendingInteraction? GetLatestPending(string? type = null)
    {
        RemoveExpired();
        return _pending.Values
            .Where(item => type is null || string.Equals(item.Type, type, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
    }

    public PendingInteraction? ConsumeLatestPending(string? type = null)
    {
        var pending = GetLatestPending(type);
        if (pending is null)
        {
            return null;
        }

        _pending.TryRemove(pending.InteractionId, out _);
        return pending;
    }

    private void RemoveExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var interaction in _pending.Values.Where(item => item.ExpiresAtUtc <= now))
        {
            _pending.TryRemove(interaction.InteractionId, out _);
        }
    }
}
