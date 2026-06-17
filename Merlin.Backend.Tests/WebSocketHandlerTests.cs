using Merlin.Backend.Services;
using Merlin.Backend.WebSocket;
using Merlin.Backend.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class WebSocketHandlerTests
{
    [Fact]
    public async Task ProcessMessageAsync_WhenJsonIsInvalid_ReturnsInvalidJsonErrorCode()
    {
        var runtimeStateService = new RuntimeStateService();
        var handler = new WebSocketHandler(
            new CommandRouter(
                new RuleBasedIntentParser(TestApplicationLaunchOptions.Create()),
                new ToolRegistry([]),
                NullLogger<CommandRouter>.Instance,
                runtimeStateService,
                new NoOpResponsePolisher()),
            new NoOpAssistantSpeechPlaybackService(),
            new SpeechPolicyService(),
            NullLogger<WebSocketHandler>.Instance,
            runtimeStateService,
            new VoiceStreamSessionService(
                new NoOpVoiceTranscriptionService(),
                NullLogger<VoiceStreamSessionService>.Instance));

        var response = await handler.ProcessMessageAsync("{bad json", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Invalid JSON.", response.Message);
        Assert.Equal("INVALID_JSON", response.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(response.CorrelationId));
        Assert.Null(response.ToolName);
        Assert.Null(response.Intent);
    }

    [Fact]
    public void ResponseSpeechText_WhenTranscriptIsEmpty_DoesNotSpeakErrorCode()
    {
        var response = new AssistantResponse
        {
            Success = false,
            Message = "I did not catch that.",
            ErrorCode = "EMPTY_TRANSCRIPT",
            Intent = "voice_stream_transcription",
            ResponseType = "error"
        };

        var speechText = WebSocketHandler.ResponseSpeechText(response);

        Assert.Equal("I did not catch that.", speechText);
        Assert.DoesNotContain("EMPTY_TRANSCRIPT", speechText);
    }

    private sealed class NoOpAssistantSpeechPlaybackService : IAssistantSpeechPlaybackService
    {
        public Task EnqueueAsync(
            string text,
            string? correlationId,
            Func<Merlin.Backend.Models.AssistantVisualEvent, CancellationToken, Task> sendEventAsync,
            string? speechCacheKey,
            bool? isReplayableSpeech,
            CancellationToken cancellationToken,
            Merlin.Backend.Models.SpeechPlaybackItemType itemType = Merlin.Backend.Models.SpeechPlaybackItemType.FinalAnswer,
            bool cancelOnlyBeforePlayback = false)
        {
            return Task.CompletedTask;
        }

        public Task StopCurrentAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task PauseCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResumeCurrentSpeechAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ClearQueueAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpVoiceTranscriptionService : IVoiceTranscriptionService
    {
        public Task<VoiceTranscriptionResponse> TranscribeAsync(
            Stream audioStream,
            string fileExtension,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new VoiceTranscriptionResponse());
        }
    }
}
