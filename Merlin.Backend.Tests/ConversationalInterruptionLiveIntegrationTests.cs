using Merlin.Backend.Configuration;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConversationalInterruptionLiveIntegrationTests
{
    [Fact]
    public void CandidateFactory_MapsYieldedUtteranceWithoutUsingLayer1ConfidenceAsTranscriptConfidence()
    {
        var started = DateTimeOffset.UtcNow.AddSeconds(-1);
        var ended = DateTimeOffset.UtcNow;
        var factory = new ConversationalInterruptionCandidateFactory();

        var candidate = factory.CreateFromYieldedInterruption(new YieldedInterruptionUtterance
        {
            Transcript = " stop ",
            YieldedByLayer1 = true,
            YieldReason = "floor_yield",
            CaptureKind = "NormalInterruption",
            RouteKind = "CancelActiveTurn",
            Layer1Confidence = 0.12,
            Layer1Decision = "AcceptCancellation",
            CorrelationId = "correlation-1",
            ActiveTurnId = "turn-1",
            OriginalUserQuestion = "why is the pool blue?",
            CurrentAssistantSentence = "The liner can affect it",
            LastCompletedAssistantSentence = "Water absorbs red light.",
            StartedAtUtc = started,
            EndedAtUtc = ended
        });

        Assert.Equal("stop", candidate.Transcript);
        Assert.Equal(1.0, candidate.TranscriptConfidence);
        Assert.Equal("correlation-1", candidate.CorrelationId);
        Assert.Equal("turn-1", candidate.ActiveTurnId);
        Assert.True(candidate.IsLikelyUserSpeech);
        Assert.False(candidate.IsLikelySelfEcho);
        Assert.Equal("why is the pool blue?", candidate.OriginalUserQuestion);
        Assert.Equal("The liner can affect it", candidate.CurrentAssistantSentence);
        Assert.Equal("Water absorbs red light.", candidate.LastCompletedAssistantSentence);
        Assert.Equal(started, candidate.StartedAtUtc);
        Assert.Equal(ended, candidate.EndedAtUtc);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_WhenDisabled_DoesNotCallClassifierOrOrchestrator()
    {
        var classifier = new FakeClassifier();
        var orchestrator = new FakeOrchestrator();
        var service = CreateService(
            classifier,
            orchestrator,
            new InterruptionHandlingOptions
            {
                Enabled = false,
                EnableLiveBargeInIntegration = false
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("stop"));

        Assert.Null(result);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_WhenLiveIntegrationDisabled_DoesNotCallClassifierOrOrchestrator()
    {
        var classifier = new FakeClassifier();
        var orchestrator = new FakeOrchestrator();
        var service = CreateService(
            classifier,
            orchestrator,
            new InterruptionHandlingOptions
            {
                Enabled = true,
                EnableLiveBargeInIntegration = false
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("stop"));

        Assert.Null(result);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_WhenNotYielded_DoesNotEnterLayer2()
    {
        var classifier = new FakeClassifier();
        var orchestrator = new FakeOrchestrator();
        var service = CreateService(
            classifier,
            orchestrator,
            new InterruptionHandlingOptions
            {
                Enabled = true,
                EnableLiveBargeInIntegration = true,
                EnableLiveShadowMode = true
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("stop", yieldedByLayer1: false));

        Assert.Null(result);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_InShadowMode_ClassifiesMeaningWithoutOrchestrator()
    {
        var classifier = new FakeClassifier
        {
            Decision = new ConversationalInterruptionDecision
            {
                Type = ConversationalInterruptionType.Correction,
                Strategy = ConversationalInterruptionHandlingStrategy.CancelAndRedirect,
                Reason = "shadow test"
            }
        };
        var orchestrator = new FakeOrchestrator();
        var service = CreateService(
            classifier,
            orchestrator,
            new InterruptionHandlingOptions
            {
                Enabled = true,
                EnableLiveBargeInIntegration = true,
                EnableLiveShadowMode = true
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("actually make that blue"));

        Assert.NotNull(result);
        Assert.False(result.WasHandled);
        Assert.True(result.ShouldContinueOldPath);
        Assert.Equal(InterruptionHandlingResultType.Ignored, result.Result?.Type);
        Assert.Equal(ConversationalInterruptionType.Correction, result.Result?.Decision.Type);
        Assert.Equal(1, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
        Assert.NotNull(classifier.LastCandidate);
        Assert.Equal(1.0, classifier.LastCandidate.TranscriptConfidence);
        Assert.True(classifier.LastCandidate.IsLikelyUserSpeech);
    }

    [Theory]
    [InlineData("stop", ConversationalInterruptionType.StopRequest, ConversationalInterruptionHandlingStrategy.StopPlayback)]
    [InlineData("yeah", ConversationalInterruptionType.Backchannel, ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse)]
    [InlineData("no I meant what is the meaning of a wife", ConversationalInterruptionType.Correction, ConversationalInterruptionHandlingStrategy.CancelAndRedirect)]
    public async Task TryHandleYieldedInterruptionAsync_LowLayer1ConfidenceStillClassifiesByTextMeaning(
        string transcript,
        ConversationalInterruptionType expectedType,
        ConversationalInterruptionHandlingStrategy expectedStrategy)
    {
        var classifier = new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions()));
        var service = CreateService(
            classifier,
            new FakeOrchestrator(),
            new InterruptionHandlingOptions
            {
                Enabled = true,
                EnableLiveBargeInIntegration = true,
                EnableLiveShadowMode = true
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(transcript, layer1Confidence: 0.12));

        Assert.NotNull(result);
        Assert.Equal(expectedType, result.Result?.Decision.Type);
        Assert.Equal(expectedStrategy, result.Result?.Decision.Strategy);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_EmptyYieldedTranscriptIsConversationallyUseless()
    {
        var classifier = new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions()));
        var service = CreateService(
            classifier,
            new FakeOrchestrator(),
            new InterruptionHandlingOptions
            {
                Enabled = true,
                EnableLiveBargeInIntegration = true,
                EnableLiveShadowMode = true
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(""));

        Assert.NotNull(result);
        Assert.Equal(ConversationalInterruptionType.NoiseOrFalsePositive, result.Result?.Decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.IgnoreAndContinue, result.Result?.Decision.Strategy);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_WhenMinimalBehaviorDisabled_DefersWithoutOrchestrator()
    {
        var classifier = new FakeClassifier();
        var orchestrator = new FakeOrchestrator();
        var service = CreateService(
            classifier,
            orchestrator,
            new InterruptionHandlingOptions
            {
                Enabled = true,
                EnableLiveBargeInIntegration = true,
                EnableLiveShadowMode = false,
                EnableLiveMinimalBehavior = false
            });

        var result = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("stop"));

        Assert.NotNull(result);
        Assert.False(result.WasHandled);
        Assert.True(result.ShouldContinueOldPath);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_BackchannelMinimalMode_HandlesWithoutSideEffectsAndKeepsOldCleanupPath()
    {
        var playback = new FakePlaybackPort();
        var feedback = new FakeFeedbackPort();
        var router = new FakeRouterPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(),
            playback,
            feedback,
            router);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("yeah"));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandled);
        Assert.True(outcome.ShouldContinueOldPath);
        Assert.Equal(InterruptionHandlingResultType.Continued, outcome.Result?.Type);
        Assert.Equal(0, playback.CancelCount);
        Assert.Equal(0, playback.StopCount);
        Assert.Equal(0, router.RouteCount);
        Assert.Equal(0, feedback.BridgeCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_EmptyMinimalMode_HandlesAsUselessWithoutSideEffects()
    {
        var playback = new FakePlaybackPort();
        var router = new FakeRouterPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(),
            playback,
            new FakeFeedbackPort(),
            router);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(""));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandled);
        Assert.True(outcome.ShouldContinueOldPath);
        Assert.Equal(ConversationalInterruptionType.NoiseOrFalsePositive, outcome.Result?.Decision.Type);
        Assert.Equal(0, playback.CancelCount);
        Assert.Equal(0, playback.StopCount);
        Assert.Equal(0, router.RouteCount);
    }

    [Theory]
    [InlineData("stop", ConversationalInterruptionType.StopRequest)]
    [InlineData("cancel", ConversationalInterruptionType.CancelRequest)]
    public async Task TryHandleYieldedInterruptionAsync_StopOrCancelMinimalMode_StopsWithoutRouterModelOrBridge(
        string transcript,
        ConversationalInterruptionType expectedType)
    {
        var playback = new FakePlaybackPort();
        var feedback = new FakeFeedbackPort();
        var router = new FakeRouterPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(),
            playback,
            feedback,
            router);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(transcript, layer1Confidence: 0.01));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandled);
        Assert.False(outcome.ShouldContinueOldPath);
        Assert.True(outcome.ShouldCancelActiveTurn);
        Assert.Equal(expectedType, outcome.Result?.Decision.Type);
        Assert.Equal(0, router.RouteCount);
        Assert.Equal(0, feedback.BridgeCount);
        Assert.True(playback.StopCount + playback.CancelCount > 0);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_CorrectionMinimalMode_RoutesOnceWhenEnabled()
    {
        var playback = new FakePlaybackPort();
        var feedback = new FakeFeedbackPort();
        var router = new FakeRouterPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(enableFeedbackBridge: true),
            playback,
            feedback,
            router);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("no i meant what is the meaning of a wife"));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandled);
        Assert.False(outcome.ShouldContinueOldPath);
        Assert.True(outcome.IsCorrectionRedirect);
        Assert.Equal("what is the meaning of a wife", outcome.RedirectedRequest);
        Assert.Equal(1, playback.CancelCount);
        Assert.Equal(1, router.RouteCount);
        Assert.Equal("what is the meaning of a wife", router.LastRewrittenRequest);
        Assert.Equal(1, feedback.SuppressCount);
        Assert.Equal(1, feedback.BridgeCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_CorrectionWithRoutingDisabled_DefersToOldPath()
    {
        var playback = new FakePlaybackPort();
        var router = new FakeRouterPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(enableRedirectRouting: false),
            playback,
            new FakeFeedbackPort(),
            router);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("no i meant what is the meaning of a wife"));

        Assert.NotNull(outcome);
        Assert.False(outcome.WasHandled);
        Assert.True(outcome.ShouldContinueOldPath);
        Assert.Equal(0, playback.CancelCount);
        Assert.Equal(0, router.RouteCount);
    }

    [Theory]
    [InlineData("but the water itself too right", ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint)]
    [InlineData("well yeah but sunlight too", ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint)]
    public async Task TryHandleYieldedInterruptionAsync_UnsupportedMinimalStrategies_DeferToOldPath(
        string transcript,
        ConversationalInterruptionHandlingStrategy expectedStrategy)
    {
        var playback = new FakePlaybackPort();
        var router = new FakeRouterPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(),
            playback,
            new FakeFeedbackPort(),
            router);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(transcript));

        Assert.NotNull(outcome);
        Assert.False(outcome.WasHandled);
        Assert.True(outcome.ShouldContinueOldPath);
        Assert.Equal(expectedStrategy, outcome.Result?.Decision.Strategy);
        Assert.Equal(0, playback.CancelCount);
        Assert.Equal(0, router.RouteCount);
    }

    private static LiveInterruptionIntegrationService CreateService(
        IConversationalInterruptionClassifier classifier,
        IInterruptionOrchestrator orchestrator,
        InterruptionHandlingOptions options,
        FakePlaybackPort? playback = null,
        FakeFeedbackPort? feedback = null,
        FakeRouterPort? router = null) =>
        new(
            new ConversationalInterruptionCandidateFactory(),
            classifier,
            orchestrator,
            playback ?? new FakePlaybackPort(),
            feedback ?? new FakeFeedbackPort(),
            router ?? new FakeRouterPort(),
            Options.Create(options),
            NullLogger<LiveInterruptionIntegrationService>.Instance);

    private static InterruptionHandlingOptions MinimalOptions(
        bool enableRedirectRouting = true,
        bool enableFeedbackBridge = false) => new()
    {
        Enabled = true,
        EnableLiveBargeInIntegration = true,
        EnableLiveShadowMode = false,
        EnableLiveMinimalBehavior = true,
        EnableLivePlaybackActions = true,
        EnableLiveRedirectRouting = enableRedirectRouting,
        EnableLiveResponsiveFeedbackBridge = enableFeedbackBridge,
        EnableLiveModelCalls = false
    };

    private static YieldedInterruptionUtterance YieldedUtterance(
        string transcript,
        bool yieldedByLayer1 = true,
        double? layer1Confidence = 0.9) => new()
    {
        Transcript = transcript,
        YieldedByLayer1 = yieldedByLayer1,
        YieldReason = "floor_yield",
        CaptureKind = "NormalInterruption",
        RouteKind = "CancelActiveTurn",
        Layer1Confidence = layer1Confidence,
        Layer1Decision = "AcceptCancellation",
        CorrelationId = "correlation-1",
        ActiveTurnId = "turn-1"
    };

    private sealed class FakeClassifier : IConversationalInterruptionClassifier
    {
        public int CallCount { get; private set; }

        public ConversationalInterruptionCandidate? LastCandidate { get; private set; }

        public ConversationalInterruptionDecision Decision { get; set; } = new()
        {
            Type = ConversationalInterruptionType.Backchannel,
            Strategy = ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse,
            Reason = "fake"
        };

        public ConversationalInterruptionDecision Classify(ConversationalInterruptionCandidate candidate)
        {
            CallCount++;
            LastCandidate = candidate;
            return Decision;
        }
    }

    private sealed class FakeOrchestrator : IInterruptionOrchestrator
    {
        public int CallCount { get; private set; }

        public Task<InterruptionHandlingResult> HandleInterruptionAsync(
            ConversationalInterruptionCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new InterruptionHandlingResult
            {
                Type = InterruptionHandlingResultType.Continued,
                Reason = "orchestrated"
            });
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
}
