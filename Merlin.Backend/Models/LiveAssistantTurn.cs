namespace Merlin.Backend.Models;

public sealed class LiveAssistantTurn : IDisposable
{
    private int _disposed;

    public required string ConversationId { get; init; }

    public required string CorrelationId { get; init; }

    public required string AssistantTurnId { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required CancellationTokenSource CancellationTokenSource { get; init; }

    public CancellationToken CancellationToken => CancellationTokenSource.Token;

    public LiveAssistantTurnStatus Status { get; private set; } = LiveAssistantTurnStatus.Active;

    public LiveAssistantTurnCancelReason? CancelReason { get; private set; }

    public string? CorrectionText { get; private set; }

    public bool MarkCancelled(
        LiveAssistantTurnCancelReason reason,
        string? correctionText = null)
    {
        if (Status is LiveAssistantTurnStatus.Cancelled)
        {
            return false;
        }

        if (Status is LiveAssistantTurnStatus.Completed)
        {
            return false;
        }

        Status = LiveAssistantTurnStatus.Cancelled;
        CancelReason = reason;
        CorrectionText = string.IsNullOrWhiteSpace(correctionText) ? null : correctionText;
        CancellationTokenSource.Cancel();
        return true;
    }

    public bool MarkCompleted()
    {
        if (Status is not LiveAssistantTurnStatus.Active)
        {
            return false;
        }

        Status = LiveAssistantTurnStatus.Completed;
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        CancellationTokenSource.Dispose();
    }
}

public enum LiveAssistantTurnStatus
{
    Active,
    Cancelled,
    Completed
}

public enum LiveAssistantTurnCancelReason
{
    Unknown,
    UserHardStop,
    UserCorrection,
    SupersededByNewTurn,
    ClientDisconnected,
    Timeout,
    SystemShutdown
}
