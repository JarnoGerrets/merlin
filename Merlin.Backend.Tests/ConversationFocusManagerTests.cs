using Merlin.Backend.Configuration;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConversationFocusManagerTests
{
    [Fact]
    public void StartMainTurn_CreatesCurrentState()
    {
        var manager = CreateManager();

        var state = manager.StartMainTurn("thread-1", "turn-1", "Why does pool water look blue?");

        Assert.Same(state, manager.GetCurrentState());
        Assert.Equal("thread-1", state.ThreadId);
        Assert.Equal("turn-1", state.ActiveTurnId);
        Assert.Equal("Why does pool water look blue?", state.OriginalUserQuestion);
        Assert.False(state.IsAssistantSpeaking);
        Assert.False(state.IsInterrupted);
        Assert.False(state.IsRecomposing);
        Assert.Empty(state.FollowUpQueue);
    }

    [Fact]
    public void UpdateSpokenAnswer_UpdatesCurrentState()
    {
        var manager = CreateStartedManager();
        var spoken = CreateSpokenAnswerState("Pool water can look blue...");

        var state = manager.UpdateSpokenAnswer(spoken);

        Assert.Same(spoken, state.ActiveSpokenAnswer);
        Assert.Equal("Pool water can look blue...", state.ActiveSpokenAnswer?.SpokenSoFar);
    }

    [Fact]
    public void UpdateSpokenAnswer_WhenNoCurrentState_Throws()
    {
        var manager = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.UpdateSpokenAnswer(CreateSpokenAnswerState("x")));
    }

    [Fact]
    public void SetAssistantSpeaking_UpdatesFlag()
    {
        var manager = CreateStartedManager();

        var state = manager.SetAssistantSpeaking(true);

        Assert.True(state.IsAssistantSpeaking);
    }

    [Fact]
    public void SetAssistantSpeaking_WhenNoCurrentState_Throws()
    {
        var manager = CreateManager();

        Assert.Throws<InvalidOperationException>(() => manager.SetAssistantSpeaking(true));
    }

    [Fact]
    public void ApplyInterruptionDecision_BackchannelContinuesMainAnswer()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("yeah"),
            CreateDecision(
                ConversationalInterruptionType.Backchannel,
                ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse));

        Assert.Equal(ConversationFocusActionType.ContinueMainAnswer, action.Type);
        Assert.False(action.ShouldCancelPlayback);
        Assert.False(action.ShouldCreateCheckpoint);
        Assert.False(action.RequiresRecomposition);
        Assert.False(manager.GetCurrentState()!.IsInterrupted);
        Assert.False(manager.GetCurrentState()!.IsRecomposing);
    }

    [Fact]
    public void ApplyInterruptionDecision_NoiseIgnoresAndContinues()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate(""),
            CreateDecision(
                ConversationalInterruptionType.NoiseOrFalsePositive,
                ConversationalInterruptionHandlingStrategy.IgnoreAndContinue));

        Assert.Equal(ConversationFocusActionType.IgnoreAndContinue, action.Type);
        Assert.False(action.ShouldCancelPlayback);
        Assert.False(action.ShouldCreateCheckpoint);
        Assert.False(action.RequiresBridgeFeedback);
    }

    [Fact]
    public void ApplyInterruptionDecision_StopCreatesStopCurrentTurnAction()
    {
        var manager = CreateStartedManager();
        manager.SetAssistantSpeaking(true);

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("stop"),
            CreateDecision(
                ConversationalInterruptionType.StopRequest,
                ConversationalInterruptionHandlingStrategy.StopPlayback));

        Assert.Equal(ConversationFocusActionType.StopCurrentTurn, action.Type);
        Assert.True(action.ShouldCancelPlayback);
        Assert.True(action.ShouldCancelOriginalTurn);
        Assert.False(manager.GetCurrentState()!.IsAssistantSpeaking);
        Assert.True(manager.GetCurrentState()!.IsInterrupted);
        Assert.False(manager.GetCurrentState()!.IsRecomposing);
    }

    [Fact]
    public void ApplyInterruptionDecision_CorrectionCreatesCancelAndReplaceAction()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("no i meant what is the meaning of a wife"),
            CreateDecision(
                ConversationalInterruptionType.Correction,
                ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
                rewrittenUserRequest: "what is the meaning of a wife",
                requiresBridgeFeedback: true));

        Assert.Equal(ConversationFocusActionType.CancelAndReplaceMainTurn, action.Type);
        Assert.Equal("what is the meaning of a wife", action.RewrittenRequest);
        Assert.True(action.ShouldCancelPlayback);
        Assert.True(action.ShouldCancelOriginalTurn);
        Assert.True(action.RequiresBridgeFeedback);
        Assert.False(action.RequiresRecomposition);
    }

    [Fact]
    public void ApplyInterruptionDecision_EmptyCorrectionAsksForClarification()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("actually"),
            CreateDecision(
                ConversationalInterruptionType.Correction,
                ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
                rewrittenUserRequest: ""));

        Assert.Equal(ConversationFocusActionType.AskUserToClarifyInterruption, action.Type);
        Assert.Null(action.RewrittenRequest);
        Assert.True(action.RequiresBridgeFeedback);
        Assert.False(action.ShouldCancelOriginalTurn);
    }

    [Fact]
    public void ApplyInterruptionDecision_ClarificationCreatesClarifyThenRecomposeAction()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("what do you mean by liner"),
            CreateDecision(
                ConversationalInterruptionType.ClarificationQuestion,
                ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
                requiresClarification: true,
                requiresRecomposition: true));

        Assert.Equal(ConversationFocusActionType.ClarifyThenRecomposeMainAnswer, action.Type);
        Assert.True(action.ShouldCreateCheckpoint);
        Assert.True(action.ShouldDiscardPartialSentence);
        Assert.True(action.RequiresClarification);
        Assert.True(action.RequiresRecomposition);
        Assert.False(action.ShouldCancelOriginalTurn);
        Assert.True(manager.GetCurrentState()!.IsRecomposing);
    }

    [Fact]
    public void ApplyInterruptionDecision_SideCommentCreatesRecomposeAction()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("well yeah but sunlight too"),
            CreateDecision(
                ConversationalInterruptionType.SideComment,
                ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint,
                requiresBridgeFeedback: true,
                requiresRecomposition: true));

        Assert.Equal(ConversationFocusActionType.RecomposeMainAnswer, action.Type);
        Assert.True(action.RequiresBridgeFeedback);
        Assert.False(action.RequiresClarification);
        Assert.True(action.RequiresRecomposition);
        Assert.True(action.ShouldCreateCheckpoint);
    }

    [Fact]
    public void ApplyInterruptionDecision_QueueFollowUpAddsItem()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("can you explain sunlight after this"),
            CreateDecision(
                ConversationalInterruptionType.RelatedFollowUpQuestion,
                ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent,
                queueAfterCurrentTurn: true,
                requiresBridgeFeedback: true));

        var state = manager.GetCurrentState();
        Assert.Equal(ConversationFocusActionType.QueueFollowUpAfterCurrent, action.Type);
        Assert.False(string.IsNullOrWhiteSpace(action.QueuedFollowUpId));
        Assert.NotNull(state);
        var followUp = Assert.Single(state!.FollowUpQueue);
        Assert.Equal("can you explain sunlight after this", followUp.UserText);
        Assert.Equal(action.QueuedFollowUpId, followUp.Id);
        Assert.True(action.RequiresBridgeFeedback);
        Assert.False(action.ShouldCancelOriginalTurn);
    }

    [Fact]
    public void ApplyInterruptionDecision_WhenQueueFull_AsksForClarification()
    {
        var manager = CreateStartedManager(maxQueuedFollowUps: 1);
        var decision = CreateDecision(
            ConversationalInterruptionType.RelatedFollowUpQuestion,
            ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent,
            queueAfterCurrentTurn: true,
            requiresBridgeFeedback: true);
        manager.ApplyInterruptionDecision(CreateCandidate("after this explain sunlight"), decision);

        var second = manager.ApplyInterruptionDecision(CreateCandidate("after this explain depth"), decision);

        Assert.Equal(ConversationFocusActionType.AskUserToClarifyInterruption, second.Type);
        Assert.Contains("queue", second.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full", second.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Single(manager.GetCurrentState()!.FollowUpQueue);
    }

    [Fact]
    public void ApplyInterruptionDecision_UnknownAsksUserToClarify()
    {
        var manager = CreateStartedManager();

        var action = manager.ApplyInterruptionDecision(
            CreateCandidate("something"),
            CreateDecision(
                ConversationalInterruptionType.Unknown,
                ConversationalInterruptionHandlingStrategy.Unknown));

        Assert.Equal(ConversationFocusActionType.AskUserToClarifyInterruption, action.Type);
        Assert.True(action.RequiresBridgeFeedback);
    }

    [Fact]
    public void Clear_RemovesCurrentState()
    {
        var manager = CreateStartedManager();

        manager.Clear();

        Assert.Null(manager.GetCurrentState());
    }

    [Fact]
    public void CompleteCurrentTurn_ClearsActiveState()
    {
        var manager = CreateStartedManager();

        var completed = manager.CompleteCurrentTurn();

        Assert.Equal("turn-1", completed.ActiveTurnId);
        Assert.False(completed.IsAssistantSpeaking);
        Assert.False(completed.IsInterrupted);
        Assert.False(completed.IsRecomposing);
        Assert.Null(manager.GetCurrentState());
    }

    private static ConversationFocusManager CreateStartedManager(int maxQueuedFollowUps = 3)
    {
        var manager = CreateManager(maxQueuedFollowUps);
        manager.StartMainTurn("thread-1", "turn-1", "Why does pool water look blue?");
        return manager;
    }

    private static ConversationFocusManager CreateManager(int maxQueuedFollowUps = 3)
    {
        return new ConversationFocusManager(Options.Create(new InterruptionHandlingOptions
        {
            MaxQueuedFollowUps = maxQueuedFollowUps
        }));
    }

    private static SpokenAnswerState CreateSpokenAnswerState(string spokenSoFar)
    {
        return new SpokenAnswerState
        {
            TurnId = "turn-1",
            CorrelationId = "correlation-1",
            OriginalUserQuestion = "Why does pool water look blue?",
            SpokenSoFar = spokenSoFar,
            CurrentTopicLabel = "pool color",
            CanRecompose = true
        };
    }

    private static ConversationalInterruptionCandidate CreateCandidate(string transcript)
    {
        return new ConversationalInterruptionCandidate
        {
            CorrelationId = "correlation-1",
            ActiveTurnId = "turn-1",
            Transcript = transcript,
            TranscriptConfidence = 0.9,
            AssistantWasSpeaking = true,
            IsLikelyUserSpeech = true
        };
    }

    private static ConversationalInterruptionDecision CreateDecision(
        ConversationalInterruptionType type,
        ConversationalInterruptionHandlingStrategy strategy,
        string? rewrittenUserRequest = null,
        bool requiresBridgeFeedback = false,
        bool requiresClarification = false,
        bool requiresRecomposition = false,
        bool queueAfterCurrentTurn = false)
    {
        return new ConversationalInterruptionDecision
        {
            Type = type,
            Strategy = strategy,
            PausePlayback = strategy is not ConversationalInterruptionHandlingStrategy.IgnoreAndContinue
                and not ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse,
            CancelOriginalTurn = strategy is ConversationalInterruptionHandlingStrategy.StopPlayback
                or ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
            DiscardCurrentPartialSentence = true,
            RequiresBridgeFeedback = requiresBridgeFeedback,
            RequiresDeepInfraClarification = requiresClarification,
            RequiresContinuationRecomposition = requiresRecomposition,
            QueueAfterCurrentTurn = queueAfterCurrentTurn,
            RewrittenUserRequest = rewrittenUserRequest,
            Reason = "test decision"
        };
    }
}
