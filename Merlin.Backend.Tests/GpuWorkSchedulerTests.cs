using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class GpuWorkSchedulerTests
{
    [Fact]
    public async Task FlagDisabled_InterruptionSttWaitsForActiveTts()
    {
        var scheduler = CreateScheduler(enabled: false);
        var ttsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sttEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var ttsTask = scheduler.RunAsync(
            "ChatterboxTtsChunk",
            GpuWorkPriority.Low,
            async token =>
            {
                ttsEntered.SetResult();
                await releaseTts.Task.WaitAsync(token);
                return true;
            },
            CancellationToken.None);

        Assert.True(await WaitForAsync(ttsEntered.Task));

        var sttTask = scheduler.RunAsync(
            "InterruptionStt",
            GpuWorkPriority.High,
            _ =>
            {
                sttEntered.SetResult();
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.False(await WaitForAsync(sttEntered.Task, milliseconds: 120));

        releaseTts.SetResult();

        Assert.True(await WaitForAsync(sttEntered.Task));
        await Task.WhenAll(ttsTask, sttTask);
    }

    [Fact]
    public async Task FlagEnabled_InterruptionSttMayStartWhileTtsActive()
    {
        var scheduler = CreateScheduler(enabled: true);
        var ttsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sttEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var ttsTask = scheduler.RunAsync(
            "ChatterboxTtsChunk",
            GpuWorkPriority.Low,
            async token =>
            {
                ttsEntered.SetResult();
                await releaseTts.Task.WaitAsync(token);
                return true;
            },
            CancellationToken.None);

        Assert.True(await WaitForAsync(ttsEntered.Task));

        var sttTask = scheduler.RunAsync(
            "InterruptionStt",
            GpuWorkPriority.High,
            _ =>
            {
                sttEntered.SetResult();
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.True(await WaitForAsync(sttEntered.Task));

        releaseTts.SetResult();
        await Task.WhenAll(ttsTask, sttTask);
    }

    [Fact]
    public async Task FlagEnabled_NormalSttStillWaitsForActiveTts()
    {
        var scheduler = CreateScheduler(enabled: true);
        var ttsEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseTts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var normalSttEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var ttsTask = scheduler.RunAsync(
            "ChatterboxTtsChunk",
            GpuWorkPriority.Low,
            async token =>
            {
                ttsEntered.SetResult();
                await releaseTts.Task.WaitAsync(token);
                return true;
            },
            CancellationToken.None);

        Assert.True(await WaitForAsync(ttsEntered.Task));

        var normalSttTask = scheduler.RunAsync(
            "NormalStt",
            GpuWorkPriority.Medium,
            _ =>
            {
                normalSttEntered.SetResult();
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.False(await WaitForAsync(normalSttEntered.Task, milliseconds: 120));

        releaseTts.SetResult();

        Assert.True(await WaitForAsync(normalSttEntered.Task));
        await Task.WhenAll(ttsTask, normalSttTask);
    }

    [Fact]
    public async Task FlagEnabled_MultipleInterruptionSttJobsDoNotOverlap()
    {
        var scheduler = CreateScheduler(enabled: true);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = scheduler.RunAsync(
            "InterruptionStt",
            GpuWorkPriority.High,
            async token =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(token);
                return true;
            },
            CancellationToken.None);

        Assert.True(await WaitForAsync(firstEntered.Task));

        var secondTask = scheduler.RunAsync(
            "InterruptionStt",
            GpuWorkPriority.High,
            _ =>
            {
                secondEntered.SetResult();
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.False(await WaitForAsync(secondEntered.Task, milliseconds: 120));

        releaseFirst.SetResult();

        Assert.True(await WaitForAsync(secondEntered.Task));
        await Task.WhenAll(firstTask, secondTask);
    }

    [Fact]
    public async Task FlagEnabled_MultipleTtsJobsDoNotOverlap()
    {
        var scheduler = CreateScheduler(enabled: true);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var firstTask = scheduler.RunAsync(
            "ChatterboxTtsChunk",
            GpuWorkPriority.Low,
            async token =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(token);
                return true;
            },
            CancellationToken.None);

        Assert.True(await WaitForAsync(firstEntered.Task));

        var secondTask = scheduler.RunAsync(
            "ChatterboxTtsChunk",
            GpuWorkPriority.Low,
            _ =>
            {
                secondEntered.SetResult();
                return Task.FromResult(true);
            },
            CancellationToken.None);

        Assert.False(await WaitForAsync(secondEntered.Task, milliseconds: 120));

        releaseFirst.SetResult();

        Assert.True(await WaitForAsync(secondEntered.Task));
        await Task.WhenAll(firstTask, secondTask);
    }

    private static GpuWorkScheduler CreateScheduler(bool enabled) =>
        new(
            NullLogger<GpuWorkScheduler>.Instance,
            Options.Create(new GpuSchedulingOptions
            {
                EnableConcurrentInterruptionSttDuringTts = enabled
            }));

    private static async Task<bool> WaitForAsync(Task task, int milliseconds = 1000)
    {
        var completed = await Task.WhenAny(task, Task.Delay(milliseconds));
        return completed == task;
    }
}
