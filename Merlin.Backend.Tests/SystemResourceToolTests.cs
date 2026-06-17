using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class SystemResourceToolTests
{
    [Theory]
    [InlineData("system resource current_time")]
    [InlineData("system resource current_date")]
    [InlineData("system resource timezone")]
    public void CanHandle_WhenResourceIsSupported_ReturnsTrue(string command)
    {
        var tool = new SystemResourceTool(new FakeSystemResourceProvider());

        Assert.True(tool.CanHandle(command));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentTimeRequested_ReturnsTime()
    {
        var tool = new SystemResourceTool(new FakeSystemResourceProvider());

        var result = await tool.ExecuteAsync("system resource current_time");

        Assert.True(result.Success);
        Assert.Equal("System Resource", result.ToolName);
        Assert.Equal("system_resource_query", result.Intent);
        Assert.Equal("system_time", result.CapabilityId);
        Assert.Equal("assistant", result.ResponseType);
        Assert.Contains("13:45:30", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentDateRequested_ReturnsDate()
    {
        var tool = new SystemResourceTool(new FakeSystemResourceProvider());

        var result = await tool.ExecuteAsync("system resource current_date");

        Assert.True(result.Success);
        Assert.Equal("system_date", result.CapabilityId);
        Assert.Contains("10-06-2026", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimezoneRequested_ReturnsTimezone()
    {
        var tool = new SystemResourceTool(new FakeSystemResourceProvider());

        var result = await tool.ExecuteAsync("system resource timezone");

        Assert.True(result.Success);
        Assert.Equal("system_timezone", result.CapabilityId);
        Assert.Contains("Test Time", result.Message);
        Assert.Contains("Test/Zone", result.Message);
    }

    private sealed class FakeSystemResourceProvider : ISystemResourceProvider
    {
        public DateTimeOffset GetCurrentLocalTime()
        {
            return new DateTimeOffset(2026, 6, 10, 13, 45, 30, TimeSpan.FromHours(2));
        }

        public DateOnly GetCurrentLocalDate()
        {
            return new DateOnly(2026, 6, 10);
        }

        public TimeZoneInfo GetLocalTimeZone()
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Test/Zone",
                TimeSpan.FromHours(2),
                "Test Time",
                "Test Standard Time");
        }
    }
}
