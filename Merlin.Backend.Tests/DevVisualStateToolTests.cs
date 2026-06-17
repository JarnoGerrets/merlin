using Merlin.Backend.Tools;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class DevVisualStateToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenStateCommandIncludesDuration_ReturnsSingleStepFlow()
    {
        var tool = new DevVisualStateTool();

        var result = await tool.ExecuteAsync("please activate thinking state for 30 seconds");

        Assert.True(result.Success);
        Assert.Equal("dev_visual", result.ResponseType);
        Assert.NotNull(result.DevVisualFlow);
        var step = Assert.Single(result.DevVisualFlow);
        Assert.Equal("thinking", step.State);
        Assert.Equal(30, step.DurationSeconds);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFlowCommandIsUsed_ReturnsOrderedSteps()
    {
        var tool = new DevVisualStateTool();

        var result = await tool.ExecuteAsync("run dev flow thinking to speaking to error for 2 seconds each");

        Assert.True(result.Success);
        Assert.NotNull(result.DevVisualFlow);
        Assert.Collection(
            result.DevVisualFlow,
            step =>
            {
                Assert.Equal("thinking", step.State);
                Assert.Equal(2, step.DurationSeconds);
            },
            step =>
            {
                Assert.Equal("speaking", step.State);
                Assert.Equal(2, step.DurationSeconds);
            },
            step =>
            {
                Assert.Equal("error", step.State);
                Assert.Equal(2, step.DurationSeconds);
            });
    }
}
