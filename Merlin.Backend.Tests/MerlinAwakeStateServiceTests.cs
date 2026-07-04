using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MerlinAwakeStateServiceTests
{
    [Theory]
    [InlineData("Merlin, are you awake?")]
    [InlineData("Hey Merlin, are you there?")]
    [InlineData("Hi Merlin you awake")]
    [InlineData("hello, are you listening merlin")]
    [InlineData("wake up Merlin")]
    public void IsWakePhrase_AcceptsNaturalWakeVariants(string transcript)
    {
        Assert.True(MerlinAwakeStateService.IsWakePhrase(transcript));
    }

    [Theory]
    [InlineData("show chat")]
    [InlineData("Hey Merlin, show chat")]
    [InlineData("tell me about Merlin")]
    [InlineData("are you going to open chat")]
    public void IsWakePhrase_RejectsNormalCommandsAndMentions(string transcript)
    {
        Assert.False(MerlinAwakeStateService.IsWakePhrase(transcript));
    }

    [Theory]
    [InlineData("Merlin, go to sleep")]
    [InlineData("Hey Merlin, go back to sleep")]
    [InlineData("you can sleep now")]
    [InlineData("stand down Merlin")]
    [InlineData("Merlin standby")]
    public void IsSleepPhrase_AcceptsNaturalSleepVariants(string transcript)
    {
        Assert.True(MerlinAwakeStateService.IsSleepPhrase(transcript));
    }

    [Theory]
    [InlineData("sleep timer")]
    [InlineData("show me sleep settings")]
    [InlineData("tell me about standby mode")]
    public void IsSleepPhrase_RejectsNormalCommandsAndMentions(string transcript)
    {
        Assert.False(MerlinAwakeStateService.IsSleepPhrase(transcript));
    }

    [Fact]
    public void EvaluateVoiceActivity_WhenSleeping_IgnoresNonWakeCommand()
    {
        var service = new MerlinAwakeStateService();

        var result = service.EvaluateVoiceActivity("show chat");

        Assert.Equal(MerlinAwakeGateDecision.IgnoredWhileSleeping, result.Decision);
        Assert.False(result.ShouldAllow);
        Assert.False(result.IsWakePhrase);
    }

    [Fact]
    public void EvaluateVoiceActivity_AfterWake_AllowsUnprefixedCommand()
    {
        var service = new MerlinAwakeStateService();

        var wake = service.EvaluateVoiceActivity("Hey Merlin, are you awake?");
        var command = service.EvaluateVoiceActivity("show chat");

        Assert.Equal(MerlinAwakeGateDecision.WakePhraseAccepted, wake.Decision);
        Assert.Equal(MerlinAwakeGateDecision.AwakeActivityAccepted, command.Decision);
        Assert.True(command.ShouldAllow);
        Assert.False(command.IsWakePhrase);
    }

    [Fact]
    public void EvaluateVoiceActivity_SleepPhrase_ReturnsToSleeping()
    {
        var service = new MerlinAwakeStateService();

        service.EvaluateVoiceActivity("Hey Merlin, are you awake?");
        var sleep = service.EvaluateVoiceActivity("go to sleep");
        var command = service.EvaluateVoiceActivity("show chat");

        Assert.Equal(MerlinAwakeGateDecision.SleepPhraseAccepted, sleep.Decision);
        Assert.True(sleep.ShouldAllow);
        Assert.True(sleep.IsSleepPhrase);
        Assert.Equal(MerlinAwakeGateDecision.IgnoredWhileSleeping, command.Decision);
        Assert.False(command.ShouldAllow);
    }

    [Fact]
    public void EvaluateVoiceActivity_WhenIdlePastTimeout_ReturnsToSleeping()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-04T10:00:00Z"));
        var service = new MerlinAwakeStateService(
            timeProvider: time,
            awakeTimeout: TimeSpan.FromMinutes(10));

        service.EvaluateVoiceActivity("Merlin are you there");
        time.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));
        var command = service.EvaluateVoiceActivity("show chat");

        Assert.Equal(MerlinAwakeGateDecision.IgnoredWhileSleeping, command.Decision);
        Assert.False(command.ShouldAllow);
    }

    [Fact]
    public void TouchActivity_ExtendsAwakeWindowAfterAcceptedCommand()
    {
        var time = new ManualTimeProvider(DateTimeOffset.Parse("2026-07-04T10:00:00Z"));
        var service = new MerlinAwakeStateService(
            timeProvider: time,
            awakeTimeout: TimeSpan.FromMinutes(10));

        service.EvaluateVoiceActivity("Merlin are you awake");
        time.Advance(TimeSpan.FromMinutes(9));
        service.EvaluateVoiceActivity("show chat");
        service.TouchActivity();
        time.Advance(TimeSpan.FromMinutes(9));
        var command = service.EvaluateVoiceActivity("hide chat");

        Assert.Equal(MerlinAwakeGateDecision.AwakeActivityAccepted, command.Decision);
        Assert.True(command.ShouldAllow);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }
}
