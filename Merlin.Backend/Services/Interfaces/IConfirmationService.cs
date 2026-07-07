using Merlin.Backend.Models;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

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
        IReadOnlyList<ApplicationCandidate>? candidates = null,
        BrowserPagePendingConfirmation? browserPage = null);

    PendingConfirmation? GetLatestPending();

    PendingConfirmation? ConsumeLatestPending();

    PendingConfirmation? SelectChoice(int choiceNumber);

    PendingConfirmation? SelectCandidateName(string candidateName);
}
