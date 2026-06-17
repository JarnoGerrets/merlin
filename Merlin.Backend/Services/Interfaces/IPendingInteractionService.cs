using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IPendingInteractionService
{
    int PendingCount { get; }

    PendingInteraction Create(
        string type,
        string prompt,
        IReadOnlyDictionary<string, string> context,
        string originalUserCommand);

    PendingInteraction? GetLatestPending(string? type = null);

    PendingInteraction? ConsumeLatestPending(string? type = null);
}
