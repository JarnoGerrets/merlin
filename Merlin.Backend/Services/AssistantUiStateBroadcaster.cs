using Merlin.Backend.Models;

namespace Merlin.Backend.Services;

public sealed class AssistantUiStateBroadcaster
{
    private const int DefaultCoalescingDelayMs = 75;

    private readonly ILogger<AssistantUiStateBroadcaster> _logger;
    private readonly object _syncRoot = new();
    private long _sequence;
    private AssistantUiStateEvent? _latestEmitted;
    private PendingCoalescedState? _pendingCoalesced;
    private long _coalescedGeneration;
    private string _overlayState = "none";

    public AssistantUiStateBroadcaster(ILogger<AssistantUiStateBroadcaster> logger)
    {
        _logger = logger;
    }

    public event Func<AssistantUiStateEvent, string, CancellationToken, Task>? StateChanged;

    public Task EmitAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken = default) =>
        EmitImmediateAsync(uiState, source, cancellationToken);

    public Task EmitImmediateAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken = default) =>
        EmitCoreAsync(uiState, source, "immediate", clearPendingCoalesced: true, cancellationToken);

    public Task EmitTerminalAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken = default) =>
        EmitCoreAsync(uiState, source, "terminal", clearPendingCoalesced: true, cancellationToken);

    public Task RequestCoalescedStateAsync(
        AssistantUiStateEvent uiState,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }

        long generation;
        PendingCoalescedState? previous;
        lock (_syncRoot)
        {
            previous = _pendingCoalesced;
            generation = ++_coalescedGeneration;
            _pendingCoalesced = new PendingCoalescedState(uiState, source, generation);
        }

        if (previous is not null)
        {
            _logger.LogInformation(
                "assistant_ui_state_coalesced_replaced PreviousReason: {PreviousReason}. NewReason: {NewReason}. PreviousBaseState: {PreviousBaseState}. NewBaseState: {NewBaseState}.",
                previous.UiState.Reason,
                uiState.Reason,
                previous.UiState.BaseState,
                uiState.BaseState);
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(DefaultCoalescingDelayMs, cancellationToken);
                    PendingCoalescedState? pending;
                    lock (_syncRoot)
                    {
                        if (_pendingCoalesced?.Generation != generation)
                        {
                            return;
                        }

                        pending = _pendingCoalesced;
                        _pendingCoalesced = null;
                    }

                    if (pending is not null)
                    {
                        await EmitCoreAsync(
                            pending.UiState,
                            pending.Source,
                            "coalesced",
                            clearPendingCoalesced: false,
                            cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            },
            CancellationToken.None);

        return Task.CompletedTask;
    }

    public void ClearPendingCoalesced()
    {
        lock (_syncRoot)
        {
            _pendingCoalesced = null;
            _coalescedGeneration++;
        }
    }

    private async Task EmitCoreAsync(
        AssistantUiStateEvent uiState,
        string source,
        string timingClass,
        bool clearPendingCoalesced,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        AssistantUiStateEvent sequenced;
        lock (_syncRoot)
        {
            if (clearPendingCoalesced)
            {
                _pendingCoalesced = null;
                _coalescedGeneration++;
            }

            _overlayState = uiState.OverlayState;
            sequenced = uiState with
            {
                Sequence = Interlocked.Increment(ref _sequence),
                OverlayState = _overlayState,
                TimestampUtc = uiState.TimestampUtc.ToUniversalTime()
            };

            if (IsDuplicate(_latestEmitted, sequenced))
            {
                _logger.LogInformation(
                    "assistant_ui_state_suppressed_duplicate BaseState: {BaseState}. OverlayState: {OverlayState}. Reason: {Reason}. CorrelationId: {CorrelationId}. TurnId: {TurnId}. SpeechItemType: {SpeechItemType}. AudiblePlaybackActive: {AudiblePlaybackActive}. InterruptionState: {InterruptionState}. Source: {Source}. TimingClass: {TimingClass}.",
                    sequenced.BaseState,
                    sequenced.OverlayState,
                    sequenced.Reason,
                    sequenced.CorrelationId,
                    sequenced.TurnId,
                    sequenced.SpeechItemType,
                    sequenced.AudiblePlaybackActive,
                    sequenced.InterruptionState,
                    source,
                    timingClass);
                return;
            }

            _latestEmitted = sequenced;
        }

        LogEmitted(sequenced, source, timingClass);

        var handlers = StateChanged;
        if (handlers is null)
        {
            return;
        }

        foreach (Func<AssistantUiStateEvent, string, CancellationToken, Task> handler in handlers.GetInvocationList())
        {
            await handler(sequenced, source, cancellationToken);
        }
    }

    private void LogEmitted(AssistantUiStateEvent uiState, string source, string timingClass)
    {
        _logger.LogInformation(
            "assistant_ui_state_emitted Sequence: {Sequence}. BaseState: {BaseState}. OverlayState: {OverlayState}. Reason: {Reason}. CorrelationId: {CorrelationId}. TurnId: {TurnId}. SpeechItemType: {SpeechItemType}. AudiblePlaybackActive: {AudiblePlaybackActive}. InterruptionState: {InterruptionState}. TimestampUtc: {TimestampUtc}. Source: {Source}. TimingClass: {TimingClass}.",
            uiState.Sequence,
            uiState.BaseState,
            uiState.OverlayState,
            uiState.Reason,
            uiState.CorrelationId,
            uiState.TurnId,
            uiState.SpeechItemType,
            uiState.AudiblePlaybackActive,
            uiState.InterruptionState,
            uiState.TimestampUtc,
            source,
            timingClass);
    }

    private static bool IsDuplicate(AssistantUiStateEvent? previous, AssistantUiStateEvent current)
    {
        return previous is not null
            && string.Equals(previous.BaseState, current.BaseState, StringComparison.Ordinal)
            && string.Equals(previous.OverlayState, current.OverlayState, StringComparison.Ordinal)
            && string.Equals(previous.Reason, current.Reason, StringComparison.Ordinal)
            && string.Equals(previous.CorrelationId, current.CorrelationId, StringComparison.Ordinal)
            && string.Equals(previous.TurnId, current.TurnId, StringComparison.Ordinal)
            && string.Equals(previous.SpeechItemType, current.SpeechItemType, StringComparison.Ordinal)
            && previous.AudiblePlaybackActive == current.AudiblePlaybackActive
            && string.Equals(previous.InterruptionState, current.InterruptionState, StringComparison.Ordinal);
    }

    private sealed record PendingCoalescedState(
        AssistantUiStateEvent UiState,
        string Source,
        long Generation);
}
