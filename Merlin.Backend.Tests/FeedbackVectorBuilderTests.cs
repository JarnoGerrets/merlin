using Merlin.Backend.Services.Feedback;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class FeedbackVectorBuilderTests
{
    [Fact]
    public void Build_WhenExecutingExternalApp_IncludesExpectedKeys()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Phase = FeedbackPhase.Executing,
            Domain = FeedbackDomain.ExternalApp,
            DurationEstimate = FeedbackDurationEstimate.Short,
            Confidence = FeedbackConfidence.High,
            IsExternalAction = true
        });

        Assert.Contains("phase.executing", vector.Keys);
        Assert.Contains("domain.external_app", vector.Keys);
        Assert.Contains("duration.short", vector.Keys);
        Assert.Contains("confidence.high", vector.Keys);
        Assert.Contains("action.external", vector.Keys);
    }

    [Fact]
    public void Build_WhenConfirmationNeeded_IncludesConfirmationRisk()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Phase = FeedbackPhase.NeedsConfirmation,
            Domain = FeedbackDomain.Confirmation,
            NeedsConfirmation = true
        });

        Assert.Contains("phase.needs_confirmation", vector.Keys);
        Assert.Contains("domain.confirmation", vector.Keys);
        Assert.Contains("risk.confirmation", vector.Keys);
    }

    [Fact]
    public void Build_WhenVoiceOrb_IncludesInteractionKeys()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            IsVoiceInteraction = true,
            IsOrbClient = true
        });

        Assert.Contains("interaction.voice", vector.Keys);
        Assert.Contains("interaction.orb", vector.Keys);
    }

    [Fact]
    public void Build_WhenUnknownValues_DoesNotThrow()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext());

        Assert.NotNull(vector);
    }

    [Fact]
    public void Build_WhenInterruptionRedirecting_IncludesInterruptionDomainPhaseAndTags()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Domain = FeedbackDomain.Interruption,
            Phase = FeedbackPhase.Redirecting,
            IsInterruptionFeedback = true,
            Tags = ["correction", "redirect"]
        });

        Assert.Contains("domain.interruption", vector.Keys);
        Assert.Contains("phase.redirecting", vector.Keys);
        Assert.Contains("interruption.correction", vector.Keys);
        Assert.Contains("interruption.redirect", vector.Keys);
    }

    [Fact]
    public void Build_WhenInterruptionRecomposing_IncludesRecomposeWaitAndClarificationTags()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Domain = FeedbackDomain.Interruption,
            Phase = FeedbackPhase.RecomposingContinuation,
            IsInterruptionFeedback = true,
            IsRecompositionFeedback = true,
            Tags = ["waiting", "clarification", "side_comment", "additional_context"]
        });

        Assert.Contains("phase.recomposing_continuation", vector.Keys);
        Assert.Contains("interruption.recompose", vector.Keys);
        Assert.Contains("interruption.waiting", vector.Keys);
        Assert.Contains("interruption.clarification", vector.Keys);
        Assert.Contains("interruption.side_comment", vector.Keys);
        Assert.Contains("interruption.additional_context", vector.Keys);
    }

    [Fact]
    public void Build_WhenInterruptionQueueing_IncludesFollowUpAndQueueTags()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Domain = FeedbackDomain.Interruption,
            Phase = FeedbackPhase.QueueingFollowUp,
            IsInterruptionFeedback = true,
            Tags = ["follow_up", "queue"]
        });

        Assert.Contains("phase.queueing_followup", vector.Keys);
        Assert.Contains("interruption.follow_up", vector.Keys);
        Assert.Contains("interruption.queue", vector.Keys);
    }

    [Fact]
    public void Build_WhenInterruptionUnclear_IncludesHandlingAndUnclearTags()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Domain = FeedbackDomain.Interruption,
            Phase = FeedbackPhase.HandlingInterruption,
            IsInterruptionFeedback = true,
            Tags = ["unclear"]
        });

        Assert.Contains("phase.handling_interruption", vector.Keys);
        Assert.Contains("interruption.unclear", vector.Keys);
    }

    [Fact]
    public void Build_WhenNonInterruptionContextHasInterruptionTags_DoesNotProduceInterruptionTagKeys()
    {
        var vector = new FeedbackVectorBuilder().Build(new FeedbackContext
        {
            Domain = FeedbackDomain.Conversation,
            Phase = FeedbackPhase.Executing,
            Tags = ["correction", "redirect", "recompose", "waiting", "follow_up", "queue", "unclear"]
        });

        Assert.DoesNotContain("interruption.correction", vector.Keys);
        Assert.DoesNotContain("interruption.redirect", vector.Keys);
        Assert.DoesNotContain("interruption.recompose", vector.Keys);
        Assert.DoesNotContain("interruption.waiting", vector.Keys);
        Assert.DoesNotContain("interruption.follow_up", vector.Keys);
        Assert.DoesNotContain("interruption.queue", vector.Keys);
        Assert.DoesNotContain("interruption.unclear", vector.Keys);
    }
}
