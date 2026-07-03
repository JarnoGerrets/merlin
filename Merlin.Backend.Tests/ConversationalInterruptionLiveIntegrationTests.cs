using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging;
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
    public async Task TryHandleYieldedInterruptionAsync_WhenDisabled_ReturnsDefaultLegacyOutcome()
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

        Assert.NotNull(result);
        Assert.False(result.WasEvaluatedByConversationalInterruption);
        Assert.False(result.WasHandledByConversationalInterruption);
        Assert.True(result.AllowLegacyCleanup);
        Assert.True(result.AllowLegacySemanticRouting);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_WhenLiveIntegrationDisabled_ReturnsDefaultLegacyOutcome()
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

        Assert.NotNull(result);
        Assert.False(result.WasEvaluatedByConversationalInterruption);
        Assert.False(result.WasHandledByConversationalInterruption);
        Assert.True(result.AllowLegacyCleanup);
        Assert.True(result.AllowLegacySemanticRouting);
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

    [Theory]
    [InlineData("What is the meaning of life?")]
    [InlineData("What is chlorophyll?")]
    public async Task TryHandleYieldedInterruptionAsync_IdleQuestionWithoutSpeakingContext_SkipsConversationalInterruption(string transcript)
    {
        var classifier = new FakeClassifier
        {
            Decision = ClarificationDecision()
        };
        var model = new FakeInterruptionModelPort();
        var service = CreateService(
            classifier,
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            spokenTracking: CreateSpokenTrackingService(),
            modelPort: model,
            speechOutputPort: new FakeInterruptionSpeechOutputPort());

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            transcript,
            assistantWasSpeakingResolved: false,
            recentlyYieldedSnapshotFound: false,
            activeTurnId: "live-utterance-monitor",
            correlationId: string.Empty,
            turnBindingSource: "observed_context"));

        Assert.NotNull(outcome);
        Assert.False(outcome.WasEvaluatedByConversationalInterruption);
        Assert.False(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacyCleanup);
        Assert.True(outcome.AllowLegacySemanticRouting);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, model.ClarificationCount);
        Assert.Equal(0, model.ContinuationCount);
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
        Assert.True(result.WasEvaluatedByConversationalInterruption);
        Assert.False(result.WasHandledByConversationalInterruption);
        Assert.True(result.AllowLegacyCleanup);
        Assert.True(result.AllowLegacySemanticRouting);
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
        Assert.True(result.WasEvaluatedByConversationalInterruption);
        Assert.False(result.WasHandledByConversationalInterruption);
        Assert.True(result.AllowLegacyCleanup);
        Assert.True(result.AllowLegacySemanticRouting);
        Assert.Equal(0, classifier.CallCount);
        Assert.Equal(0, orchestrator.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_BackchannelMinimalMode_SuppressesSemanticRoutingAndKeepsCleanupPath()
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

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "yeah",
            provisionalAudioHoldId: "hold-backchannel",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasEvaluatedByConversationalInterruption);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacyCleanup);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldResumeOrContinuePlaybackIfPossible);
        Assert.False(outcome.ShouldCancelPlayback);
        Assert.False(outcome.ShouldCancelCurrentTurn);
        Assert.False(outcome.ShouldRouteReplacementRequest);
        Assert.Equal(InterruptionHandlingResultType.Continued, outcome.Result?.Type);
        Assert.Equal(1, playback.ResumeHoldCount);
        Assert.Equal(0, playback.FlushHoldCount);
        Assert.Equal("hold-backchannel", playback.LastHoldId);
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

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "",
            provisionalAudioHoldId: "hold-empty",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasEvaluatedByConversationalInterruption);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacyCleanup);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldResumeOrContinuePlaybackIfPossible);
        Assert.False(outcome.ShouldCancelPlayback);
        Assert.False(outcome.ShouldRouteReplacementRequest);
        Assert.Equal(ConversationalInterruptionType.NoiseOrFalsePositive, outcome.Result?.Decision.Type);
        Assert.Equal(1, playback.ResumeHoldCount);
        Assert.Equal(0, playback.FlushHoldCount);
        Assert.Equal("hold-empty", playback.LastHoldId);
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

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            transcript,
            layer1Confidence: 0.01,
            provisionalAudioHoldId: "hold-stop",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacyCleanup);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldCancelPlayback);
        Assert.True(outcome.ShouldCancelCurrentTurn);
        Assert.False(outcome.ShouldRouteReplacementRequest);
        Assert.Equal(expectedType, outcome.Result?.Decision.Type);
        Assert.Equal(1, playback.FlushHoldCount);
        Assert.Equal(0, playback.ResumeHoldCount);
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

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "no i meant what is the meaning of a wife",
            provisionalAudioHoldId: "hold-correction",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacyCleanup);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldCancelPlayback);
        Assert.True(outcome.ShouldCancelCurrentTurn);
        Assert.True(outcome.ShouldRouteReplacementRequest);
        Assert.Equal("what is the meaning of a wife", outcome.RewrittenRequest);
        Assert.Equal(1, playback.FlushHoldCount);
        Assert.Equal(0, playback.ResumeHoldCount);
        Assert.Equal("hold-correction", playback.LastHoldId);
        Assert.Equal(1, playback.FlushCount);
        Assert.Equal(1, playback.CancelCount);
        Assert.Equal(0, router.RouteCount);
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
        Assert.False(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacyCleanup);
        Assert.True(outcome.AllowLegacySemanticRouting);
        Assert.Contains("disabled", outcome.Reason, StringComparison.OrdinalIgnoreCase);
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
        Assert.False(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacyCleanup);
        Assert.True(outcome.AllowLegacySemanticRouting);
        Assert.True(
            outcome.Reason.Contains("deferred", StringComparison.OrdinalIgnoreCase)
            || outcome.Reason.Contains("disabled", StringComparison.OrdinalIgnoreCase),
            $"Unexpected reason: {outcome.Reason}");
        Assert.Equal(expectedStrategy, outcome.Result?.Decision.Strategy);
        Assert.Equal(0, playback.CancelCount);
        Assert.Equal(0, router.RouteCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_WithSpokenTrackingEnabled_CanCreateCheckpointDiagnostics()
    {
        var tracker = new SpokenAnswerTracker();
        var spokenTracking = new LiveSpokenAnswerTrackingService(
            tracker,
            Options.Create(new InterruptionHandlingOptions
            {
                EnableLiveSpokenAnswerTracking = true,
                EnableSpokenAnswerTrackingDiagnostics = true
            }),
            NullLogger<LiveSpokenAnswerTrackingService>.Instance);
        spokenTracking.StartAnswer(
            "turn-1",
            "correlation-1",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons. Due to the color of the pool li");
        spokenTracking.MarkChunkCompleted("turn-1", "Pool water can look blue for several reasons.");
        spokenTracking.MarkChunkStarted("turn-1", "Due to the color of the pool li");
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true),
            spokenTracking: spokenTracking);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("yeah"));
        var checkpoint = spokenTracking.TryCreateCheckpoint("turn-1");

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.NotNull(checkpoint);
        Assert.Equal("Pool water can look blue for several reasons.", checkpoint!.SafeSpokenPrefix);
        Assert.Equal("Due to the color of the pool li", checkpoint.DiscardedPartialSentence);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_SequentialRecompositionDisabled_DefersWithoutModelOrSpeech()
    {
        var model = new FakeInterruptionModelPort();
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            new FakeClassifier
            {
                Decision = ClarificationDecision()
            },
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true),
            modelPort: model,
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "but the water itself too right",
            provisionalAudioHoldId: "hold-clarification",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.False(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacySemanticRouting);
        Assert.Equal(0, model.ClarificationCount);
        Assert.Equal(0, speech.SpeakCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_SequentialClarification_RecomposesAndSpeaksInOrder()
    {
        var order = new List<string>();
        var playback = new FakePlaybackPort(order);
        var model = new FakeInterruptionModelPort(order);
        var speech = new FakeInterruptionSpeechOutputPort(order);
        var tracking = StartedTrackingService();
        var service = CreateService(
            new FakeClassifier
            {
                Decision = ClarificationDecision()
            },
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            playback,
            new FakeFeedbackPort(order),
            new FakeRouterPort(),
            spokenTracking: tracking,
            modelPort: model,
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "but the water itself too right",
            provisionalAudioHoldId: "hold-clarification",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldCancelPlayback);
        Assert.False(outcome.ShouldCancelCurrentTurn);
        Assert.Equal(InterruptionHandlingResultType.ClarificationAndRecompositionPrepared, outcome.Result?.Type);
        Assert.Equal(1, playback.FlushCount);
        Assert.Equal(1, playback.CancelCount);
        Assert.Equal(1, model.ClarificationCount);
        Assert.Equal(1, model.ContinuationCount);
        Assert.Equal(1, playback.FlushHoldCount);
        Assert.Equal(0, playback.ResumeHoldCount);
        Assert.Equal(["hold-flush", "flush", "suppress", "cancel", "clarification-model", "speak:clarification", "continuation-model", "speak:recomposed_continuation"], order);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_Stop_FlushesAndSpeaksLocalConfirmationWithoutModel()
    {
        var order = new List<string>();
        var playback = new FakePlaybackPort(order);
        var model = new FakeInterruptionModelPort(order);
        var speech = new FakeInterruptionSpeechOutputPort(order);
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            playback,
            new FakeFeedbackPort(order),
            new FakeRouterPort(),
            modelPort: model,
            speechOutputPort: speech,
            stopConfirmationPhraseSelector: new FakeStopConfirmationPhraseSelector("Okay, stopping."));

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "stop",
            provisionalAudioHoldId: "hold-stop-confirmation",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldCancelPlayback);
        Assert.True(outcome.ShouldCancelCurrentTurn);
        Assert.Equal(InterruptionHandlingResultType.Stopped, outcome.Result?.Type);
        Assert.Equal(1, playback.FlushHoldCount);
        Assert.Equal(0, playback.ResumeHoldCount);
        Assert.Equal(1, playback.FlushCount);
        Assert.Equal(1, playback.StopCount);
        Assert.Equal(0, model.ClarificationCount);
        Assert.Equal(0, model.ContinuationCount);
        Assert.Equal(["stop_confirmation"], speech.ContentKinds);
        Assert.Equal(["hold-flush", "flush", "stop", "speak:stop_confirmation"], order);
    }

    [Theory]
    [InlineData("Merlin, stop.")]
    [InlineData("Stop.")]
    public async Task TryHandleYieldedInterruptionAsync_PlaybackControlStop_MapsToCiStopRequest(string transcript)
    {
        var logger = new RecordingLogger<LiveInterruptionIntegrationService>();
        var classifier = new FakeClassifier
        {
            Decision = new ConversationalInterruptionDecision
            {
                Type = ConversationalInterruptionType.Unknown,
                Strategy = ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption,
                Reason = "fake unknown fallback"
            }
        };
        var playback = new FakePlaybackPort();
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            classifier,
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true),
            playback,
            new FakeFeedbackPort(),
            new FakeRouterPort(),
            speechOutputPort: speech,
            stopConfirmationPhraseSelector: new FakeStopConfirmationPhraseSelector("Got it, I'll stop."),
            logger: logger);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            transcript,
            routeKind: "PauseAndClarify",
            routeAction: "StopSpeechOnlyNoConfirmation",
            layer1Decision: "AcceptPlaybackControl",
            provisionalAudioHoldId: "hold-stop",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasEvaluatedByConversationalInterruption);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacyCleanup);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.True(outcome.ShouldCancelPlayback);
        Assert.True(outcome.ShouldCancelCurrentTurn);
        Assert.Equal(InterruptionHandlingResultType.Stopped, outcome.Result?.Type);
        Assert.Equal(ConversationalInterruptionType.StopRequest, outcome.InterruptionType);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.StopPlayback, outcome.Strategy);
        Assert.Equal(1, playback.FlushHoldCount);
        Assert.Equal(1, playback.FlushCount);
        Assert.Equal(1, playback.StopCount);
        Assert.Equal(0, playback.CancelCount);
        Assert.Equal(["stop_confirmation"], speech.ContentKinds);
        Assert.Equal(0, classifier.CallCount);
        Assert.Contains(logger.Messages, message => message.Contains("playback_control_stop_mapped_to_ci_stop", StringComparison.Ordinal)
            && message.Contains("DecisionType: StopRequest", StringComparison.Ordinal)
            && message.Contains("Strategy: StopPlayback", StringComparison.Ordinal)
            && message.Contains("RouteAction: StopSpeechOnlyNoConfirmation", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("Ask-user-to-clarify is not executed live in PR7", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_NonControlUnknown_DoesNotMapToStop()
    {
        var classifier = new FakeClassifier
        {
            Decision = new ConversationalInterruptionDecision
            {
                Type = ConversationalInterruptionType.Unknown,
                Strategy = ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption,
                Reason = "fake unclear utterance"
            }
        };
        var playback = new FakePlaybackPort();
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            classifier,
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true),
            playback,
            new FakeFeedbackPort(),
            new FakeRouterPort(),
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "as well as the meaning of life",
            routeKind: "PauseAndClarify",
            routeAction: "AskClarification",
            layer1Decision: "AskClarification"));

        Assert.NotNull(outcome);
        Assert.False(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.AllowLegacyCleanup);
        Assert.True(outcome.AllowLegacySemanticRouting);
        Assert.Equal(ConversationalInterruptionType.Unknown, outcome.InterruptionType);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption, outcome.Strategy);
        Assert.Equal(1, classifier.CallCount);
        Assert.Equal(0, playback.StopCount);
        Assert.Empty(speech.ContentKinds);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_Backchannel_DoesNotSemanticallyFlush()
    {
        var playback = new FakePlaybackPort();
        var model = new FakeInterruptionModelPort();
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            playback,
            new FakeFeedbackPort(),
            new FakeRouterPort(),
            modelPort: model,
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "yeah",
            provisionalAudioHoldId: "hold-backchannel-no-flush",
            wasHeldByProvisionalAudioHold: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.Equal(1, playback.ResumeHoldCount);
        Assert.Equal(0, playback.FlushHoldCount);
        Assert.Equal(0, playback.FlushCount);
        Assert.Equal(0, model.ClarificationCount);
        Assert.Equal(0, speech.SpeakCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_BackchannelWithoutHoldId_HandlesSafelyWithoutPlaybackAction()
    {
        var playback = new FakePlaybackPort();
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(),
            playback,
            new FakeFeedbackPort(),
            new FakeRouterPort());

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("yeah"));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.True(outcome.ShouldResumeOrContinuePlaybackIfPossible);
        Assert.Equal(0, playback.ResumeHoldCount);
        Assert.Equal(0, playback.FlushHoldCount);
        Assert.Equal(0, playback.FlushCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_CheckpointLookupUsesRecentlyYieldedTurn()
    {
        var model = new FakeInterruptionModelPort();
        var playback = new FakePlaybackPort();
        var tracking = CreateSpokenTrackingService();
        tracking.StartAnswer(
            "backend_voice:abc",
            "backend_voice:abc",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons. Due to the color of the pool li");
        tracking.MarkChunkCompleted("backend_voice:abc", "Pool water can look blue for several reasons.");
        tracking.MarkChunkStarted("backend_voice:abc", "Due to the color of the pool li");
        var service = CreateService(
            new ConversationalInterruptionClassifier(Options.Create(new InterruptionHandlingOptions())),
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            spokenTracking: tracking,
            modelPort: model,
            playback: playback,
            speechOutputPort: new FakeInterruptionSpeechOutputPort());

        var outcome = await service.TryHandleYieldedInterruptionAsync(new YieldedInterruptionUtterance
        {
            Transcript = "The water itself, too, right?",
            YieldedByLayer1 = true,
            YieldReason = "floor_yield",
            CaptureKind = "NormalInterruption",
            RouteKind = "PauseAndClarify",
            ActiveTurnId = "backend_voice:abc",
            CorrelationId = "backend_voice:abc",
            OriginalObservedTurnId = "live-utterance-monitor",
            TurnBindingSource = "recently_yielded_spoken_turn",
            ActivePlaybackCorrelationId = "backend_voice:abc",
            ActivePlaybackSpeechType = "FinalAnswer",
            ProvisionalAudioHoldId = "hold-recent",
            WasHeldByProvisionalAudioHold = true,
            AssistantWasSpeakingOriginal = false,
            AssistantWasSpeakingResolved = true,
            RecentlyYieldedSnapshotFound = true,
            RecentlyYieldedSnapshotAgeMs = 750,
            Layer1Confidence = 0.9,
            Layer1Decision = "AskClarification"
        });

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.Equal(InterruptionHandlingResultType.ClarificationAndRecompositionPrepared, outcome.Result?.Type);
        Assert.Equal("Why does pool water look blue?", model.LastClarificationRequest?.OriginalUserQuestion);
        Assert.Equal("Pool water can look blue for several reasons.", model.LastClarificationRequest?.SpokenAnswerSoFar);
        Assert.Equal(1, playback.FlushHoldCount);
        Assert.Equal("hold-recent", playback.LastHoldId);
        Assert.Equal(1, model.ClarificationCount);
        Assert.Equal(1, model.ContinuationCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_ActiveSpeakingQuestion_StillEvaluates()
    {
        var classifier = new FakeClassifier
        {
            Decision = ClarificationDecision()
        };
        var service = CreateService(
            classifier,
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true),
            spokenTracking: CreateSpokenTrackingService());

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "Wait, what does that mean?",
            assistantWasSpeakingResolved: true));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasEvaluatedByConversationalInterruption);
        Assert.Equal(1, classifier.CallCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_ClarificationWithoutContinuation_SpeaksClarificationOnly()
    {
        var model = new FakeInterruptionModelPort
        {
            ClarificationResult = new ClarificationResult
            {
                ReplyText = "Yes.",
                ClarificationContext = "Water itself matters.",
                ShouldRecomposeContinuation = false,
                UserQuestionAnswered = true
            }
        };
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            new FakeClassifier { Decision = ClarificationDecision() },
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            spokenTracking: StartedTrackingService(),
            modelPort: model,
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("but the water itself too right"));

        Assert.NotNull(outcome);
        Assert.Equal(InterruptionHandlingResultType.ClarificationPrepared, outcome.Result?.Type);
        Assert.Equal(1, model.ClarificationCount);
        Assert.Equal(0, model.ContinuationCount);
        Assert.Equal(["clarification"], speech.ContentKinds);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_MissingTrackerState_UsesCoarseYieldedUtteranceFallback()
    {
        var model = new FakeInterruptionModelPort();
        var service = CreateService(
            new FakeClassifier { Decision = ClarificationDecision() },
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            spokenTracking: CreateSpokenTrackingService(),
            modelPort: model,
            speechOutputPort: new FakeInterruptionSpeechOutputPort());

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance(
            "but the water itself too right",
            originalUserQuestion: "Why does pool water look blue?",
            lastCompletedAssistantSentence: "Pool water can look blue for several reasons.",
            currentAssistantSentence: "Due to the color of the pool li"));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.Equal("Why does pool water look blue?", model.LastClarificationRequest?.OriginalUserQuestion);
        Assert.Equal("Due to the color of the pool li", model.LastClarificationRequest?.DiscardedPartialSentence);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_MissingOriginalQuestion_FailsSafelyWithoutModelOrSpeech()
    {
        var model = new FakeInterruptionModelPort();
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            new FakeClassifier { Decision = ClarificationDecision() },
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            spokenTracking: CreateSpokenTrackingService(),
            modelPort: model,
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("but the water itself too right"));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.Equal(InterruptionHandlingResultType.Failed, outcome.Result?.Type);
        Assert.Equal(0, model.ClarificationCount);
        Assert.Equal(0, speech.SpeakCount);
    }

    [Fact]
    public async Task TryHandleYieldedInterruptionAsync_ContinuationFailureAfterClarification_DoesNotResumeOldRouting()
    {
        var model = new FakeInterruptionModelPort
        {
            ThrowOnContinuation = true
        };
        var speech = new FakeInterruptionSpeechOutputPort();
        var service = CreateService(
            new FakeClassifier { Decision = ClarificationDecision() },
            new FakeOrchestrator(),
            MinimalOptions(enableSpokenTracking: true, enableSequentialRecomposition: true, enableModelCalls: true),
            spokenTracking: StartedTrackingService(),
            modelPort: model,
            speechOutputPort: speech);

        var outcome = await service.TryHandleYieldedInterruptionAsync(YieldedUtterance("but the water itself too right"));

        Assert.NotNull(outcome);
        Assert.True(outcome.WasHandledByConversationalInterruption);
        Assert.False(outcome.AllowLegacySemanticRouting);
        Assert.Equal(InterruptionHandlingResultType.Failed, outcome.Result?.Type);
        Assert.Equal(1, model.ClarificationCount);
        Assert.Equal(1, model.ContinuationCount);
        Assert.Equal(["clarification"], speech.ContentKinds);
    }

    private static LiveInterruptionIntegrationService CreateService(
        IConversationalInterruptionClassifier classifier,
        IInterruptionOrchestrator orchestrator,
        InterruptionHandlingOptions options,
        FakePlaybackPort? playback = null,
        FakeFeedbackPort? feedback = null,
        FakeRouterPort? router = null,
        ILiveSpokenAnswerTrackingService? spokenTracking = null,
        IInterruptionModelPort? modelPort = null,
        IInterruptionSpeechOutputPort? speechOutputPort = null,
        IStopConfirmationPhraseSelector? stopConfirmationPhraseSelector = null,
        ILogger<LiveInterruptionIntegrationService>? logger = null) =>
        new(
            new ConversationalInterruptionCandidateFactory(),
            classifier,
            orchestrator,
            playback ?? new FakePlaybackPort(),
            feedback ?? new FakeFeedbackPort(),
            router ?? new FakeRouterPort(),
            Options.Create(options),
            logger ?? NullLogger<LiveInterruptionIntegrationService>.Instance,
            spokenTracking,
            modelPort,
            speechOutputPort,
            stopConfirmationPhraseSelector);

    private static InterruptionHandlingOptions MinimalOptions(
        bool enableRedirectRouting = true,
        bool enableFeedbackBridge = false,
        bool enableSpokenTracking = false,
        bool enableSequentialRecomposition = false,
        bool enableModelCalls = false) => new()
    {
        Enabled = true,
        EnableLiveBargeInIntegration = true,
        EnableLiveShadowMode = false,
        EnableLiveMinimalBehavior = true,
        EnableLivePlaybackActions = true,
        EnableLiveRedirectRouting = enableRedirectRouting,
        EnableLiveResponsiveFeedbackBridge = enableFeedbackBridge,
        EnableLiveModelCalls = enableModelCalls,
        EnableLiveSpokenAnswerTracking = enableSpokenTracking,
        EnableSpokenAnswerTrackingDiagnostics = true,
        EnableClarificationCalls = enableModelCalls,
        EnableContinuationRecomposition = enableModelCalls,
        EnableSequentialRecomposition = enableSequentialRecomposition
    };

    private static YieldedInterruptionUtterance YieldedUtterance(
        string transcript,
        bool yieldedByLayer1 = true,
        double? layer1Confidence = 0.9,
        string? originalUserQuestion = null,
        string? lastCompletedAssistantSentence = null,
        string? currentAssistantSentence = null,
        bool? assistantWasSpeakingResolved = true,
        bool recentlyYieldedSnapshotFound = false,
        string activeTurnId = "turn-1",
        string correlationId = "correlation-1",
        string? turnBindingSource = "active_playback_snapshot",
        string? provisionalAudioHoldId = null,
        bool wasHeldByProvisionalAudioHold = false,
        string routeKind = "CancelActiveTurn",
        string routeAction = "CancelActiveTurn",
        string layer1Decision = "AcceptCancellation") => new()
    {
        Transcript = transcript,
        YieldedByLayer1 = yieldedByLayer1,
        YieldReason = "floor_yield",
        CaptureKind = "NormalInterruption",
        RouteKind = routeKind,
        RouteAction = routeAction,
        Layer1Confidence = layer1Confidence,
        Layer1Decision = layer1Decision,
        CorrelationId = correlationId,
        ActiveTurnId = activeTurnId,
        OriginalObservedTurnId = activeTurnId,
        TurnBindingSource = turnBindingSource,
        ProvisionalAudioHoldId = provisionalAudioHoldId,
        WasHeldByProvisionalAudioHold = wasHeldByProvisionalAudioHold,
        AssistantWasSpeakingOriginal = assistantWasSpeakingResolved,
        AssistantWasSpeakingResolved = assistantWasSpeakingResolved,
        RecentlyYieldedSnapshotFound = recentlyYieldedSnapshotFound,
        OriginalUserQuestion = originalUserQuestion,
        LastCompletedAssistantSentence = lastCompletedAssistantSentence,
        CurrentAssistantSentence = currentAssistantSentence
    };

    private static ConversationalInterruptionDecision ClarificationDecision() => new()
    {
        Type = ConversationalInterruptionType.RelatedFollowUpQuestion,
        Strategy = ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint,
        RequiresDeepInfraClarification = true,
        RequiresContinuationRecomposition = true,
        DiscardCurrentPartialSentence = true,
        Reason = "test clarification"
    };

    private static LiveSpokenAnswerTrackingService StartedTrackingService()
    {
        var tracking = CreateSpokenTrackingService();
        tracking.StartAnswer(
            "turn-1",
            "correlation-1",
            "Why does pool water look blue?",
            "Pool water can look blue for several reasons. Due to the color of the pool li");
        tracking.MarkChunkCompleted("turn-1", "Pool water can look blue for several reasons.");
        tracking.MarkChunkStarted("turn-1", "Due to the color of the pool li");
        return tracking;
    }

    private static LiveSpokenAnswerTrackingService CreateSpokenTrackingService() =>
        new(
            new SpokenAnswerTracker(),
            Options.Create(new InterruptionHandlingOptions
            {
                EnableLiveSpokenAnswerTracking = true,
                EnableSpokenAnswerTrackingDiagnostics = true
            }),
            NullLogger<LiveSpokenAnswerTrackingService>.Instance);

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
        private readonly List<string>? _order;

        public FakePlaybackPort(List<string>? order = null)
        {
            _order = order;
        }

        public int PauseCount { get; private set; }
        public int CancelCount { get; private set; }
        public int StopCount { get; private set; }
        public int FlushCount { get; private set; }
        public int ResumeHoldCount { get; private set; }
        public int FlushHoldCount { get; private set; }
        public string? LastHoldId { get; private set; }

        public Task PauseCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            PauseCount++;
            return Task.CompletedTask;
        }

        public Task CancelCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            CancelCount++;
            _order?.Add("cancel");
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            StopCount++;
            _order?.Add("stop");
            return Task.CompletedTask;
        }

        public Task FlushFinalAnswerSpeechForTurnAsync(string turnId, string reason, CancellationToken cancellationToken = default)
        {
            FlushCount++;
            _order?.Add("flush");
            return Task.CompletedTask;
        }

        public Task<ProvisionalAudioHoldResult> ResumeProvisionalAudioHoldAsync(string holdId, string reason, CancellationToken cancellationToken = default)
        {
            ResumeHoldCount++;
            LastHoldId = holdId;
            _order?.Add("hold-resume");
            return Task.FromResult(new ProvisionalAudioHoldResult(
                Success: true,
                HoldId: holdId,
                TurnId: "turn-1",
                Reason: reason,
                WasResumed: true));
        }

        public Task<ProvisionalAudioHoldResult> FlushProvisionalAudioHoldAsync(string holdId, string reason, CancellationToken cancellationToken = default)
        {
            FlushHoldCount++;
            LastHoldId = holdId;
            _order?.Add("hold-flush");
            return Task.FromResult(new ProvisionalAudioHoldResult(
                Success: true,
                HoldId: holdId,
                TurnId: "turn-1",
                Reason: reason,
                WasFlushed: true));
        }
    }

    private sealed class FakeFeedbackPort : IInterruptionFeedbackPort
    {
        private readonly List<string>? _order;

        public FakeFeedbackPort(List<string>? order = null)
        {
            _order = order;
        }

        public int SuppressCount { get; private set; }
        public int BridgeCount { get; private set; }

        public Task SuppressNormalProgressAsync(string turnId, CancellationToken cancellationToken = default)
        {
            SuppressCount++;
            _order?.Add("suppress");
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

    private sealed class FakeInterruptionModelPort : IInterruptionModelPort
    {
        private readonly List<string>? _order;

        public FakeInterruptionModelPort(List<string>? order = null)
        {
            _order = order;
        }

        public int ClarificationCount { get; private set; }
        public int ContinuationCount { get; private set; }
        public bool ThrowOnContinuation { get; set; }
        public ClarificationRequest? LastClarificationRequest { get; private set; }
        public ClarificationResult ClarificationResult { get; set; } = new()
        {
            ReplyText = "Yes, exactly. The water itself can affect the color too.",
            ClarificationContext = "Water itself and depth affect perceived pool color.",
            ShouldRecomposeContinuation = true,
            UserQuestionAnswered = true
        };

        public Task<ClarificationResult> GenerateClarificationAsync(
            ClarificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ClarificationCount++;
            LastClarificationRequest = request;
            _order?.Add("clarification-model");
            return Task.FromResult(ClarificationResult);
        }

        public Task<ContinuationRecompositionResult> GenerateContinuationAsync(
            ContinuationRecompositionRequest request,
            CancellationToken cancellationToken = default)
        {
            ContinuationCount++;
            _order?.Add("continuation-model");
            if (ThrowOnContinuation)
            {
                throw new InvalidOperationException("continuation failed");
            }

            return Task.FromResult(new ContinuationRecompositionResult
            {
                ContinuationText = "So besides the liner, the water itself also matters.",
                IncludedClarificationContext = true,
                AvoidedRepeatingSpokenContent = true
            });
        }
    }

    private sealed class FakeInterruptionSpeechOutputPort : IInterruptionSpeechOutputPort
    {
        private readonly List<string>? _order;
        private readonly List<string> _contentKinds = new();

        public FakeInterruptionSpeechOutputPort(List<string>? order = null)
        {
            _order = order;
        }

        public int SpeakCount { get; private set; }

        public IReadOnlyList<string> ContentKinds => _contentKinds;

        public Task SpeakInterruptionContentAsync(
            string turnId,
            string correlationId,
            string text,
            string contentKind,
            CancellationToken cancellationToken = default)
        {
            SpeakCount++;
            _contentKinds.Add(contentKind);
            _order?.Add($"speak:{contentKind}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStopConfirmationPhraseSelector : IStopConfirmationPhraseSelector
    {
        private readonly string _phrase;

        public FakeStopConfirmationPhraseSelector(string phrase)
        {
            _phrase = phrase;
        }

        public string SelectPhrase() => _phrase;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly object _sync = new();
        private readonly List<string> _messages = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_sync)
                {
                    return _messages.ToArray();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_sync)
            {
                _messages.Add(formatter(state, exception));
            }
        }
    }
}
