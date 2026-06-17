using Merlin.Backend.Models;
using Merlin.Backend.Services.IntentRouting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MerlinIntentRouterTests
{
    [Theory]
    [InlineData("what time is it", "system.get_time")]
    [InlineData("what's the time", "system.get_time")]
    [InlineData("tell me the time", "system.get_time")]
    [InlineData("check the clock", "system.get_time")]
    [InlineData("what date is it today", "system.get_date")]
    [InlineData("what timezone are we in", "system.get_timezone")]
    [InlineData("which timezone are we using", "system.get_timezone")]
    [InlineData("what is my timezone", "system.get_timezone")]
    public async Task RouteAsync_WhenCurrentTimeDateOrTimezoneRequested_SelectsSystemCapability(
        string message,
        string expectedCapabilityId)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.True(decision.ShouldExecuteTool);
        Assert.Equal(expectedCapabilityId, decision.CapabilityId);
    }

    [Theory]
    [InlineData("remember the time i went backpacking? what gear would be best now?")]
    [InlineData("what is the time complexity of this algorithm?")]
    [InlineData("i had a great time yesterday")]
    [InlineData("last time we talked about merlin")]
    public async Task RouteAsync_WhenTimeIsPersonalOrAbstract_DoesNotSelectSystemTime(string message)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.NotEqual("system.get_time", decision.CapabilityId);
    }

    [Theory]
    [InlineData("what is the volume of a cylinder")]
    [InlineData("i have a memory of backpacking")]
    public async Task RouteAsync_WhenAudioOrMemoryTermsAreNonSystemContext_DoesNotSelectWrongLocalTool(string message)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.DoesNotContain(decision.CapabilityId ?? string.Empty, new[] { "audio.get_volume", "system.get_memory" });
    }

    [Theory]
    [InlineData("how much memory do i have left", "system.get_memory")]
    [InlineData("how much ram is available", "system.get_memory")]
    [InlineData("what is my cpu usage", "system.get_cpu")]
    [InlineData("how much disk space do i have", "system.get_disk")]
    public async Task RouteAsync_WhenSystemResourcesAreRequested_SelectsSystemResourceCapability(
        string message,
        string expectedCapabilityId)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.Equal(expectedCapabilityId, decision.CapabilityId);
    }

    [Theory]
    [InlineData("set volume to 30", "audio.set_volume")]
    [InlineData("mute the sound", "audio.mute")]
    [InlineData("unmute", "audio.unmute")]
    [InlineData("make it louder", "audio.set_volume")]
    public async Task RouteAsync_WhenAudioControlRequested_SelectsAudioCapability(
        string message,
        string expectedCapabilityId)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.Equal(expectedCapabilityId, decision.CapabilityId);
    }

    [Theory]
    [InlineData("open spotify", "app.open")]
    [InlineData("launch chrome", "app.open")]
    [InlineData("close spotify", "app.close")]
    [InlineData("can you open paint", "app.open")]
    [InlineData("please open paint", "app.open")]
    public async Task RouteAsync_WhenAppControlRequested_SelectsAppCapability(
        string message,
        string expectedCapabilityId)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.Equal(expectedCapabilityId, decision.CapabilityId);
    }

    [Theory]
    [InlineData("open facebook.com")]
    [InlineData("please open facebook.com")]
    [InlineData("can you open facebook.com")]
    [InlineData("please open facebook.com for me")]
    [InlineData("go to facebook.com")]
    [InlineData("pull up facebook.com")]
    [InlineData("can you open facebook in the browser")]
    public async Task RouteAsync_WhenUrlNavigationRequested_SelectsUrlOpenCapability(string message)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.True(decision.ShouldExecuteTool);
        Assert.Equal("url.open", decision.CapabilityId);
    }

    [Theory]
    [InlineData("stop", "assistant.stop")]
    [InlineData("stop talking", "assistant.stop")]
    [InlineData("cancel", "assistant.cancel")]
    [InlineData("never mind", "assistant.cancel")]
    public async Task RouteAsync_WhenEmergencyCommandRequested_RoutesImmediately(
        string message,
        string expectedCapabilityId)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.True(decision.ShouldExecuteTool);
        Assert.Equal(expectedCapabilityId, decision.CapabilityId);
        Assert.Equal(IntentDomain.ConversationControl, decision.Domain);
    }

    [Theory]
    [InlineData("explain dependency injection in simple terms")]
    [InlineData("help me design a better orb animation system")]
    public async Task RouteAsync_WhenGeneralChatRequested_SelectsDeepInfraFallback(string message)
    {
        var decision = await CreateRouter().RouteAsync(message);

        Assert.False(decision.ShouldExecuteTool);
        Assert.Equal("no_tool", decision.CapabilityId);
        Assert.Equal(IntentDomain.GeneralChat, decision.Domain);
    }

    [Fact]
    public void Normalize_ExpandsContractionsAndTimezoneVariants()
    {
        var input = new TextNormalizer().Normalize("  What's our time-zone, tz?  ");

        Assert.Equal("What's our time-zone, tz?", input.OriginalText.Trim());
        Assert.Equal("what is our timezone timezone", input.Text);
    }

    internal static MerlinIntentRouter CreateRouter()
    {
        return new MerlinIntentRouter(
            new TextNormalizer(),
            new EmergencyIntentRouter(),
            new DomainRouter(),
            new CapabilityRegistry(),
            new CapabilityRouter(),
            new DeterministicIntentClassifier(),
            NullLogger<MerlinIntentRouter>.Instance);
    }
}
