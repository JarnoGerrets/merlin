using Merlin.Backend.Next.Host;
using Merlin.Backend.Next.Kernel.Requests;
using Merlin.Backend.Next.Kernel.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MerlinNextShadowBridgeTests
{
    [Fact]
    public async Task TryStartShadow_DefaultOptions_DoesNotInvokeRuntime()
    {
        var runtime = new RecordingNextRuntime();
        var bridge = new MerlinNextShadowBridge(
            runtime,
            new TestOptionsMonitor(new MerlinNextRuntimeOptions()),
            NullLogger<MerlinNextShadowBridge>.Instance);

        bridge.TryStartShadow(CreateRequest());
        await Task.Delay(50);

        Assert.Equal(0, runtime.CallCount);
    }

    [Fact]
    public async Task TryStartShadow_WhenEnabledShadowMode_InvokesReadOnlyRuntime()
    {
        var runtime = new RecordingNextRuntime();
        var bridge = new MerlinNextShadowBridge(
            runtime,
            new TestOptionsMonitor(new MerlinNextRuntimeOptions
            {
                Enabled = true,
                Mode = MerlinNextRuntimeMode.Shadow,
                ShadowEnabled = true
            }),
            NullLogger<MerlinNextShadowBridge>.Instance);

        bridge.TryStartShadow(CreateRequest());

        var request = await runtime.WaitForRequestAsync();
        Assert.Equal("request-1", request.RequestId);
        Assert.Equal("backend_idle_voice", request.Source);
        Assert.Equal("open notepad", request.UserText);
    }

    [Fact]
    public async Task RunShadowAsync_ReturnsDisabledExecutionTrace()
    {
        var runtime = new MerlinNextShadowRuntime(NullLogger<MerlinNextShadowRuntime>.Instance);

        var trace = await runtime.RunShadowAsync(CreateRequest());

        Assert.Equal("request-1", trace.RequestId);
        Assert.Equal("backend_idle_voice", trace.Source);
        Assert.Equal("open notepad", trace.NormalizedInputText);
        Assert.Equal("dashboard.main", trace.ActiveSurfaceId);
        Assert.Equal("NoDecision", trace.RoutePrediction);
        Assert.Equal("disabled_shadow_mode", trace.ExecutionDisabledReason);
        Assert.Null(trace.CapabilityId);
    }

    private static MerlinRequest CreateRequest() => new(
        RequestId: "request-1",
        UserText: "open notepad",
        Source: "backend_idle_voice",
        SourceSessionId: "capture-1",
        RequestedSurfaceId: "dashboard.main",
        CreatedAt: DateTimeOffset.UtcNow,
        Metadata: new Dictionary<string, string?>
        {
            ["raw_text"] = "open notepad"
        });

    private sealed class RecordingNextRuntime : IMerlinNextRuntime
    {
        private readonly TaskCompletionSource<MerlinRequest> _requestSeen =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public Task<MerlinNextShadowTrace> RunShadowAsync(
            MerlinRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            _requestSeen.TrySetResult(request);
            return Task.FromResult(new MerlinNextShadowTrace
            {
                RequestId = request.RequestId,
                Source = request.Source,
                NormalizedInputText = request.UserText,
                ExecutionDisabledReason = "disabled_shadow_mode"
            });
        }

        public async Task<MerlinRequest> WaitForRequestAsync()
        {
            var completed = await Task.WhenAny(_requestSeen.Task, Task.Delay(1000));
            if (completed != _requestSeen.Task)
            {
                throw new TimeoutException("Shadow runtime was not invoked.");
            }

            return await _requestSeen.Task;
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<MerlinNextRuntimeOptions>
    {
        public TestOptionsMonitor(MerlinNextRuntimeOptions currentValue)
        {
            CurrentValue = currentValue;
        }

        public MerlinNextRuntimeOptions CurrentValue { get; }

        public MerlinNextRuntimeOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<MerlinNextRuntimeOptions, string?> listener) => null;
    }
}
