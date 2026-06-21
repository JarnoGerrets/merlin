using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SpeechPolicyServiceTests
{
    [Theory]
    [InlineData(true, "assistant")]
    [InlineData(false, "confirmation")]
    [InlineData(false, "error")]
    [InlineData(false, "limitation")]
    [InlineData(false, "safety")]
    public void Decide_WhenVoiceOrbResponseIsSpeakable_QueuesSpeech(bool success, string responseType)
    {
        var service = new SpeechPolicyService();

        var decision = service.Decide(
            new AssistantRequest
            {
                InteractionSource = "voice",
                ClientMode = "orb"
            },
            new AssistantResponse
            {
                Success = success,
                ResponseType = responseType,
                Message = "Speak this."
            });

        Assert.True(decision.ShouldSpeak);
        Assert.True(decision.ShouldQueue);
    }

    [Fact]
    public void Decide_WhenVoiceStreamOrbResponseIsSpeakable_QueuesSpeech()
    {
        var service = new SpeechPolicyService();

        var decision = service.Decide(
            new AssistantRequest
            {
                InteractionSource = "voice_stream",
                ClientMode = "orb"
            },
            new AssistantResponse
            {
                Success = true,
                ResponseType = "assistant",
                Message = "Speak this."
            });

        Assert.True(decision.ShouldSpeak);
        Assert.True(decision.ShouldQueue);
    }

    [Fact]
    public void Decide_WhenBackendIdleVoiceOrbResponseIsSpeakable_QueuesSpeech()
    {
        var service = new SpeechPolicyService();

        var decision = service.Decide(
            new AssistantRequest
            {
                InteractionSource = "backend_idle_voice",
                ClientMode = "orb"
            },
            new AssistantResponse
            {
                Success = true,
                ResponseType = "assistant",
                Message = "Speak this."
            });

        Assert.True(decision.ShouldSpeak);
        Assert.True(decision.ShouldQueue);
    }

    [Fact]
    public void Decide_WhenTextChatResponse_DoesNotQueueSpeech()
    {
        var service = new SpeechPolicyService();

        var decision = service.Decide(
            new AssistantRequest
            {
                InteractionSource = "text",
                ClientMode = "chat"
            },
            new AssistantResponse
            {
                Success = true,
                ResponseType = "assistant",
                Message = "Do not speak this."
            });

        Assert.False(decision.ShouldSpeak);
        Assert.False(decision.ShouldQueue);
    }
}
