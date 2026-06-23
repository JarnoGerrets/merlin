using Merlin.Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Services.InterruptionIntelligence;

public sealed class ConversationFocusManager : IConversationFocusManager
{
    private readonly object _gate = new();
    private readonly InterruptionHandlingOptions _options;
    private ConversationThreadState? _currentState;

    public ConversationFocusManager(IOptions<InterruptionHandlingOptions> options)
    {
        _options = options.Value;
    }

    public ConversationThreadState? GetCurrentState()
    {
        lock (_gate)
        {
            return _currentState;
        }
    }

    public ConversationThreadState StartMainTurn(
        string threadId,
        string turnId,
        string originalUserQuestion,
        string? currentAnswerPurpose = null,
        SpokenAnswerState? spokenAnswerState = null)
    {
        if (string.IsNullOrWhiteSpace(threadId))
        {
            throw new ArgumentException("Thread id is required.", nameof(threadId));
        }

        if (string.IsNullOrWhiteSpace(turnId))
        {
            throw new ArgumentException("Turn id is required.", nameof(turnId));
        }

        var state = new ConversationThreadState
        {
            ThreadId = threadId.Trim(),
            ActiveTurnId = turnId.Trim(),
            OriginalUserQuestion = originalUserQuestion?.Trim() ?? string.Empty,
            CurrentAnswerPurpose = NormalizeOptional(currentAnswerPurpose),
            ActiveSpokenAnswer = spokenAnswerState,
            FollowUpQueue = Array.Empty<QueuedFollowUp>(),
            IsAssistantSpeaking = false,
            IsInterrupted = false,
            IsRecomposing = false,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        lock (_gate)
        {
            _currentState = state;
            return state;
        }
    }

    public ConversationThreadState UpdateSpokenAnswer(SpokenAnswerState spokenAnswerState)
    {
        ArgumentNullException.ThrowIfNull(spokenAnswerState);
        return Mutate(state => Clone(
            state,
            activeTurnId: string.IsNullOrWhiteSpace(state.ActiveTurnId) ? spokenAnswerState.TurnId : state.ActiveTurnId,
            activeSpokenAnswer: spokenAnswerState));
    }

    public ConversationThreadState SetAssistantSpeaking(bool isSpeaking)
    {
        return Mutate(state => Clone(state, isAssistantSpeaking: isSpeaking));
    }

    public ConversationFocusAction ApplyInterruptionDecision(
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(decision);

        lock (_gate)
        {
            var state = RequireState();
            return decision.Strategy switch
            {
                ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse =>
                    ContinueOrIgnore(state, ConversationFocusActionType.ContinueMainAnswer, decision, "Interruption can continue without response."),
                ConversationalInterruptionHandlingStrategy.IgnoreAndContinue =>
                    ContinueOrIgnore(state, ConversationFocusActionType.IgnoreAndContinue, decision, "Interruption ignored; continue main answer."),
                ConversationalInterruptionHandlingStrategy.StopPlayback =>
                    StopPlayback(state, decision),
                ConversationalInterruptionHandlingStrategy.CancelAndRedirect =>
                    CancelAndRedirect(state, decision),
                ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint =>
                    ClarifyThenRecompose(state, decision),
                ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint =>
                    Recompose(state, decision),
                ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent =>
                    QueueFollowUp(state, candidate, decision),
                ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption =>
                    AskUserToClarify(state, decision, "Interruption decision requires user clarification."),
                _ => AskUserToClarify(state, decision, "Unknown interruption handling strategy.")
            };
        }
    }

    public ConversationThreadState MarkRecomposing(bool isRecomposing)
    {
        return Mutate(state => Clone(state, isRecomposing: isRecomposing));
    }

    public ConversationThreadState CompleteCurrentTurn()
    {
        lock (_gate)
        {
            var state = RequireState();
            _currentState = null;
            return Clone(
                state,
                isAssistantSpeaking: false,
                isInterrupted: false,
                isRecomposing: false);
        }
    }

    public ConversationThreadState StopCurrentTurn(string reason)
    {
        return Mutate(state => Clone(
            state,
            isAssistantSpeaking: false,
            isInterrupted: true,
            isRecomposing: false));
    }

    public void Clear()
    {
        lock (_gate)
        {
            _currentState = null;
        }
    }

    private ConversationFocusAction ContinueOrIgnore(
        ConversationThreadState state,
        ConversationFocusActionType actionType,
        ConversationalInterruptionDecision decision,
        string reason)
    {
        _currentState = Clone(state, isInterrupted: false, isRecomposing: false);
        return Action(state, actionType, reason);
    }

    private ConversationFocusAction StopPlayback(
        ConversationThreadState state,
        ConversationalInterruptionDecision decision)
    {
        _currentState = Clone(state, isAssistantSpeaking: false, isInterrupted: true, isRecomposing: false);
        return Action(
            state,
            ConversationFocusActionType.StopCurrentTurn,
            "Stop playback interruption accepted.",
            shouldPausePlayback: true,
            shouldCancelPlayback: true,
            shouldCancelOriginalTurn: true);
    }

    private ConversationFocusAction CancelAndRedirect(
        ConversationThreadState state,
        ConversationalInterruptionDecision decision)
    {
        if (string.IsNullOrWhiteSpace(decision.RewrittenUserRequest))
        {
            _currentState = Clone(state, isInterrupted: true, isRecomposing: false);
            return Action(
                state,
                ConversationFocusActionType.AskUserToClarifyInterruption,
                "Correction or redirect did not include a replacement request.",
                shouldPausePlayback: true,
                shouldCancelOriginalTurn: false,
                requiresBridgeFeedback: true);
        }

        _currentState = Clone(state, isAssistantSpeaking: false, isInterrupted: true, isRecomposing: false);
        return Action(
            state,
            ConversationFocusActionType.CancelAndReplaceMainTurn,
            "Correction or redirect replaces the main turn.",
            rewrittenRequest: decision.RewrittenUserRequest.Trim(),
            shouldPausePlayback: true,
            shouldCancelPlayback: true,
            shouldCancelOriginalTurn: true,
            requiresBridgeFeedback: decision.RequiresBridgeFeedback);
    }

    private ConversationFocusAction ClarifyThenRecompose(
        ConversationThreadState state,
        ConversationalInterruptionDecision decision)
    {
        _currentState = Clone(state, isInterrupted: true, isRecomposing: true);
        return Action(
            state,
            ConversationFocusActionType.ClarifyThenRecomposeMainAnswer,
            "Clarification should be answered before recomposing the main answer.",
            shouldPausePlayback: true,
            shouldCancelPlayback: true,
            shouldCreateCheckpoint: true,
            shouldDiscardPartialSentence: true,
            requiresBridgeFeedback: decision.RequiresBridgeFeedback,
            requiresClarification: true,
            requiresRecomposition: true);
    }

    private ConversationFocusAction Recompose(
        ConversationThreadState state,
        ConversationalInterruptionDecision decision)
    {
        _currentState = Clone(state, isInterrupted: true, isRecomposing: true);
        return Action(
            state,
            ConversationFocusActionType.RecomposeMainAnswer,
            "Interruption adds context; recompose from checkpoint.",
            shouldPausePlayback: true,
            shouldCancelPlayback: true,
            shouldCreateCheckpoint: true,
            shouldDiscardPartialSentence: true,
            requiresBridgeFeedback: true,
            requiresRecomposition: true);
    }

    private ConversationFocusAction QueueFollowUp(
        ConversationThreadState state,
        ConversationalInterruptionCandidate candidate,
        ConversationalInterruptionDecision decision)
    {
        var queue = state.FollowUpQueue.ToList();
        if (queue.Count >= Math.Max(0, _options.MaxQueuedFollowUps))
        {
            _currentState = Clone(state, isInterrupted: true, isRecomposing: false);
            return Action(
                state,
                ConversationFocusActionType.AskUserToClarifyInterruption,
                "Follow-up queue is full; cannot queue another interruption.",
                shouldPausePlayback: true,
                requiresBridgeFeedback: true);
        }

        var followUp = new QueuedFollowUp
        {
            Id = Guid.NewGuid().ToString("N"),
            UserText = candidate.Transcript?.Trim() ?? string.Empty,
            RelatedTurnId = state.ActiveTurnId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            RequiresDeepInfra = true,
            Reason = decision.Reason,
            CurrentTopicLabel = state.ActiveSpokenAnswer?.CurrentTopicLabel
        };
        queue.Add(followUp);
        _currentState = Clone(
            state,
            followUpQueue: queue,
            isInterrupted: true,
            isRecomposing: false);

        return Action(
            state,
            ConversationFocusActionType.QueueFollowUpAfterCurrent,
            "Follow-up queued after the current turn.",
            queuedFollowUpId: followUp.Id,
            shouldPausePlayback: decision.PausePlayback,
            shouldCreateCheckpoint: decision.DiscardCurrentPartialSentence,
            shouldDiscardPartialSentence: decision.DiscardCurrentPartialSentence,
            requiresBridgeFeedback: true);
    }

    private ConversationFocusAction AskUserToClarify(
        ConversationThreadState state,
        ConversationalInterruptionDecision decision,
        string reason)
    {
        _currentState = Clone(state, isInterrupted: true, isRecomposing: false);
        return Action(
            state,
            ConversationFocusActionType.AskUserToClarifyInterruption,
            reason,
            shouldPausePlayback: true,
            requiresBridgeFeedback: true);
    }

    private ConversationThreadState Mutate(Func<ConversationThreadState, ConversationThreadState> mutation)
    {
        lock (_gate)
        {
            var updated = mutation(RequireState());
            _currentState = updated;
            return updated;
        }
    }

    private ConversationThreadState RequireState()
    {
        return _currentState
            ?? throw new InvalidOperationException("No active conversation thread state exists.");
    }

    private static ConversationFocusAction Action(
        ConversationThreadState state,
        ConversationFocusActionType type,
        string reason,
        string? rewrittenRequest = null,
        string? queuedFollowUpId = null,
        bool shouldPausePlayback = false,
        bool shouldCancelPlayback = false,
        bool shouldCancelOriginalTurn = false,
        bool shouldCreateCheckpoint = false,
        bool shouldDiscardPartialSentence = false,
        bool requiresBridgeFeedback = false,
        bool requiresClarification = false,
        bool requiresRecomposition = false)
    {
        return new ConversationFocusAction
        {
            Type = type,
            ThreadId = state.ThreadId,
            ActiveTurnId = state.ActiveTurnId,
            RewrittenRequest = rewrittenRequest,
            QueuedFollowUpId = queuedFollowUpId,
            ShouldPausePlayback = shouldPausePlayback,
            ShouldCancelPlayback = shouldCancelPlayback,
            ShouldCancelOriginalTurn = shouldCancelOriginalTurn,
            ShouldCreateCheckpoint = shouldCreateCheckpoint,
            ShouldDiscardPartialSentence = shouldDiscardPartialSentence,
            RequiresBridgeFeedback = requiresBridgeFeedback,
            RequiresClarification = requiresClarification,
            RequiresRecomposition = requiresRecomposition,
            Reason = reason
        };
    }

    private static ConversationThreadState Clone(
        ConversationThreadState state,
        string? activeTurnId = null,
        SpokenAnswerState? activeSpokenAnswer = null,
        IReadOnlyList<QueuedFollowUp>? followUpQueue = null,
        bool? isAssistantSpeaking = null,
        bool? isInterrupted = null,
        bool? isRecomposing = null)
    {
        return new ConversationThreadState
        {
            ThreadId = state.ThreadId,
            ActiveTurnId = activeTurnId ?? state.ActiveTurnId,
            OriginalUserQuestion = state.OriginalUserQuestion,
            CurrentAnswerPurpose = state.CurrentAnswerPurpose,
            ActiveSpokenAnswer = activeSpokenAnswer ?? state.ActiveSpokenAnswer,
            FollowUpQueue = (followUpQueue ?? state.FollowUpQueue).ToArray(),
            IsAssistantSpeaking = isAssistantSpeaking ?? state.IsAssistantSpeaking,
            IsInterrupted = isInterrupted ?? state.IsInterrupted,
            IsRecomposing = isRecomposing ?? state.IsRecomposing,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
