namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackEmissionResult
{
    public bool Emitted { get; init; }

    public string? CardId { get; init; }

    public string? Text { get; init; }

    public FeedbackDomain Domain { get; init; } = FeedbackDomain.General;

    public string Reason { get; init; } = string.Empty;
}
