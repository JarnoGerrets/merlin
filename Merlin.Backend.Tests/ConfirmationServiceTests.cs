using Merlin.Backend.Services;
using Merlin.Backend.Models;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConfirmationServiceTests
{
    [Fact]
    public void Create_StoresPendingConfirmation()
    {
        var service = new ConfirmationService();

        var confirmation = service.Create(
            "open_application",
            "mspaint.exe",
            "Paint",
            "paint",
            "open paint",
            "open_application",
            "open paint",
            "Open Application");

        Assert.False(string.IsNullOrWhiteSpace(confirmation.ConfirmationId));
        Assert.Equal(1, service.PendingCount);
        Assert.Equal("Paint", service.GetLatestPending()?.DisplayName);
    }

    [Fact]
    public async Task PendingCount_RemovesExpiredConfirmations()
    {
        var service = new ConfirmationService(TimeSpan.FromMilliseconds(10));
        service.Create(
            "open_application",
            "mspaint.exe",
            "Paint",
            "paint",
            "open paint",
            "open_application",
            "open paint",
            "Open Application");

        await Task.Delay(30);

        Assert.Equal(0, service.PendingCount);
        Assert.Null(service.GetLatestPending());
    }

    [Fact]
    public void SelectCandidateName_WhenDisplayNameMatches_UpdatesPendingConfirmation()
    {
        var service = new ConfirmationService();
        service.Create(
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

        var confirmation = service.SelectCandidateName("Visual Studio Installer");

        Assert.NotNull(confirmation);
        Assert.Equal("Visual Studio Installer", confirmation.DisplayName);
        Assert.Equal("visual-studio-installer.lnk", confirmation.Target);
        Assert.Single(confirmation.Candidates);
        Assert.Equal(1, service.PendingCount);
        Assert.Equal("Visual Studio Installer", service.GetLatestPending()?.DisplayName);
    }
}
