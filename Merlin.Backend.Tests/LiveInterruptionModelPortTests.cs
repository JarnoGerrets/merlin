using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Merlin.Backend.Services.InterruptionIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LiveInterruptionModelPortTests
{
    [Fact]
    public async Task GenerateClarificationAsync_WhenLiveModelCallsDisabled_DoesNotCallChatService()
    {
        var chat = new FakeLocalAIChatService();
        var port = CreatePort(chat, enableLiveModelCalls: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.GenerateClarificationAsync(ClarificationRequest()));

        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task GenerateClarificationAsync_WhenClarificationCallsDisabled_DoesNotCallChatService()
    {
        var chat = new FakeLocalAIChatService();
        var port = CreatePort(chat, enableClarificationCalls: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.GenerateClarificationAsync(ClarificationRequest()));

        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task GenerateContinuationAsync_WhenContinuationDisabled_DoesNotCallChatService()
    {
        var chat = new FakeLocalAIChatService();
        var port = CreatePort(chat, enableContinuationRecomposition: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.GenerateContinuationAsync(ContinuationRequest()));

        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task GenerateClarificationAsync_ParsesModelJson()
    {
        var chat = new FakeLocalAIChatService
        {
            Result = new LocalAIChatResult
            {
                Success = true,
                Message = """
                    {
                      "replyText": "Yes, exactly.",
                      "clarificationContext": "Water itself matters.",
                      "shouldRecomposeContinuation": true,
                      "userQuestionAnswered": true
                    }
                    """
            }
        };
        var port = CreatePort(chat);

        var result = await port.GenerateClarificationAsync(ClarificationRequest());

        Assert.Equal("Yes, exactly.", result.ReplyText);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task GenerateClarificationAsync_InvalidJsonThrows()
    {
        var chat = new FakeLocalAIChatService
        {
            Result = new LocalAIChatResult
            {
                Success = true,
                Message = "not json"
            }
        };
        var port = CreatePort(chat);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            port.GenerateClarificationAsync(ClarificationRequest()));

        Assert.Equal(1, chat.CallCount);
    }

    private static LiveInterruptionModelPort CreatePort(
        FakeLocalAIChatService chat,
        bool enableLiveModelCalls = true,
        bool enableClarificationCalls = true,
        bool enableContinuationRecomposition = true) =>
        new(
            chat,
            new AnswerRecomposer(),
            Options.Create(new InterruptionHandlingOptions
            {
                EnableLiveModelCalls = enableLiveModelCalls,
                EnableClarificationCalls = enableClarificationCalls,
                EnableContinuationRecomposition = enableContinuationRecomposition
            }),
            NullLogger<LiveInterruptionModelPort>.Instance);

    private static ClarificationRequest ClarificationRequest() => new()
    {
        OriginalUserQuestion = "Why does pool water look blue?",
        SpokenAnswerSoFar = "Pool water can look blue.",
        LastCompletedSentence = "Pool water can look blue.",
        UserInterruption = "But the water itself too right?"
    };

    private static ContinuationRecompositionRequest ContinuationRequest() => new()
    {
        OriginalUserQuestion = "Why does pool water look blue?",
        SpokenAnswerSoFar = "Pool water can look blue.",
        LastCompletedSentence = "Pool water can look blue.",
        UserInterruption = "But the water itself too right?",
        ClarificationReply = "Yes.",
        ClarificationContext = "Water itself matters."
    };

    private sealed class FakeLocalAIChatService : ILocalAIChatService
    {
        public int CallCount { get; private set; }

        public LocalAIChatResult Result { get; set; } = new()
        {
            Success = true,
            Message = """
                {
                  "continuationText": "Continuing naturally.",
                  "includedClarificationContext": true,
                  "avoidedRepeatingSpokenContent": true
                }
                """
        };

        public Task<LocalAIChatResult> GenerateResponseAsync(
            string message,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }
}
