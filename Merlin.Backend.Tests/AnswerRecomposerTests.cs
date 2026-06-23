using Merlin.Backend.Services.InterruptionIntelligence;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AnswerRecomposerTests
{
    private readonly AnswerRecomposer _recomposer = new();

    [Fact]
    public void BuildClarificationPrompt_IncludesCheckpointContextAndJsonSchema()
    {
        var prompt = _recomposer.BuildClarificationPrompt(CreateClarificationRequest());

        Assert.Contains("Why does pool water look blue?", prompt);
        Assert.Contains("Pool water can look blue for several reasons.", prompt);
        Assert.Contains("Due to the color of the pool li", prompt);
        Assert.Contains("But the water itself too, right?", prompt);
        Assert.Contains("1-2 short sentences", prompt);
        Assert.Contains("Do not restart the original answer", prompt);
        Assert.Contains("Do not continue the full original answer yet", prompt);
        Assert.Contains("Do not continue this partial sentence directly", prompt);
        Assert.Contains("Return strict JSON", prompt);
        Assert.Contains("replyText", prompt);
        Assert.Contains("clarificationContext", prompt);
        Assert.Contains("shouldRecomposeContinuation", prompt);
        Assert.Contains("userQuestionAnswered", prompt);
    }

    [Fact]
    public void BuildContinuationRecompositionPrompt_IncludesClarificationAndAntiRepeatInstructions()
    {
        var prompt = _recomposer.BuildContinuationRecompositionPrompt(CreateContinuationRequest());

        Assert.Contains("Original user question", prompt);
        Assert.Contains("assistant had already spoken", prompt);
        Assert.Contains("last safe completed sentence", prompt);
        Assert.Contains("discarded partial sentence", prompt);
        Assert.Contains("user interruption", prompt);
        Assert.Contains("clarification reply", prompt);
        Assert.Contains("clarification context", prompt);
        Assert.Contains("Avoid repeating what the user already heard", prompt);
        Assert.Contains("Do not restart the answer from the beginning", prompt);
        Assert.Contains("Preserve the original answer's red wire", prompt);
        Assert.Contains("Return strict JSON", prompt);
        Assert.Contains("continuationText", prompt);
        Assert.Contains("includedClarificationContext", prompt);
        Assert.Contains("avoidedRepeatingSpokenContent", prompt);
    }

    [Fact]
    public void BuildPrompts_IncludeOptionalTopicAndPlanWhenProvided()
    {
        var clarificationPrompt = _recomposer.BuildClarificationPrompt(CreateClarificationRequest("pool color"));
        var continuationPrompt = _recomposer.BuildContinuationRecompositionPrompt(CreateContinuationRequest(
            currentTopicLabel: "pool color",
            originalPlanOrIntent: "Explain liner color, water depth, and light scattering."));

        Assert.Contains("pool color", clarificationPrompt);
        Assert.Contains("pool color", continuationPrompt);
        Assert.Contains("Explain liner color, water depth, and light scattering.", continuationPrompt);
    }

    [Fact]
    public void BuildPrompts_OmitOptionalNullSections()
    {
        var continuationPrompt = _recomposer.BuildContinuationRecompositionPrompt(CreateContinuationRequest());

        Assert.DoesNotContain("--- current topic label ---", continuationPrompt);
        Assert.DoesNotContain("--- original plan or intent ---", continuationPrompt);
        Assert.DoesNotContain("null", continuationPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildClarificationPrompt_PreservesQuotesAndNewlinesAsContext()
    {
        var prompt = _recomposer.BuildClarificationPrompt(new ClarificationRequest
        {
            OriginalUserQuestion = "Why does pool water look blue?",
            SpokenAnswerSoFar = "Pool water can look blue.",
            LastCompletedSentence = "Pool water can look blue.",
            DiscardedPartialSentence = "Due to the liner",
            UserInterruption = "He said \"water itself\", right?\nAlso, what about depth?"
        });

        Assert.Contains("He said \"water itself\", right?", prompt);
        Assert.Contains("Also, what about depth?", prompt);
        Assert.Contains("--- user interruption ---", prompt);
        Assert.Contains("--- end user interruption ---", prompt);
    }

    [Fact]
    public void ParseClarificationResult_ParsesPureJson()
    {
        var result = _recomposer.ParseClarificationResult("""
            {
              "replyText": "Yes, exactly. The water itself can affect the color, especially in deeper pools.",
              "clarificationContext": "The user pointed out that water depth and optical properties affect perceived pool color.",
              "shouldRecomposeContinuation": true,
              "userQuestionAnswered": true
            }
            """);

        Assert.Equal("Yes, exactly. The water itself can affect the color, especially in deeper pools.", result.ReplyText);
        Assert.Equal("The user pointed out that water depth and optical properties affect perceived pool color.", result.ClarificationContext);
        Assert.True(result.ShouldRecomposeContinuation);
        Assert.True(result.UserQuestionAnswered);
    }

    [Fact]
    public void ParseClarificationResult_ParsesFencedJson()
    {
        var result = _recomposer.ParseClarificationResult("""
            ```json
            {
              "replyText": "Yes, exactly.",
              "clarificationContext": "Water itself matters.",
              "shouldRecomposeContinuation": true,
              "userQuestionAnswered": true
            }
            ```
            """);

        Assert.Equal("Yes, exactly.", result.ReplyText);
        Assert.Equal("Water itself matters.", result.ClarificationContext);
        Assert.True(result.ShouldRecomposeContinuation);
        Assert.True(result.UserQuestionAnswered);
    }

    [Fact]
    public void ParseClarificationResult_DefaultsMissingBooleansToTrue()
    {
        var result = _recomposer.ParseClarificationResult("""
            {
              "replyText": "Yes.",
              "clarificationContext": ""
            }
            """);

        Assert.True(result.ShouldRecomposeContinuation);
        Assert.True(result.UserQuestionAnswered);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{")]
    public void ParseClarificationResult_InvalidJsonThrows(string output)
    {
        Assert.Throws<InvalidOperationException>(() => _recomposer.ParseClarificationResult(output));
    }

    [Fact]
    public void ParseClarificationResult_EmptyReplyThrows()
    {
        Assert.Throws<InvalidOperationException>(() => _recomposer.ParseClarificationResult("""
            {
              "replyText": "",
              "clarificationContext": "x"
            }
            """));
    }

    [Fact]
    public void ParseContinuationRecompositionResult_ParsesPureJson()
    {
        var result = _recomposer.ParseContinuationRecompositionResult("""
            {
              "continuationText": "So besides the liner, the water itself can also affect the color as depth increases.",
              "includedClarificationContext": true,
              "avoidedRepeatingSpokenContent": true
            }
            """);

        Assert.Equal("So besides the liner, the water itself can also affect the color as depth increases.", result.ContinuationText);
        Assert.True(result.IncludedClarificationContext);
        Assert.True(result.AvoidedRepeatingSpokenContent);
    }

    [Fact]
    public void ParseContinuationRecompositionResult_ParsesFencedJson()
    {
        var result = _recomposer.ParseContinuationRecompositionResult("""
            Here is the continuation:
            ```json
            {
              "continuationText": "So the water itself is the next piece.",
              "includedClarificationContext": true,
              "avoidedRepeatingSpokenContent": true
            }
            ```
            """);

        Assert.Equal("So the water itself is the next piece.", result.ContinuationText);
        Assert.True(result.IncludedClarificationContext);
        Assert.True(result.AvoidedRepeatingSpokenContent);
    }

    [Fact]
    public void ParseContinuationRecompositionResult_DefaultsMissingBooleansToFalse()
    {
        var result = _recomposer.ParseContinuationRecompositionResult("""
            {
              "continuationText": "Continuing from there."
            }
            """);

        Assert.False(result.IncludedClarificationContext);
        Assert.False(result.AvoidedRepeatingSpokenContent);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"continuationText\":\"\"}")]
    public void ParseContinuationRecompositionResult_InvalidOrEmptyThrows(string output)
    {
        Assert.Throws<InvalidOperationException>(() => _recomposer.ParseContinuationRecompositionResult(output));
    }

    private static ClarificationRequest CreateClarificationRequest(string? withTopic = null)
    {
        return new ClarificationRequest
        {
            OriginalUserQuestion = "Why does pool water look blue?",
            SpokenAnswerSoFar = "Pool water can look blue for several reasons. Due to the color of the pool li",
            LastCompletedSentence = "Pool water can look blue for several reasons.",
            DiscardedPartialSentence = "Due to the color of the pool li",
            UserInterruption = "But the water itself too, right?",
            CurrentTopicLabel = withTopic
        };
    }

    private static ContinuationRecompositionRequest CreateContinuationRequest(
        string? currentTopicLabel = null,
        string? originalPlanOrIntent = null)
    {
        return new ContinuationRecompositionRequest
        {
            OriginalUserQuestion = "Why does pool water look blue?",
            SpokenAnswerSoFar = "Pool water can look blue for several reasons. Due to the color of the pool li",
            LastCompletedSentence = "Pool water can look blue for several reasons.",
            DiscardedPartialSentence = "Due to the color of the pool li",
            UserInterruption = "But the water itself too, right?",
            ClarificationReply = "Yes, exactly. The water itself can affect the color too.",
            ClarificationContext = "Water depth and optical properties affect perceived pool color.",
            CurrentTopicLabel = currentTopicLabel,
            OriginalPlanOrIntent = originalPlanOrIntent
        };
    }
}
