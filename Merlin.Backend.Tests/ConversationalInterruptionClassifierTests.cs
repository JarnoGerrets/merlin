using Merlin.Backend.Configuration;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConversationalInterruptionClassifierTests
{
    private readonly ConversationalInterruptionClassifier _classifier = new(
        Options.Create(new InterruptionHandlingOptions()));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_NoiseOrFalsePositive_WhenTranscriptIsEmpty(string transcript)
    {
        var decision = Classify(transcript);

        AssertNoise(decision);
    }

    [Fact]
    public void Classify_NoiseOrFalsePositive_WhenTranscriptConfidenceIsLow()
    {
        var decision = Classify("hello", transcriptConfidence: 0.2);

        AssertNoise(decision);
    }

    [Fact]
    public void Classify_NoiseOrFalsePositive_WhenLikelySelfEcho()
    {
        var decision = Classify("hello", isLikelySelfEcho: true);

        AssertNoise(decision);
    }

    [Fact]
    public void Classify_NoiseOrFalsePositive_WhenNotLikelyUserSpeech()
    {
        var decision = Classify("hello", isLikelyUserSpeech: false);

        AssertNoise(decision);
    }

    [Theory]
    [InlineData("yeah")]
    [InlineData("mhm")]
    public void Classify_Backchannel_ContinuesWithoutResponse(string transcript)
    {
        var decision = Classify(transcript);

        Assert.Equal(ConversationalInterruptionType.Backchannel, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse, decision.Strategy);
        Assert.False(decision.PausePlayback);
        Assert.False(decision.CancelOriginalTurn);
        Assert.True(decision.ResumeRawPlayback);
        Assert.False(decision.DiscardCurrentPartialSentence);
        Assert.False(decision.RequiresDeepInfraClarification);
        Assert.False(decision.RequiresContinuationRecomposition);
    }

    [Theory]
    [InlineData("right")]
    [InlineData("okay")]
    public void Classify_PassiveAgreement_ContinuesWithoutResponse(string transcript)
    {
        var decision = Classify(transcript);

        Assert.Equal(ConversationalInterruptionType.PassiveAgreement, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.ContinueWithoutResponse, decision.Strategy);
        Assert.True(decision.ResumeRawPlayback);
    }

    [Fact]
    public void Classify_LongerBackchannelPhrase_IsNotBackchannel()
    {
        var decision = Classify("yeah but water too");

        Assert.NotEqual(ConversationalInterruptionType.Backchannel, decision.Type);
        Assert.NotEqual(ConversationalInterruptionType.PassiveAgreement, decision.Type);
    }

    [Theory]
    [InlineData("stop", ConversationalInterruptionType.StopRequest)]
    [InlineData("stop talking", ConversationalInterruptionType.StopRequest)]
    [InlineData("shut up", ConversationalInterruptionType.StopRequest)]
    [InlineData("cancel", ConversationalInterruptionType.CancelRequest)]
    [InlineData("never mind", ConversationalInterruptionType.CancelRequest)]
    public void Classify_StopOrCancel_StopsPlayback(string transcript, ConversationalInterruptionType expectedType)
    {
        var decision = Classify(transcript);

        Assert.Equal(expectedType, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.StopPlayback, decision.Strategy);
        Assert.True(decision.PausePlayback);
        Assert.True(decision.CancelOriginalTurn);
        Assert.False(decision.ResumeRawPlayback);
        Assert.False(decision.RequiresDeepInfraClarification);
        Assert.False(decision.RequiresContinuationRecomposition);
    }

    [Fact]
    public void Classify_Correction_ExtractsRewrittenRequest()
    {
        var decision = Classify("no i meant what is the meaning of a wife");

        Assert.Equal(ConversationalInterruptionType.Correction, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.CancelAndRedirect, decision.Strategy);
        Assert.Equal("what is the meaning of a wife", decision.RewrittenUserRequest);
        Assert.True(decision.CancelOriginalTurn);
        Assert.True(decision.RequiresBridgeFeedback);
        Assert.False(decision.RequiresContinuationRecomposition);
    }

    [Fact]
    public void Classify_ActuallyRedirect_ExtractsRequestWithoutInstead()
    {
        var decision = Classify("actually explain water depth instead");

        Assert.True(decision.Type is ConversationalInterruptionType.Redirect or ConversationalInterruptionType.Correction);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.CancelAndRedirect, decision.Strategy);
        Assert.Contains("water depth", decision.RewrittenUserRequest);
    }

    [Fact]
    public void Classify_TalkAboutInstead_IsRedirect()
    {
        var decision = Classify("talk about sunlight instead");

        Assert.Equal(ConversationalInterruptionType.Redirect, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.CancelAndRedirect, decision.Strategy);
        Assert.Equal("talk about sunlight", decision.RewrittenUserRequest);
    }

    [Theory]
    [InlineData("what do you mean by liner", ConversationalInterruptionType.ClarificationQuestion)]
    [InlineData("but the water itself too right", ConversationalInterruptionType.RelatedFollowUpQuestion)]
    [InlineData("The water itself too, right?", ConversationalInterruptionType.RelatedFollowUpQuestion)]
    [InlineData("The water itself, too, right?", ConversationalInterruptionType.RelatedFollowUpQuestion)]
    [InlineData("But the water itself too, right?", ConversationalInterruptionType.RelatedFollowUpQuestion)]
    [InlineData("But the water itself, too, right?", ConversationalInterruptionType.RelatedFollowUpQuestion)]
    [InlineData("what about depth", ConversationalInterruptionType.RelatedFollowUpQuestion)]
    public void Classify_ClarificationOrFollowUp_RecomposesFromCheckpoint(
        string transcript,
        ConversationalInterruptionType expectedType)
    {
        var decision = Classify(transcript);

        Assert.Equal(expectedType, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.ClarifyThenRecomposeFromCheckpoint, decision.Strategy);
        Assert.True(decision.PausePlayback);
        Assert.False(decision.CancelOriginalTurn);
        Assert.False(decision.ResumeRawPlayback);
        Assert.True(decision.RequiresDeepInfraClarification);
        Assert.True(decision.RequiresContinuationRecomposition);
        Assert.True(decision.CanRunContinuationInParallel);
    }

    [Theory]
    [InlineData("well yeah but sunlight too", ConversationalInterruptionType.SideComment)]
    [InlineData("also water depth matters", ConversationalInterruptionType.AdditionalContext)]
    [InlineData("that is only true indoors though", ConversationalInterruptionType.Disagreement)]
    public void Classify_SideCommentOrAdditionalContext_UsesBridgeAndRecomposition(
        string transcript,
        ConversationalInterruptionType expectedType)
    {
        var decision = Classify(transcript);

        Assert.Equal(expectedType, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.LocalBridgeAndRecomposeFromCheckpoint, decision.Strategy);
        Assert.True(decision.PausePlayback);
        Assert.False(decision.CancelOriginalTurn);
        Assert.True(decision.RequiresBridgeFeedback);
        Assert.True(decision.RequiresContinuationRecomposition);
    }

    [Theory]
    [InlineData("after this explain sunlight")]
    [InlineData("can you explain sunlight after this")]
    public void Classify_QueueFollowUp_SetsQueueFlags(string transcript)
    {
        var decision = Classify(transcript);

        Assert.Equal(ConversationalInterruptionType.RelatedFollowUpQuestion, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.QueueFollowUpAfterCurrent, decision.Strategy);
        Assert.True(decision.PausePlayback);
        Assert.False(decision.CancelOriginalTurn);
        Assert.True(decision.QueueAfterCurrentTurn);
        Assert.True(decision.RequiresBridgeFeedback);
        Assert.False(decision.RequiresDeepInfraClarification);
        Assert.False(decision.RequiresContinuationRecomposition);
    }

    [Theory]
    [InlineData("Yeah!", "yeah")]
    [InlineData("  No, I meant wife? ", "no, i meant wife")]
    [InlineData("but the water itself too, right?", "but the water itself too, right")]
    public void Normalize_StabilizesTranscriptText(string transcript, string expected)
    {
        var normalized = ConversationalInterruptionTextNormalizer.Normalize(transcript);

        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void Classify_AmbiguousText_AsksUserToClarifyWithoutModelCall()
    {
        var decision = Classify("sunlight maybe");

        Assert.Equal(ConversationalInterruptionType.Unknown, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.AskUserToClarifyInterruption, decision.Strategy);
        Assert.True(decision.Confidence <= 0.55);
        Assert.True(decision.RequiresBridgeFeedback);
        Assert.True(decision.NeedsUserConfirmation);
        Assert.False(decision.RequiresDeepInfraClarification);
        Assert.False(decision.RequiresContinuationRecomposition);
    }

    private ConversationalInterruptionDecision Classify(
        string transcript,
        double transcriptConfidence = 0.9,
        bool isLikelySelfEcho = false,
        bool isLikelyUserSpeech = true)
    {
        return _classifier.Classify(new ConversationalInterruptionCandidate
        {
            CorrelationId = "correlation-1",
            ActiveTurnId = "turn-1",
            Transcript = transcript,
            TranscriptConfidence = transcriptConfidence,
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(500),
            AssistantWasSpeaking = true,
            IsLikelySelfEcho = isLikelySelfEcho,
            IsLikelyUserSpeech = isLikelyUserSpeech
        });
    }

    private static void AssertNoise(ConversationalInterruptionDecision decision)
    {
        Assert.Equal(ConversationalInterruptionType.NoiseOrFalsePositive, decision.Type);
        Assert.Equal(ConversationalInterruptionHandlingStrategy.IgnoreAndContinue, decision.Strategy);
        Assert.False(decision.PausePlayback);
        Assert.False(decision.CancelOriginalTurn);
        Assert.True(decision.ResumeRawPlayback);
        Assert.False(decision.DiscardCurrentPartialSentence);
        Assert.False(decision.RequiresBridgeFeedback);
        Assert.False(decision.RequiresDeepInfraClarification);
        Assert.False(decision.RequiresContinuationRecomposition);
    }
}
