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
}
