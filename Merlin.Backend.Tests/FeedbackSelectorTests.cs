using Merlin.Backend.Configuration;
using Merlin.Backend.Services.Feedback;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class FeedbackSelectorTests
{
    [Fact]
    public void Select_WhenExternalAppContext_SelectsExternalOpenCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.ExternalApp, duration: FeedbackDurationEstimate.Short));

        Assert.NotNull(selection);
        Assert.StartsWith("external_open_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenFileSearchContext_SelectsFileSearchCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.FileSearch, duration: FeedbackDurationEstimate.Medium));

        Assert.NotNull(selection);
        Assert.StartsWith("file_search_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenWebSearchContext_SelectsWebSearchCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.WebSearch, duration: FeedbackDurationEstimate.Medium));

        Assert.NotNull(selection);
        Assert.StartsWith("web_search_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenMemoryContext_SelectsMemoryCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.Memory, duration: FeedbackDurationEstimate.Medium));

        Assert.NotNull(selection);
        Assert.StartsWith("memory_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenConversationContext_SelectsConversationThinkingCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.Conversation, duration: FeedbackDurationEstimate.Medium));

        Assert.NotNull(selection);
        Assert.StartsWith("conversation_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file_search_")]
    [InlineData("web_search_")]
    [InlineData("memory_")]
    [InlineData("external_open_")]
    [InlineData("confirmation_")]
    [InlineData("interruption_")]
    public void Select_WhenConversationContext_DoesNotSelectUnrelatedDomainCards(string disallowedPrefix)
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.Conversation, duration: FeedbackDurationEstimate.Medium));

        Assert.NotNull(selection);
        Assert.False(selection.Card.Id.StartsWith(disallowedPrefix, StringComparison.Ordinal), selection.Card.Id);
    }

    [Fact]
    public void Select_WhenConversationCardsOnCooldown_DoesNotFallbackToUnrelatedDomain()
    {
        var selector = CreateSelector();
        var selectedIds = new List<string>();

        for (var index = 0; index < 4; index++)
        {
            var selection = selector.Select(Context(
                FeedbackDomain.Conversation,
                duration: FeedbackDurationEstimate.Medium,
                turnId: $"conversation-turn-{index}"));

            Assert.NotNull(selection);
            Assert.StartsWith("conversation_", selection.Card.Id, StringComparison.Ordinal);
            selectedIds.Add(selection.Card.Id);
        }

        var fallback = selector.Select(Context(
            FeedbackDomain.Conversation,
            duration: FeedbackDurationEstimate.Medium,
            turnId: "conversation-turn-fallback"));

        Assert.Equal(4, selectedIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Null(fallback);
    }

    [Fact]
    public void Select_WhenConfirmationNeeded_SelectsConfirmationCardOverGeneric()
    {
        var selector = CreateSelector();

        var selection = selector.Select(new FeedbackContext
        {
            CorrelationId = "turn-confirm",
            TurnId = "turn-confirm",
            Phase = FeedbackPhase.NeedsConfirmation,
            Domain = FeedbackDomain.Confirmation,
            NeedsConfirmation = true,
            AllowSpeech = true
        });

        Assert.NotNull(selection);
        Assert.StartsWith("confirmation_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenScoreBelowThreshold_ReturnsNull()
    {
        var selector = CreateSelector(new ResponsiveFeedbackOptions
        {
            MinimumSelectionScore = 100,
            GlobalCooldownMs = 0,
            MaxImmediateFeedbackPerTurn = 1
        });

        var selection = selector.Select(Context(FeedbackDomain.ExternalApp));

        Assert.Null(selection);
    }

    [Fact]
    public void Select_WhenSameTurnAlreadyUsed_ReturnsNull()
    {
        var selector = CreateSelector();
        var context = Context(FeedbackDomain.ExternalApp, turnId: "same-turn");

        var first = selector.Select(context);
        var second = selector.Select(context);

        Assert.NotNull(first);
        Assert.Null(second);
    }

    [Fact]
    public void Select_WhenSpeechDisallowed_ReturnsNull()
    {
        var selector = CreateSelector();

        var selection = selector.Select(Context(FeedbackDomain.ExternalApp, allowSpeech: false));

        Assert.Null(selection);
    }

    [Fact]
    public void Select_WhenInterruptionContext_SelectsInterruptionCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(new FeedbackContext
        {
            CorrelationId = "turn-interrupt",
            TurnId = "turn-interrupt",
            Phase = FeedbackPhase.RecomposingContinuation,
            Domain = FeedbackDomain.Interruption,
            IsInterruptionFeedback = true,
            IsRecompositionFeedback = true,
            AllowSpeech = true
        });

        Assert.NotNull(selection);
        Assert.StartsWith("interruption_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenInterruptionCorrectionContext_SelectsCorrectionOrRedirectCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(InterruptionContext(
            FeedbackPhase.Redirecting,
            tags: ["correction", "redirect"]));

        Assert.NotNull(selection);
        Assert.True(
            selection.Card.Id.StartsWith("interruption_correction_", StringComparison.Ordinal)
            || selection.Card.Id.StartsWith("interruption_redirect_", StringComparison.Ordinal),
            selection.Card.Id);
    }

    [Fact]
    public void Select_WhenInterruptionRecompositionContext_SelectsRecomposeCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(InterruptionContext(
            FeedbackPhase.RecomposingContinuation,
            isRecomposition: true,
            tags: ["recompose", "clarification"]));

        Assert.NotNull(selection);
        Assert.StartsWith("interruption_recompose_", selection.Card.Id, StringComparison.Ordinal);
        Assert.DoesNotContain("_wait_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenInterruptionQueueContext_SelectsQueueFollowUpCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(InterruptionContext(
            FeedbackPhase.QueueingFollowUp,
            tags: ["follow_up", "queue"]));

        Assert.NotNull(selection);
        Assert.StartsWith("interruption_queue_followup_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void Select_WhenInterruptionUnclearContext_SelectsUnclearCard()
    {
        var selector = CreateSelector();

        var selection = selector.Select(InterruptionContext(
            FeedbackPhase.HandlingInterruption,
            tags: ["unclear"]));

        Assert.NotNull(selection);
        Assert.StartsWith("interruption_unclear_", selection.Card.Id, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(FeedbackDomain.Conversation)]
    [InlineData(FeedbackDomain.ExternalApp)]
    public void Select_WhenNonInterruptionContextHasInterruptionTags_CannotSelectInterruptionCard(FeedbackDomain domain)
    {
        var selector = CreateSelector();

        var selection = selector.Select(new FeedbackContext
        {
            CorrelationId = Guid.NewGuid().ToString("N"),
            TurnId = Guid.NewGuid().ToString("N"),
            Phase = FeedbackPhase.Redirecting,
            Domain = domain,
            DurationEstimate = FeedbackDurationEstimate.Short,
            Confidence = FeedbackConfidence.High,
            Tags = ["interruption", "correction", "redirect"],
            AllowSpeech = true,
            IsExternalAction = domain == FeedbackDomain.ExternalApp
        });

        Assert.True(selection is null || !selection.Card.Id.StartsWith("interruption_", StringComparison.Ordinal));
    }

    [Fact]
    public void Select_WhenInterruptionCardsOnCooldown_DoesNotFallbackToGeneralOrToolCard()
    {
        var selector = CreateSelector(new ResponsiveFeedbackOptions
        {
            GlobalCooldownMs = 0,
            SameTextCooldownSeconds = 0,
            MinimumSelectionScore = 0.5,
            MaxImmediateFeedbackPerTurn = 1,
            MaxInterruptionBridgeFeedbackPerInterruption = 100
        });

        for (var index = 0; index < 3; index++)
        {
            var selection = selector.Select(InterruptionContext(
                FeedbackPhase.QueueingFollowUp,
                turnId: $"queue-{index}",
                tags: ["follow_up", "queue"]));

            Assert.NotNull(selection);
            Assert.StartsWith("interruption_queue_followup_", selection.Card.Id, StringComparison.Ordinal);
        }

        var fallback = selector.Select(InterruptionContext(
            FeedbackPhase.QueueingFollowUp,
            turnId: "queue-fallback",
            tags: ["follow_up", "queue"]));

        Assert.True(fallback is null || fallback.Card.Id.StartsWith("interruption_", StringComparison.Ordinal), fallback?.Card.Id);
    }

    private static FeedbackSelector CreateSelector(ResponsiveFeedbackOptions? configuredOptions = null)
    {
        var options = Options.Create(configuredOptions ?? new ResponsiveFeedbackOptions
        {
            GlobalCooldownMs = 0,
            SameTextCooldownSeconds = 0,
            MinimumSelectionScore = 0.5,
            MaxImmediateFeedbackPerTurn = 1,
            MaxInterruptionBridgeFeedbackPerInterruption = 1
        });

        return new FeedbackSelector(
            new DefaultFeedbackCardProvider(),
            new FeedbackVectorBuilder(),
            new FeedbackCooldownTracker(options),
            options,
            NullLogger<FeedbackSelector>.Instance);
    }

    private static FeedbackContext Context(
        FeedbackDomain domain,
        FeedbackDurationEstimate duration = FeedbackDurationEstimate.Short,
        bool allowSpeech = true,
        string? turnId = null)
    {
        var id = turnId ?? Guid.NewGuid().ToString("N");
        return new FeedbackContext
        {
            CorrelationId = id,
            TurnId = id,
            Phase = FeedbackPhase.Executing,
            Domain = domain,
            DurationEstimate = duration,
            Confidence = FeedbackConfidence.High,
            IsExternalAction = domain is FeedbackDomain.ExternalApp or FeedbackDomain.WebSearch,
            AllowSpeech = allowSpeech
        };
    }

    private static FeedbackContext InterruptionContext(
        FeedbackPhase phase,
        bool isRecomposition = false,
        string? turnId = null,
        IReadOnlyList<string>? tags = null)
    {
        var id = turnId ?? Guid.NewGuid().ToString("N");
        return new FeedbackContext
        {
            CorrelationId = id,
            TurnId = id,
            Phase = phase,
            Domain = FeedbackDomain.Interruption,
            DurationEstimate = FeedbackDurationEstimate.Short,
            Confidence = FeedbackConfidence.High,
            IsInterruptionFeedback = true,
            IsRecompositionFeedback = isRecomposition,
            Tags = tags ?? Array.Empty<string>(),
            AllowSpeech = true
        };
    }
}
