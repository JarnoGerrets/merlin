using Merlin.Backend.Services.BrowserWorkspace;
using Merlin.Backend.Services.BrowserWorkspace.PageControl;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Robustness;
using Merlin.Backend.Services.BrowserWorkspace.PageControl.Safety;
using Merlin.Backend.Services.BrowserWorkspace.Snapshot;
using Merlin.Backend.Tools;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class OpenUrlToolTests
{
    [Theory]
    [InlineData("open google.com")]
    [InlineData("open https://google.com")]
    [InlineData("go to google.com")]
    [InlineData("browse google.com")]
    [InlineData("visit google.com")]
    [InlineData("pull up facebook.com")]
    [InlineData("open facebook in the browser")]
    public void CanHandle_WhenCommandLooksLikeUrl_ReturnsTrue(string command)
    {
        var tool = new OpenUrlTool(new FakeProcessLauncher());

        Assert.True(tool.CanHandle(command));
    }

    [Theory]
    [InlineData("open notepad")]
    [InlineData("open calculator")]
    [InlineData("")]
    [InlineData("visit")]
    public void CanHandle_WhenCommandDoesNotLookLikeUrl_ReturnsFalse(string command)
    {
        var tool = new OpenUrlTool(new FakeProcessLauncher());

        Assert.False(tool.CanHandle(command));
    }

    [Theory]
    [InlineData("google.com", "https://google.com")]
    [InlineData("www.google.com", "https://www.google.com")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("facebook in the browser", "https://facebook.com")]
    public void NormalizeUrl_WhenUrlIsValid_ReturnsNormalizedUrl(string input, string expected)
    {
        var result = OpenUrlTool.NormalizeUrl(input);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Url);
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [InlineData("file:///C:/test.txt")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/plain,hello")]
    [InlineData("cmd:dir")]
    [InlineData("powershell:Get-Process")]
    public async Task ExecuteAsync_WhenSchemeIsBlocked_ReturnsBlockedUrlScheme(string target)
    {
        var launcher = new FakeProcessLauncher();
        var tool = new OpenUrlTool(launcher);

        var result = await tool.ExecuteAsync($"open {target}");

        Assert.False(result.Success);
        Assert.Equal("Blocked URL scheme.", result.Message);
        Assert.Equal("BLOCKED_URL_SCHEME", result.ErrorCode);
        Assert.Equal("Open URL", result.ToolName);
        Assert.Equal("open_url", result.Intent);
        Assert.Null(launcher.LaunchedTarget);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\Users\\jarno")]
    [InlineData(@"\\server\share")]
    [InlineData("not a url")]
    [InlineData("localhost")]
    public void NormalizeUrl_WhenUrlIsInvalid_ReturnsInvalidUrl(string target)
    {
        var result = OpenUrlTool.NormalizeUrl(target);

        Assert.False(result.Success);
        Assert.Equal("INVALID_URL", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUrlIsValid_LaunchesNormalizedUrl()
    {
        var launcher = new FakeProcessLauncher();
        var tool = new OpenUrlTool(launcher);

        var result = await tool.ExecuteAsync("open google.com");

        Assert.True(result.Success);
        Assert.Equal("Opening https://google.com...", result.Message);
        Assert.Equal("Open URL", result.ToolName);
        Assert.Equal("open_url", result.Intent);
        Assert.Equal("https://google.com", launcher.LaunchedTarget);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBrowserWorkspaceIsAvailable_AutoOpensAndNavigatesWorkspace()
    {
        var launcher = new FakeProcessLauncher();
        var workspace = new FakeBrowserWorkspaceService
        {
            IsActive = false,
            OpenUrlsInsideWorkspaceWhenActive = true
        };
        var tool = new OpenUrlTool(launcher, workspace);

        var result = await tool.ExecuteAsync("open google.com");

        Assert.True(result.Success);
        Assert.Equal("Opening https://google.com...", result.Message);
        Assert.Equal("Merlin Browser Workspace", result.ToolName);
        Assert.Equal("open_url", result.Intent);
        Assert.True(workspace.WasOpened);
        Assert.Null(workspace.OpenedUrl);
        Assert.Equal("https://google.com", workspace.NavigatedUrl);
        Assert.Null(launcher.LaunchedTarget);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBrowserTargetHasNoDot_DefaultsToDotCom()
    {
        var launcher = new FakeProcessLauncher();
        var tool = new OpenUrlTool(launcher);

        var result = await tool.ExecuteAsync("open facebook in the browser");

        Assert.True(result.Success);
        Assert.Equal("Opening https://facebook.com...", result.Message);
        Assert.Equal("https://facebook.com", launcher.LaunchedTarget);
    }

    [Fact]
    public async Task ExecuteAsync_WhenLauncherFails_ReturnsToolExecutionFailure()
    {
        var tool = new OpenUrlTool(new FakeProcessLauncher(new InvalidOperationException("boom")));

        var result = await tool.ExecuteAsync("open google.com");

        Assert.False(result.Success);
        Assert.Equal("TOOL_EXECUTION_FAILED", result.ErrorCode);
        Assert.Equal("Open URL", result.ToolName);
        Assert.Equal("open_url", result.Intent);
        Assert.Contains("Failed to open URL: boom", result.Message);
    }

    [Fact]
    public void Metadata_IsExposedForDiscovery()
    {
        var tool = new OpenUrlTool(new FakeProcessLauncher());

        Assert.Equal("Open URL", tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Contains("open google.com", tool.Examples);
        Assert.Contains("visit google.com", tool.Examples);
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        private readonly Exception? _exception;

        public FakeProcessLauncher(Exception? exception = null)
        {
            _exception = exception;
        }

        public string? LaunchedTarget { get; private set; }

        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            LaunchedTarget = target;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBrowserWorkspaceService : IBrowserWorkspaceService
    {
        public event Func<BrowserWorkspaceStateChanged, CancellationToken, Task>? StateChanged;

        public bool IsActive { get; init; }

        public BrowserWorkspaceBounds? CurrentBounds => null;

        public bool OpenUrlsInsideWorkspaceWhenActive { get; init; }

        public string? OpenedUrl { get; private set; }

        public string? NavigatedUrl { get; private set; }

        public bool WasOpened { get; private set; }

        public bool WasClosed { get; private set; }

        public BrowserPageSnapshot? LatestSnapshot => null;

        public Task OpenAsync(string? initialUrl = null, CancellationToken cancellationToken = default)
        {
            WasOpened = true;
            OpenedUrl = initialUrl;
            StateChanged?.Invoke(new BrowserWorkspaceStateChanged(true, CurrentBounds, "test"), cancellationToken);
            return Task.CompletedTask;
        }

        public Task NavigateAsync(string url, CancellationToken cancellationToken = default)
        {
            NavigatedUrl = url;
            return Task.CompletedTask;
        }

        public Task BackAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ForwardAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ScrollAsync(
            BrowserScrollDirection direction,
            BrowserScrollAmount amount,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ScrollToTopAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ScrollToBottomAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ZoomInAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ZoomOutAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task ResetZoomAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<BrowserPageSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BrowserPageSnapshot?>(null);
        }

        public Task<BrowserPageSnapshot?> GetFreshSnapshotAsync(
            BrowserSnapshotFreshnessPolicy policy,
            CancellationToken cancellationToken = default)
        {
            return GetSnapshotAsync(cancellationToken);
        }

        public Task<BrowserPageActionResult> SearchCurrentPageAsync(
            string query,
            string? preferredElementId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "not_supported"
            });
        }

        public Task<BrowserPageActionResult> ClickVisibleElementAsync(
            string? query,
            string? targetKind = null,
            int? ordinal = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "not_supported"
            });
        }

        public Task<BrowserPageActionResult> ConfirmBrowserPageClickAsync(
            BrowserPagePendingConfirmation pending,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "not_supported"
            });
        }

        public Task<BrowserPageActionResult> PerformCommonActionAsync(
            string action,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new BrowserPageActionResult
            {
                Success = false,
                ErrorCode = "not_supported"
            });
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            WasClosed = true;
            StateChanged?.Invoke(new BrowserWorkspaceStateChanged(false, null, "test"), cancellationToken);
            return Task.CompletedTask;
        }
    }
}
