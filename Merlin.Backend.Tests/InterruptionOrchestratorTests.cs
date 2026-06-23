using Merlin.Backend.Configuration;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class InterruptionOrchestratorTests
{
    [Fact]
    public async Task HandleInterruptionAsync_WhenDisabled_ReturnsIgnoredWithNoSideEffects()
    {
        var fixture = CreateFixture(enabled: false, Decision(ConversationalInterruptionHandlingStrategy.StopPlayback));

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("stop"));

        Assert.Equal(InterruptionHandlingResultType.Ignored, result.Type);
        Assert.Equal(0, fixture.Classifier.CallCount);
        AssertNoSideEffects(fixture);
    }

    [Fact]
    public async Task HandleInterruptionAsync_Noise_IsIgnored()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.IgnoreAndContinue,
                ConversationalInterruptionType.NoiseOrFalsePositive));
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate(""));

        Assert.Equal(InterruptionHandlingResultType.Ignored, result.Type);
        AssertNoSideEffects(fixture);
    }

    [Fact]
    public async Task HandleInterruptionAsync_Backchannel_Continues()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse,
                ConversationalInterruptionType.Backchannel));
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("yeah"));

        Assert.Equal(InterruptionHandlingResultType.Continued, result.Type);
        AssertNoSideEffects(fixture);
    }

    [Fact]
    public async Task HandleInterruptionAsync_Stop_StopsPlaybackOnly()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.StopPlayback,
                ConversationalInterruptionType.StopRequest,
                cancelOriginalTurn: true));
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("stop"));

        Assert.Equal(InterruptionHandlingResultType.Stopped, result.Type);
        Assert.Equal(1, fixture.Playback.StopCount);
        Assert.Equal(0, fixture.Playback.CancelCount);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
        Assert.Equal(0, fixture.Router.RouteCount);
        Assert.Equal(0, fixture.Feedback.BridgeCount);
    }

    [Fact]
    public async Task HandleInterruptionAsync_Correction_CancelsRequestsBridgeAndRoutes()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
                ConversationalInterruptionType.Correction,
                rewrittenUserRequest: "what is the meaning of a wife",
                requiresBridgeFeedback: true,
                cancelOriginalTurn: true));
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("no i meant what is the meaning of a wife"));

        Assert.Equal(InterruptionHandlingResultType.CancelledAndRedirected, result.Type);
        Assert.Equal("what is the meaning of a wife", result.RedirectedRequest);
        Assert.Equal(1, fixture.Playback.CancelCount);
        Assert.Equal(1, fixture.Feedback.SuppressCount);
        Assert.Equal(1, fixture.Feedback.BridgeCount);
        Assert.Equal(1, fixture.Router.RouteCount);
        Assert.Equal("what is the meaning of a wife", fixture.Router.LastRewrittenRequest);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
    }

    [Fact]
    public async Task HandleInterruptionAsync_EmptyCorrection_AsksUserToClarifyWithoutRouting()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
                ConversationalInterruptionType.Correction,
                rewrittenUserRequest: "",
                requiresBridgeFeedback: true));
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("actually"));

        Assert.Equal(InterruptionHandlingResultType.AskedUserToClarify, result.Type);
        Assert.Equal(1, fixture.Feedback.BridgeCount);
        Assert.Equal(0, fixture.Router.RouteCount);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
    }

    [Fact]
    public async Task HandleInterruptionAsync_Clarification_PreparesClarificationAndContinuation()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
                ConversationalInterruptionType.RelatedFollowUpQuestion,
                requiresClarification: true,
                requiresRecomposition: true));
        StartFocusAndSpokenTracker(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("But the water itself too, right?"));

        Assert.Equal(InterruptionHandlingResultType.ClarificationAndRecompositionPrepared, result.Type);
        Assert.NotNull(result.Checkpoint);
        Assert.NotNull(result.ClarificationRequest);
        Assert.NotNull(result.ClarificationResult);
        Assert.NotNull(result.ContinuationRequest);
        Assert.NotNull(result.ContinuationResult);
        Assert.Equal(1, fixture.Playback.CancelCount);
        Assert.Equal(1, fixture.Feedback.SuppressCount);
        Assert.Equal(1, fixture.Model.ClarificationCount);
        Assert.Equal(1, fixture.Model.ContinuationCount);
        Assert.Equal(0, fixture.Router.RouteCount);
        Assert.Equal("Why does pool water look blue?", result.ClarificationRequest!.OriginalUserQuestion);
        Assert.Equal("Pool water can look blue for several reasons.", result.ClarificationRequest.SpokenAnswerSoFar);
        Assert.Equal("Due to the color of the pool li", result.ClarificationRequest.DiscardedPartialSentence);
        Assert.Equal("But the water itself too, right?", result.ClarificationRequest.UserInterruption);
        Assert.Equal("Yes, exactly. The water itself can also affect the color.", result.ContinuationRequest!.ClarificationReply);
        Assert.Equal("Water itself and depth can affect perceived pool color.", result.ContinuationRequest.ClarificationContext);
    }

    [Fact]
    public async Task HandleInterruptionAsync_ClarificationWithoutContinuation_ReturnsClarificationPrepared()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
                ConversationalInterruptionType.ClarificationQuestion,
                requiresClarification: true,
                requiresRecomposition: true));
        fixture.Model.ClarificationResult = new ClarificationResult
        {
            ReplyText = "Yes.",
            ClarificationContext = "Water itself matters.",
            ShouldRecomposeContinuation = false,
            UserQuestionAnswered = true
        };
        StartFocusAndSpokenTracker(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("what do you mean by liner"));

        Assert.Equal(InterruptionHandlingResultType.ClarificationPrepared, result.Type);
        Assert.NotNull(result.ClarificationResult);
        Assert.Null(result.ContinuationResult);
        Assert.Equal(1, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
    }

    [Fact]
    public async Task HandleInterruptionAsync_SideComment_PreparesContinuationOnly()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint,
                ConversationalInterruptionType.SideComment,
                requiresBridgeFeedback: true,
                requiresRecomposition: true));
        StartFocusAndSpokenTracker(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("well yeah but sunlight too"));

        Assert.Equal(InterruptionHandlingResultType.RecompositionPrepared, result.Type);
        Assert.Equal(1, fixture.Feedback.BridgeCount);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(1, fixture.Model.ContinuationCount);
        Assert.Equal("well yeah but sunlight too", result.ContinuationRequest!.UserInterruption);
        Assert.Equal(string.Empty, result.ContinuationRequest.ClarificationReply);
        Assert.Contains("well yeah but sunlight too", result.ContinuationRequest.ClarificationContext);
    }

    [Fact]
    public async Task HandleInterruptionAsync_QueueFollowUp_QueuesAndRequestsBridge()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent,
                ConversationalInterruptionType.RelatedFollowUpQuestion,
                requiresBridgeFeedback: true,
                queueAfterCurrentTurn: true));
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("can you explain sunlight after this"));

        Assert.Equal(InterruptionHandlingResultType.FollowUpQueued, result.Type);
        Assert.False(string.IsNullOrWhiteSpace(result.QueuedFollowUpId));
        Assert.Equal(1, fixture.Feedback.BridgeCount);
        Assert.Equal(0, fixture.Playback.CancelCount);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
        Assert.Equal(0, fixture.Router.RouteCount);
    }

    [Fact]
    public async Task HandleInterruptionAsync_CheckpointFailure_ReturnsFailed()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
                ConversationalInterruptionType.ClarificationQuestion,
                requiresClarification: true,
                requiresRecomposition: true),
            spokenAnswerTracker: new ThrowingSpokenAnswerTracker());
        StartFocus(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("what about depth"));

        Assert.Equal(InterruptionHandlingResultType.Failed, result.Type);
        Assert.Contains("Checkpoint", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
    }

    [Fact]
    public async Task HandleInterruptionAsync_ModelFailure_ReturnsFailed()
    {
        var fixture = CreateFixture(
            enabled: true,
            Decision(
                ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
                ConversationalInterruptionType.ClarificationQuestion,
                requiresClarification: true,
                requiresRecomposition: true));
        fixture.Model.ThrowOnClarification = true;
        StartFocusAndSpokenTracker(fixture);

        var result = await fixture.Orchestrator.HandleInterruptionAsync(Candidate("what about depth"));

        Assert.Equal(InterruptionHandlingResultType.Failed, result.Type);
        Assert.Contains("model", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static OrchestratorFixture CreateFixture(
        bool enabled,
        ConversationalInterruptionDecision decision,
        ISpokenAnswerTracker? spokenAnswerTracker = null)
    {
        var options = Options.Create(new InterruptionHandlingOptions
        {
            Enabled = enabled,
            ClarificationMaxTokens = 90,
            ContinuationMaxTokens = 500
        });
        var classifier = new FakeClassifier(decision);
        var focus = new ConversationFocusManager(options);
        var tracker = spokenAnswerTracker ?? new SpokenAnswerTracker();
        var playback = new FakePlaybackPort();
        var feedback = new FakeFeedbackPort();
        var router = new FakeRouterPort();
        var model = new FakeModelPort();
        var orchestrator = new InterruptionOrchestrator(
            classifier,
            focus,
            tracker,
            new AnswerRecomposer(),
            playback,
            feedback,
            router,
            model,
            options,
            NullLogger<InterruptionOrchestrator>.Instance);
        return new OrchestratorFixture(orchestrator, classifier, focus, tracker, playback, feedback, router, model);
    }

    private static void StartFocus(OrchestratorFixture fixture)
    {
        fixture.Focus.StartMainTurn("thread-1", "turn-1", "Why does pool water look blue?");
        fixture.Focus.SetAssistantSpeaking(true);
    }

    private static void StartFocusAndSpokenTracker(OrchestratorFixture fixture)
    {
        StartFocus(fixture);
        fixture.Tracker.StartAnswer(
            "turn-1",
            "correlation-1",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons. Due to the liner, it appears blue.");
        fixture.Tracker.AppendSpokenText(
            "turn-1",
            "Pool water can look blue for several reasons. Due to the color of the pool li");
    }

    private static ConversationalInterruptionCandidate Candidate(string transcript) => new()
    {
        CorrelationId = "correlation-1",
        ActiveTurnId = "turn-1",
        Transcript = transcript,
        TranscriptConfidence = 0.9,
        AssistantWasSpeaking = true,
        IsLikelyUserSpeech = true
    };

    private static ConversationalInterruptionDecision Decision(
        ConversationalInterruptionHandlingStrategy strategy,
        ConversationalInterruptionType type = ConversationalInterruptionType.Unknown,
        string? rewrittenUserRequest = null,
        bool requiresBridgeFeedback = false,
        bool requiresClarification = false,
        bool requiresRecomposition = false,
        bool queueAfterCurrentTurn = false,
        bool cancelOriginalTurn = false)
    {
        return new ConversationalInterruptionDecision
        {
            Type = type,
            Strategy = strategy,
            PausePlayback = strategy is not ConversationalInterruptionHandlingStrategy.IgnoreAndContinue
                and not ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse,
            CancelOriginalTurn = cancelOriginalTurn,
            DiscardCurrentPartialSentence = true,
            RequiresBridgeFeedback = requiresBridgeFeedback,
            RequiresDeepInfraClarification = requiresClarification,
            RequiresContinuationRecomposition = requiresRecomposition,
            QueueAfterCurrentTurn = queueAfterCurrentTurn,
            RewrittenUserRequest = rewrittenUserRequest,
            Reason = "test decision"
        };
    }

    private static void AssertNoSideEffects(OrchestratorFixture fixture)
    {
        Assert.Equal(0, fixture.Playback.PauseCount);
        Assert.Equal(0, fixture.Playback.CancelCount);
        Assert.Equal(0, fixture.Playback.StopCount);
        Assert.Equal(0, fixture.Feedback.SuppressCount);
        Assert.Equal(0, fixture.Feedback.BridgeCount);
        Assert.Equal(0, fixture.Router.RouteCount);
        Assert.Equal(0, fixture.Model.ClarificationCount);
        Assert.Equal(0, fixture.Model.ContinuationCount);
    }

    private sealed record OrchestratorFixture(
        InterruptionOrchestrator Orchestrator,
        FakeClassifier Classifier,
        ConversationFocusManager Focus,
        ISpokenAnswerTracker Tracker,
        FakePlaybackPort Playback,
        FakeFeedbackPort Feedback,
        FakeRouterPort Router,
        FakeModelPort Model);

    private sealed class FakeClassifier : IConversationalInterruptionClassifier
    {
        private readonly ConversationalInterruptionDecision _decision;

        public FakeClassifier(ConversationalInterruptionDecision decision)
        {
            _decision = decision;
        }

        public int CallCount { get; private set; }

        public ConversationalInterruptionDecision Classify(ConversationalInterruptionCandidate candidate)
        {
            CallCount++;
            return _decision;
        }
    }

    private sealed class FakePlaybackPort : IInterruptionPlaybackPort
    {
        public int PauseCount { get; private set; }
        public int CancelCount { get; private set; }
        public int StopCount { get; private set; }

        public Task PauseCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            PauseCount++;
            return Task.CompletedTask;
        }

        public Task CancelCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            CancelCount++;
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFeedbackPort : IInterruptionFeedbackPort
    {
        public int SuppressCount { get; private set; }
        public int BridgeCount { get; private set; }

        public Task SuppressNormalProgressAsync(string turnId, CancellationToken cancellationToken = default)
        {
            SuppressCount++;
            return Task.CompletedTask;
        }

        public Task RequestBridgeFeedbackAsync(
            ConversationalInterruptionCandidate candidate,
            ConversationalInterruptionDecision decision,
            ConversationFocusAction focusAction,
            CancellationToken cancellationToken = default)
        {
            BridgeCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRouterPort : IInterruptionRequestRouterPort
    {
        public int RouteCount { get; private set; }
        public string? LastRewrittenRequest { get; private set; }

        public Task RouteRedirectedRequestAsync(
            string rewrittenRequest,
            string originalTurnId,
            string correlationId,
            CancellationToken cancellationToken = default)
        {
            RouteCount++;
            LastRewrittenRequest = rewrittenRequest;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeModelPort : IInterruptionModelPort
    {
        public int ClarificationCount { get; private set; }
        public int ContinuationCount { get; private set; }
        public bool ThrowOnClarification { get; set; }
        public ClarificationResult ClarificationResult { get; set; } = new()
        {
            ReplyText = "Yes, exactly. The water itself can also affect the color.",
            ClarificationContext = "Water itself and depth can affect perceived pool color.",
            ShouldRecomposeContinuation = true,
            UserQuestionAnswered = true
        };

        public ContinuationRecompositionResult ContinuationResult { get; set; } = new()
        {
            ContinuationText = "So besides the liner, the water itself also matters...",
            IncludedClarificationContext = true,
            AvoidedRepeatingSpokenContent = true
        };

        public Task<ClarificationResult> GenerateClarificationAsync(
            ClarificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ClarificationCount++;
            if (ThrowOnClarification)
            {
                throw new InvalidOperationException("model error");
            }

            return Task.FromResult(ClarificationResult);
        }

        public Task<ContinuationRecompositionResult> GenerateContinuationAsync(
            ContinuationRecompositionRequest request,
            CancellationToken cancellationToken = default)
        {
            ContinuationCount++;
            return Task.FromResult(ContinuationResult);
        }
    }

    private sealed class ThrowingSpokenAnswerTracker : ISpokenAnswerTracker
    {
        public SpokenAnswerState? GetState(string turnId) => null;

        public SpokenAnswerState StartAnswer(
            string turnId,
            string correlationId,
            string originalUserQuestion,
            string? originalAssistantDraft = null,
            string? currentTopicLabel = null) =>
            throw new NotSupportedException();

        public SpokenAnswerState AppendSpokenText(string turnId, string text, TimeSpan? playbackPosition = null) =>
            throw new NotSupportedException();

        public SpokenAnswerState MarkChunkStarted(string turnId, string text, TimeSpan? playbackPosition = null) =>
            throw new NotSupportedException();

        public SpokenAnswerState MarkChunkCompleted(string turnId, string text, TimeSpan? playbackPosition = null) =>
            throw new NotSupportedException();

        public SpokenAnswerCheckpoint CreateCheckpoint(string turnId, bool discardCurrentPartialSentence = true) =>
            throw new InvalidOperationException("checkpoint state missing");

        public void Clear(string turnId)
        {
        }
    }
}
