using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public interface IConfirmationService
{
    TimeSpan ExpiryDuration { get; }

    int PendingCount { get; }

    PendingConfirmation Create(
        string action,
        string target,
        string displayName,
        string requestedAlias,
        string originalUserCommand,
        string intent,
        string normalizedCommand,
        string toolName,
        IReadOnlyList<ApplicationCandidate>? candidates = null);

    PendingConfirmation? GetLatestPending();

    PendingConfirmation? ConsumeLatestPending();

    PendingConfirmation? ConsumeChoice(int choiceNumber);
}
