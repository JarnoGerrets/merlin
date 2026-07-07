using Merlin.Backend.Services.Context.ActiveSurface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ActiveSurfaceServiceTests
{
    [Fact]
    public void Current_DefaultSurfaceIsDashboard()
    {
        var service = CreateService();

        Assert.Equal(ActiveSurfaceKind.Dashboard, service.Current.Kind);
        Assert.True(service.CurrentSupports(ActiveSurfaceCapabilities.AssistantPlaybackPause));
    }

    [Fact]
    public async Task SetActiveSurfaceAsync_BrowserWorkspaceUpdatesCurrentSnapshot()
    {
        var service = CreateService();

        await service.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = ActiveSurfaceKind.BrowserWorkspace,
            SurfaceId = "browser.workspace.main",
            DisplayName = "Browser Workspace",
            Source = ActiveSurfaceSource.BrowserWorkspace,
            Confidence = 1.0,
            Capabilities = KnownSurfaces.BrowserWorkspace(DateTimeOffset.UtcNow).Capabilities,
            Metadata = new Dictionary<string, string>
            {
                ["url"] = "https://youtube.com/watch",
                ["domain"] = "youtube.com",
                ["title"] = "Video"
            }
        });

        Assert.Equal(ActiveSurfaceKind.BrowserWorkspace, service.Current.Kind);
        Assert.True(service.CurrentSupports(ActiveSurfaceCapabilities.BrowserMediaPause));
        Assert.Equal("youtube.com", service.Current.Metadata["domain"]);
    }

    [Fact]
    public async Task ResetToDashboardAsync_SetsDashboard()
    {
        var service = CreateService();
        await service.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = ActiveSurfaceKind.BrowserWorkspace,
            SurfaceId = "browser.workspace.main",
            DisplayName = "Browser Workspace",
            Source = ActiveSurfaceSource.BrowserWorkspace,
            Confidence = 1.0,
            Capabilities = KnownSurfaces.BrowserWorkspace(DateTimeOffset.UtcNow).Capabilities
        });

        await service.ResetToDashboardAsync("browser_workspace_closed");

        Assert.Equal(ActiveSurfaceKind.Dashboard, service.Current.Kind);
        Assert.True(service.CurrentSupports(ActiveSurfaceCapabilities.AssistantPlaybackPause));
        Assert.False(service.CurrentSupports(ActiveSurfaceCapabilities.BrowserMediaPause));
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(2.0, 1.0)]
    public async Task SetActiveSurfaceAsync_ClampsConfidence(double input, double expected)
    {
        var service = CreateService();

        await service.SetActiveSurfaceAsync(new ActiveSurfaceUpdate
        {
            Kind = ActiveSurfaceKind.Unknown,
            SurfaceId = "unknown",
            DisplayName = "Unknown",
            Source = ActiveSurfaceSource.Unknown,
            Confidence = input
        });

        Assert.Equal(expected, service.Current.Confidence);
    }

    [Fact]
    public async Task ConcurrentReads_DoNotThrow()
    {
        var service = CreateService();

        var reads = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => service.Current))
            .ToArray();

        await Task.WhenAll(reads);

        Assert.All(reads, task => Assert.NotNull(task.Result));
    }

    private static ActiveSurfaceService CreateService() =>
        new(NullLogger<ActiveSurfaceService>.Instance);
}
