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
        confirmationService.Create(
            "open_application",
            "mspaint.exe",
            "Paint",
            "paint",
            "open paint",
            "open_application",
            "open paint",
            "Open Application");
        var trustedCommandStore = new FakeTrustedCommandStore();
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore);

        var result = await tool.ExecuteAsync("confirm");

        Assert.True(result.Success);
        Assert.Equal("Opening Paint...", result.Message);
        Assert.Equal(ToolSpeechTemplates.AppOpenSuccess, result.SpokenText);
        Assert.Equal("tool.app.open.success.generic", result.SpeechCacheKey);
        Assert.True(result.PreferPhraseCache);
        Assert.True(result.IsReplayableSpeech);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.Equal("mspaint.exe", launcher.LaunchedTarget);
        Assert.Equal(0, confirmationService.PendingCount);
        Assert.NotNull(trustedStore.FindByAlias("paint"));
        Assert.Null(trustedCommandStore.FindByCommand("open paint"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoPendingConfirmation_ReturnsNoPendingConfirmation()
    {
        var tool = new ConfirmationTool(
            new ConfirmationService(),
            new FakeProcessLauncher(),
            new FakeTrustedApplicationStore());

        var result = await tool.ExecuteAsync("confirm");

        Assert.False(result.Success);
        Assert.Equal("NO_PENDING_CONFIRMATION", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPendingUrlConfirmationExists_LaunchesUrlWithoutSavingTrustedMappings()
    {
        var confirmationService = new ConfirmationService();
        var launcher = new FakeProcessLauncher();
        var trustedStore = new FakeTrustedApplicationStore();
        confirmationService.Create(
            "open_url",
            "https://facebook.com",
            "facebook.com",
            "facebook.com",
            "open facebook",
            "open_url",
            "open facebook.com",
            "Open URL");
        var trustedCommandStore = new FakeTrustedCommandStore();
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore);

        var result = await tool.ExecuteAsync("confirm");

        Assert.True(result.Success);
        Assert.Equal("Opening facebook.com...", result.Message);
        Assert.Equal("open_url", result.Intent);
        Assert.Equal("https://facebook.com", launcher.LaunchedTarget);
        Assert.Null(trustedStore.FindByAlias("facebook.com"));
        Assert.Null(trustedCommandStore.FindByCommand("open facebook.com"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPendingUrlFallbackExists_SavesTrustedUrlButNotTrustedCommand()
    {
        var confirmationService = new ConfirmationService();
        var launcher = new FakeProcessLauncher();
        var trustedStore = new FakeTrustedApplicationStore();
        var trustedCommandStore = new FakeTrustedCommandStore();
        var trustedUrlStore = new FakeTrustedUrlStore();
        confirmationService.Create(
            "open_url_fallback",
            "https://facebook.com",
            "facebook.com",
            "facebook",
            "can you open facebook for me",
            "open_url",
            "open facebook.com",
            "Open URL");
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore, trustedUrlStore);

        var result = await tool.ExecuteAsync("confirm");

        Assert.True(result.Success);
        Assert.Equal("https://facebook.com", launcher.LaunchedTarget);
        Assert.NotNull(trustedUrlStore.FindByAlias("facebook"));
        Assert.Null(trustedCommandStore.FindByCommand("can you open facebook for me"));
        Assert.Null(trustedCommandStore.FindByCommand("open facebook.com"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenChoiceIsProvided_SelectsCandidateWithoutLaunching()
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
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore);

        var result = await tool.ExecuteAsync("choose 2");

        Assert.False(result.Success);
        Assert.Equal("CONFIRMATION_REQUIRED", result.ErrorCode);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.Equal("You selected Visual Studio Installer. Please confirm before I open it.", result.Message);
        Assert.Equal("Visual Studio Installer", result.Confirmation?.DisplayName);
        Assert.Equal("visual-studio-installer.lnk", result.Confirmation?.Target);
        Assert.Null(launcher.LaunchedTarget);
        Assert.Null(trustedStore.FindByAlias("visual"));
        Assert.Null(trustedCommandStore.FindByCommand("open visual"));
        Assert.Equal(1, confirmationService.PendingCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateNameIsProvided_SelectsCandidateWithoutLaunching()
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
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore);

        var result = await tool.ExecuteAsync("Visual Studio Installer");

        Assert.False(result.Success);
        Assert.Equal("CONFIRMATION_REQUIRED", result.ErrorCode);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.Equal("You selected Visual Studio Installer. Please confirm before I open it.", result.Message);
        Assert.Equal("Visual Studio Installer", result.Confirmation?.DisplayName);
        Assert.Equal("visual-studio-installer.lnk", result.Confirmation?.Target);
        Assert.Null(launcher.LaunchedTarget);
        Assert.Null(trustedStore.FindByAlias("visual"));
        Assert.Null(trustedCommandStore.FindByCommand("open visual"));
        Assert.Equal(1, confirmationService.PendingCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCandidateIsSelectedThenConfirmed_LaunchesAndSavesTrustedAppMappingOnly()
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
        var tool = new ConfirmationTool(confirmationService, launcher, trustedStore);

        var selectionResult = await tool.ExecuteAsync("Visual Studio Installer");
        var confirmationResult = await tool.ExecuteAsync("confirm");

        Assert.False(selectionResult.Success);
        Assert.True(confirmationResult.Success);
        Assert.Equal("confirmation", confirmationResult.ResponseType);
        Assert.Equal("visual-studio-installer.lnk", launcher.LaunchedTarget);
        var trustedMapping = trustedStore.FindByAlias("visual");
        Assert.NotNull(trustedMapping);
        Assert.Equal("Visual Studio Installer", trustedMapping.DisplayName);
        Assert.Null(trustedCommandStore.FindByCommand("open visual"));
        Assert.Equal(0, confirmationService.PendingCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAmbiguousConfirmationIsConfirmedBeforeSelection_AsksForSelection()
    {
        var confirmationService = new ConfirmationService();
        var launcher = new FakeProcessLauncher();
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
        var tool = new ConfirmationTool(
            confirmationService,
            launcher,
            new FakeTrustedApplicationStore());

        var result = await tool.ExecuteAsync("confirm");

        Assert.False(result.Success);
        Assert.Equal("AMBIGUOUS_APPLICATION", result.ErrorCode);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.Equal("Please choose which app you want to open before confirming.", result.Message);
        Assert.Null(launcher.LaunchedTarget);
        Assert.Equal(1, confirmationService.PendingCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsProvided_ClearsPendingConfirmation()
    {
        var confirmationService = new ConfirmationService();
        confirmationService.Create(
            "open_application",
            "powerpnt.exe",
            "PowerPoint",
            "powerpoint",
            "open powerpoint",
            "open_application",
            "open powerpoint",
            "Open Application");
        var launcher = new FakeProcessLauncher();
        var tool = new ConfirmationTool(
            confirmationService,
            launcher,
            new FakeTrustedApplicationStore());

        var result = await tool.ExecuteAsync("sorry not needed anymore");

        Assert.True(result.Success);
        Assert.Equal("Okay, I will not open anything.", result.Message);
        Assert.Equal("confirmation", result.ResponseType);
        Assert.Null(launcher.LaunchedTarget);
        Assert.Equal(0, confirmationService.PendingCount);
    }

    [Fact]
    public void CanHandle_WhenPendingCandidateNameMatches_ReturnsTrue()
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
                    DisplayName = "Visual Studio Installer",
                    ExecutablePath = "visual-studio-installer.lnk",
                    Source = "StartMenu",
                    Confidence = 0.85
                }
            ]);
        var tool = new ConfirmationTool(
            confirmationService,
            new FakeProcessLauncher(),
            new FakeTrustedApplicationStore());

        Assert.True(tool.CanHandle("Visual Studio Installer"));
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
            new FakeTrustedApplicationStore());

        var result = await tool.ExecuteAsync("choose 2");

        Assert.False(result.Success);
        Assert.Equal("INVALID_CONFIRMATION_CHOICE", result.ErrorCode);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("I confirm.")]
    [InlineData("yes")]
    [InlineData("yes please")]
    [InlineData("yes please do")]
    [InlineData("yes please open it")]
    [InlineData("please do so")]
    [InlineData("do so")]
    [InlineData("yes do so")]
    [InlineData("sure do so")]
    [InlineData("okay please do so")]
    [InlineData("go ahead please")]
    [InlineData("open that as a website")]
    [InlineData("yes open that in the browser")]
    [InlineData("that works")]
    [InlineData("works for me")]
    [InlineData("approve")]
    [InlineData("go ahead")]
    [InlineData("sure go ahead")]
    [InlineData("okay do it")]
    [InlineData("open it")]
    public void CanHandle_WhenCommandIsConfirmation_ReturnsTrue(string command)
    {
        var tool = new ConfirmationTool(
            new ConfirmationService(),
            new FakeProcessLauncher(),
            new FakeTrustedApplicationStore());

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
