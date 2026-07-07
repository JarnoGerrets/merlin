using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;

namespace Merlin.Backend.Models;

public sealed class PendingConfirmation
{
    public string ConfirmationId { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string RequestedAlias { get; init; } = string.Empty;

    public string OriginalUserCommand { get; init; } = string.Empty;

    public string Intent { get; init; } = string.Empty;

    public string NormalizedCommand { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public IReadOnlyList<ApplicationCandidate> Candidates { get; init; } = [];

    public BrowserPagePendingConfirmation? BrowserPage { get; init; }
}
