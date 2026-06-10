using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LocalSystemResourceProviderTests
{
    [Fact]
    public void GetCurrentLocalTime_ReturnsLocalTime()
    {
        var provider = new LocalSystemResourceProvider();

        var currentTime = provider.GetCurrentLocalTime();

        Assert.True(currentTime <= DateTimeOffset.Now.AddSeconds(1));
        Assert.True(currentTime >= DateTimeOffset.Now.AddMinutes(-1));
    }

    [Fact]
    public void GetCurrentLocalDate_ReturnsLocalDate()
    {
        var provider = new LocalSystemResourceProvider();

        var currentDate = provider.GetCurrentLocalDate();

        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), currentDate);
    }

    [Fact]
    public void GetLocalTimeZone_ReturnsLocalTimeZone()
    {
        var provider = new LocalSystemResourceProvider();

        var timeZone = provider.GetLocalTimeZone();

        Assert.Equal(TimeZoneInfo.Local.Id, timeZone.Id);
    }
}
