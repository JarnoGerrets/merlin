using Merlin.Backend.Tools;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class OpenApplicationToolTests
{
    [Theory]
    [InlineData("open notepad")]
    [InlineData("start notepad")]
    [InlineData("launch notepad")]
    [InlineData("OPEN NOTEPAD")]
    [InlineData("open calculator")]
    [InlineData("open browser")]
    [InlineData("open VS Code")]
    [InlineData("open vscode")]
    [InlineData("open paint")]
    [InlineData("start calc")]
    [InlineData("launch visual studio code")]
    [InlineData("pull up facebook")]
    [InlineData("open web browser")]
    public void CanHandle_WhenCommandIsSupported_ReturnsTrue(string command)
    {
        var tool = CreateTool();

        Assert.True(tool.CanHandle(command));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notepad")]
    [InlineData("close notepad")]
    public void CanHandle_WhenCommandIsUnsupported_ReturnsFalse(string command)
    {
        var tool = CreateTool();

        Assert.False(tool.CanHandle(command));
    }

    [Fact]
    public void Metadata_IsExposedForDiscovery()
    {
        var tool = CreateTool();

        Assert.Equal("Open Application", tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Contains("open notepad", tool.Examples);
        Assert.Contains("open calculator", tool.Examples);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscoveredAppRequiresConfirmation_StoresOriginalUserCommandOnConfirmation()
    {
        var confirmationService = new ConfirmationService();
        var tool = new OpenApplicationTool(
            TestApplicationLaunchOptions.Create(),
            new FakeApplicationResolver(new ApplicationResolutionResult
            {
                Found = true,
                RequiresConfirmation = true,
                Candidates =
                [
                    new ApplicationCandidate
                    {
                        DisplayName = "Paint",
                        ExecutablePath = "mspaint.exe",
                        Source = "PATH",
                        Confidence = 1
                    }
                ]
            }),
            confirmationService,
            new FakeProcessLauncher());

        var result = await tool.ExecuteAsync(
            new ToolExecutionContext
            {
                OriginalMessage = "could you open paint for me",
                NormalizedCommand = "open paint",
                Intent = "open_application"
            });

        Assert.False(result.Success);
        Assert.Equal("CONFIRMATION_REQUIRED", result.ErrorCode);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.NotNull(result.Confirmation);
        Assert.Equal("could you open paint for me", result.Confirmation.OriginalUserCommand);
        Assert.Equal("open paint", result.Confirmation.NormalizedCommand);
        Assert.Equal("Open Application", result.Confirmation.ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResolverReturnsTrustedApplication_OpensWithoutConfirmation()
    {
        var launcher = new FakeProcessLauncher();
        var tool = new OpenApplicationTool(
            TestApplicationLaunchOptions.Create(),
            new FakeApplicationResolver(new ApplicationResolutionResult
            {
                Found = true,
                RequiresConfirmation = false,
                Candidates =
                [
                    new ApplicationCandidate
                    {
                        DisplayName = "Paint",
                        ExecutablePath = "mspaint.exe",
                        Source = "Trusted",
                        Confidence = 1
                    }
                ]
            }),
            new ConfirmationService(),
            launcher);

        var result = await tool.ExecuteAsync("open paint");

        Assert.True(result.Success);
        Assert.Equal("Opening Paint...", result.Message);
        Assert.Null(result.Confirmation);
        Assert.Equal("mspaint.exe", launcher.LaunchedTarget);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAppIsNotFound_AsksToOpenDotComInBrowser()
    {
        var confirmationService = new ConfirmationService();
        var tool = new OpenApplicationTool(
            TestApplicationLaunchOptions.Create(),
            new FakeApplicationResolver(new ApplicationResolutionResult { Found = false }),
            confirmationService,
            new FakeProcessLauncher());

        var result = await tool.ExecuteAsync(
            new ToolExecutionContext
            {
                OriginalMessage = "can you open facebook",
                NormalizedCommand = "open facebook",
                Intent = "open_application"
            });

        Assert.False(result.Success);
        Assert.Equal("CONFIRMATION_REQUIRED", result.ErrorCode);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.NotNull(result.Confirmation);
        Assert.Equal("open_url_fallback", result.Confirmation.Action);
        Assert.Equal("https://facebook.com", result.Confirmation.Target);
        Assert.Equal("facebook.com", result.Confirmation.DisplayName);
        Assert.Contains("Should I open facebook.com as a website instead?", result.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPullUpBareWebsiteName_AsksToOpenDotComInBrowser()
    {
        var confirmationService = new ConfirmationService();
        var tool = new OpenApplicationTool(
            TestApplicationLaunchOptions.Create(),
            new FakeApplicationResolver(new ApplicationResolutionResult { Found = false }),
            confirmationService,
            new FakeProcessLauncher());

        var result = await tool.ExecuteAsync(
            new ToolExecutionContext
            {
                OriginalMessage = "can you pull up facebook for me",
                NormalizedCommand = "pull up facebook for me",
                Intent = "open_application"
            });

        Assert.False(result.Success);
        Assert.Equal("CONFIRMATION_REQUIRED", result.ErrorCode);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.NotNull(result.Confirmation);
        Assert.Equal("open_url_fallback", result.Confirmation.Action);
        Assert.Equal("https://facebook.com", result.Confirmation.Target);
        Assert.Equal("facebook.com", result.Confirmation.DisplayName);
    }

    private static OpenApplicationTool CreateTool()
    {
        return new OpenApplicationTool(
            TestApplicationLaunchOptions.Create(),
            new FakeApplicationResolver(),
            new ConfirmationService(),
            new FakeProcessLauncher());
    }

    private sealed class FakeApplicationResolver : IApplicationResolver
    {
        private readonly ApplicationResolutionResult _result;

        public FakeApplicationResolver()
            : this(new ApplicationResolutionResult())
        {
        }

        public FakeApplicationResolver(ApplicationResolutionResult result)
        {
            _result = result;
        }

        public string LastResolutionStatus => "Fake";

        public Task<ApplicationResolutionResult> ResolveAsync(
            string applicationName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public string? LaunchedTarget { get; private set; }

        public Task LaunchAsync(string target, CancellationToken cancellationToken = default)
        {
            LaunchedTarget = target;
            return Task.CompletedTask;
        }
    }
}
