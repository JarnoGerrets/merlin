using Merlin.Backend.Services.Feedback;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class InterruptionFeedbackAdapterTests
{
    [Fact]
    public void CreateBridgeContext_WhenCorrectionRequest_MapsToRedirectingInterruptionContext()
    {
        var context = CreateContext(new InterruptionFeedbackRequest
        {
            CorrelationId = "correlation-1",
            TurnId = "turn-1",
            InterruptionType = "correction",
            Strategy = "redirect",
            IsRedirectOrCorrection = true
        });

        Assert.Equal(FeedbackDomain.Interruption, context.Domain);
        Assert.Equal(FeedbackPhase.Redirecting, context.Phase);
        Assert.True(context.IsInterruptionFeedback);
        Assert.Contains("interruption", context.Tags);
        Assert.Contains("correction", context.Tags);
        Assert.Contains("redirect", context.Tags);
    }

    [Fact]
    public void CreateBridgeContext_WhenRecompositionRequest_MapsToRecomposingContinuation()
    {
        var context = CreateContext(new InterruptionFeedbackRequest
        {
            IsRecompositionFeedback = true
        });

        Assert.Equal(FeedbackPhase.RecomposingContinuation, context.Phase);
        Assert.True(context.IsRecompositionFeedback);
        Assert.Contains("interruption", context.Tags);
        Assert.Contains("recompose", context.Tags);
    }

    [Fact]
    public void CreateBridgeContext_WhenWaitBridgeRequest_AddsWaitingTag()
    {
        var context = CreateContext(new InterruptionFeedbackRequest
        {
            IsWaitBridge = true
        });

        Assert.Equal(FeedbackPhase.RecomposingContinuation, context.Phase);
        Assert.Contains("waiting", context.Tags);
        Assert.Contains("recompose", context.Tags);
    }

    [Fact]
    public void CreateBridgeContext_WhenQueueFollowUpRequest_MapsToQueueingFollowUp()
    {
        var context = CreateContext(new InterruptionFeedbackRequest
        {
            IsQueueFollowUp = true
        });

        Assert.Equal(FeedbackPhase.QueueingFollowUp, context.Phase);
        Assert.Contains("follow_up", context.Tags);
        Assert.Contains("queue", context.Tags);
    }

    [Fact]
    public void CreateBridgeContext_WhenUnclearRequest_MapsToHandlingInterruption()
    {
        var context = CreateContext(new InterruptionFeedbackRequest
        {
            IsUnclear = true
        });

        Assert.Equal(FeedbackPhase.HandlingInterruption, context.Phase);
        Assert.Contains("unclear", context.Tags);
        Assert.Equal(FeedbackUrgency.Normal, context.Urgency);
    }

    [Fact]
    public void CreateBridgeContext_DoesNotCreateCardTextOrRawTranscript()
    {
        var context = CreateContext(new InterruptionFeedbackRequest
        {
            CorrelationId = "correlation-raw",
            TurnId = "turn-raw",
            Tags = ["additional context"]
        });

        Assert.Empty(context.RawUserText);
        Assert.Empty(context.NormalizedUserText);
        Assert.Empty(context.TargetName ?? string.Empty);
        Assert.Contains("additional_context", context.Tags);
    }

    private static FeedbackContext CreateContext(InterruptionFeedbackRequest request)
    {
        return new InterruptionFeedbackAdapter().CreateBridgeContext(request);
    }
}
