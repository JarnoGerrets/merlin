using Merlin.Backend.Configuration;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LocalAIHealthServiceTests
{
    [Fact]
    public void Constructor_WhenLocalAIIsDisabled_MarksDisabled()
    {
        var service = CreateService(new FakeLocalAIClient(), enabled: false);

        Assert.False(service.IsEnabled);
        Assert.False(service.IsAvailable);
        Assert.Null(service.LastError);
        Assert.Null(service.LastLatencyMs);
    }

    [Fact]
    public async Task WarmupAsync_WhenClientSucceeds_MarksAvailable()
    {
        var service = CreateService(new FakeLocalAIClient("ok"), enabled: true);

        await service.WarmupAsync();

        Assert.True(service.IsEnabled);
        Assert.True(service.IsAvailable);
        Assert.Null(service.LastError);
        Assert.NotNull(service.LastWarmupUtc);
        Assert.NotNull(service.LastLatencyMs);
    }

    [Fact]
    public async Task WarmupAsync_WhenClientFails_MarksUnavailable()
    {
        var service = CreateService(
            new FakeLocalAIClient(exception: new InvalidOperationException("offline")),
            enabled: true);

        await service.WarmupAsync();

        Assert.True(service.IsEnabled);
        Assert.False(service.IsAvailable);
        Assert.Equal("offline", service.LastError);
        Assert.NotNull(service.LastWarmupUtc);
        Assert.NotNull(service.LastLatencyMs);
    }

    [Fact]
    public async Task WarmupAsync_WhenCallerCancels_RethrowsWithoutMarkingUnavailable()
    {
        var service = CreateService(new FakeLocalAIClient("unused"), enabled: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.WarmupAsync(cancellation.Token));

        Assert.False(service.IsAvailable);
        Assert.Null(service.LastError);
        Assert.Null(service.LastWarmupUtc);
        Assert.Null(service.LastLatencyMs);
    }

    private static LocalAIHealthService CreateService(
        ILocalAIClient client,
        bool enabled)
    {
        return new LocalAIHealthService(
            client,
            Options.Create(new LocalAIOptions
            {
                Enabled = enabled,
                KeepAlive = "10m",
                WarmupOnStartup = true
            }),
            NullLogger<LocalAIHealthService>.Instance);
    }

    private sealed class FakeLocalAIClient : ILocalAIClient
    {
        private readonly Exception? _exception;
        private readonly string? _response;

        public FakeLocalAIClient(string? response = null, Exception? exception = null)
        {
            _response = response;
            _exception = exception;
        }

        public Task<string?> GenerateAsync(
            string prompt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_response);
        }
    }
}
