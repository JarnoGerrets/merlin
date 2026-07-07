using Merlin.Backend.Models;

namespace Merlin.Backend.Services.BrowserWorkspace.PageControl;

public sealed record BrowserPageActionResult
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }

    public string? ElementId { get; init; }

    public string? ElementText { get; init; }

    public string? ElementHref { get; init; }

    public int CandidateCount { get; init; }

    public PendingConfirmation? Confirmation { get; init; }
}
