namespace Merlin.Backend.Services.Feedback;

public sealed class FeedbackCard
{
    public string Id { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public FeedbackOutputMode OutputMode { get; init; } = FeedbackOutputMode.Speech;

    public FeedbackUrgency Urgency { get; init; } = FeedbackUrgency.Normal;

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public Dictionary<string, double> Vector { get; init; } = new();

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(60);

    public bool InterruptibleBeforePlayback { get; init; } = true;

    public bool IsReplayableSpeech { get; init; } = true;

    public int Priority { get; init; }

    public bool RequiresConfirmationContext { get; init; }

    public bool RequiresInterruptionContext { get; init; }

    public bool DisallowWhenFinalAnswerLikelyReady { get; init; } = true;
}
