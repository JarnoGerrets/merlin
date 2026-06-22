namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackSelection
{
    public FeedbackCard Card { get; init; } = default!;

    public double Score { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DateTimeOffset SelectedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
