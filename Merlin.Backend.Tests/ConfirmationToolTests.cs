using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Merlin.Backend.Tools;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConfirmationToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenPendingConfirmationExists_LaunchesTarget()
    {
        var confirmationService = new ConfirmationService();
        var launcher = new FakeProcessLauncher();
        var trustedStore = new FakeTrustedApplicationStore();
        var trustedCommandStore = new FakeTrustedCommandStore();
        confirmationService.Create(
            "open_application",
            "mspaint.exe",
            "Paint",
            "paint",
            "open paint",
            "open_application",
            "open paint",
            "Open Application");
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore, trustedCommandStore);

        var result = await tool.ExecuteAsync("confirm");

        Assert.True(result.Success);
        Assert.Equal("Opening Paint...", result.Message);
        Assert.Equal("mspaint.exe", launcher.LaunchedTarget);
        Assert.Equal(0, confirmationService.PendingCount);
        Assert.NotNull(trustedStore.FindByAlias("paint"));
        Assert.NotNull(trustedCommandStore.FindByCommand("open paint"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingConfirmation_ReturnsNoPendingConfirmation()
    {
        var tool = new ConfirmationTool(
            new ConfirmationService(),
            new FakeProcessLauncher(),
            new FakeTrustedApplicationStore(),
            new FakeTrustedCommandStore());

        var result = await tool.ExecuteAsync("confirm");

        Assert.False(result.Success);
        Assert.Equal("NO_PENDING_CONFIRMATION", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenChoiceIsProvided_LaunchesSelectedCandidate()
    {
        var confirmationService = new ConfirmationService();
        var launcher = new FakeProcessLauncher();
        var trustedStore = new FakeTrustedApplicationStore();
        var trustedCommandStore = new FakeTrustedCommandStore();
        confirmationService.Create(
            "open_application",
            string.Empty,
            "visual",
            "visual",
            "open visual",
            "open_application",
            "open visual",
            "Open Application",
            [
                new ApplicationCandidate
                {
                    DisplayName = "Visual Studio",
                    ExecutablePath = "visual-studio.lnk",
                    Source = "StartMenu",
                    Confidence = 0.85
                },
                new ApplicationCandidate
                {
                    DisplayName = "Visual Studio Installer",
                    ExecutablePath = "visual-studio-installer.lnk",
                    Source = "StartMenu",
                    Confidence = 0.85
                }
            ]);
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore, trustedCommandStore);

        var result = await tool.ExecuteAsync("choose 2");

        Assert.True(result.Success);
        Assert.Equal("visual-studio-installer.lnk", launcher.LaunchedTarget);
        var trustedMapping = trustedStore.FindByAlias("visual");
        Assert.NotNull(trustedMapping);
        Assert.Equal("Visual Studio Installer", trustedMapping.DisplayName);
        Assert.NotNull(trustedCommandStore.FindByCommand("open visual"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenChoiceIsInvalid_ReturnsInvalidConfirmationChoice()
    {
        var confirmationService = new ConfirmationService();
        confirmationService.Create(
            "open_application",
            string.Empty,
            "visual",
            "visual",
            "open visual",
            "open_application",
            "open visual",
            "Open Application",
            [
                new ApplicationCandidate
                {
                    DisplayName = "Visual Studio",
                    ExecutablePath = "visual-studio.lnk",
                    Source = "StartMenu",
                    Confidence = 0.85
                }
            ]);
        var tool = new ConfirmationTool(
            confirmationService,
            new FakeProcessLauncher(),
            new FakeTrustedApplicationStore(),
            new FakeTrustedCommandStore());

        var result = await tool.ExecuteAsync("choose 2");

        Assert.False(result.Success);
        Assert.Equal("INVALID_CONFIRMATION_CHOICE", result.ErrorCode);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("yes")]
    [InlineData("approve")]
    public void CanHandle_WhenCommandIsConfirmation_ReturnsTrue(string command)
    {
        var tool = new ConfirmationTool(
            new ConfirmationService(),
            new FakeProcessLauncher(),
            new FakeTrustedApplicationStore(),
            new FakeTrustedCommandStore());

        Assert.True(tool.CanHandle(command));
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
