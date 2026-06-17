using Merlin.Backend.Configuration;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ApplicationResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenConfiguredAliasMatches_ReturnsConfiguredCandidate()
    {
        var resolver = new ApplicationResolver(TestApplicationLaunchOptions.Create(), new FakeTrustedApplicationStore());

        var result = await resolver.ResolveAsync("calc");

        Assert.True(result.Found);
        var candidate = result.Candidates.First();
        Assert.Equal("Calculator", candidate.DisplayName);
        Assert.Equal("calc.exe", candidate.ExecutablePath);
        Assert.Equal("Configured", candidate.Source);
    }

    [Fact]
    public async Task ResolveAsync_WhenTrustedAliasMatches_ReturnsTrustedCandidate()
    {
        var trustedStore = new FakeTrustedApplicationStore();
        trustedStore.SaveMapping("paint", new ApplicationCandidate
        {
            DisplayName = "Paint",
            ExecutablePath = "mspaint.exe",
            Source = "StartMenu",
            Confidence = 1
        });
        var resolver = new ApplicationResolver(
            Options.Create(new ApplicationLaunchOptions()),
            trustedStore);

        var result = await resolver.ResolveAsync("paint");

        Assert.True(result.Found);
        Assert.False(result.RequiresConfirmation);
        var candidate = result.Candidates.Single();
        Assert.Equal("Paint", candidate.DisplayName);
        Assert.Equal("mspaint.exe", candidate.ExecutablePath);
        Assert.Equal("Trusted", candidate.Source);
    }

    [Fact]
    public async Task ResolveAsync_WhenShortAmbiguousNameMatchesMultipleCandidates_ReturnsAmbiguous()
    {
        var tempRoot = CreateTempDirectory();
        var originalProgramData = Environment.GetEnvironmentVariable("ProgramData");
        var originalAppData = Environment.GetEnvironmentVariable("AppData");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            var startMenu = Path.Combine(tempRoot, "Microsoft", "Windows", "Start Menu", "Programs");
            Directory.CreateDirectory(startMenu);
            File.WriteAllText(Path.Combine(startMenu, "Visual Studio.lnk"), "");
            File.WriteAllText(Path.Combine(startMenu, "Visual Studio Installer.lnk"), "");
            Environment.SetEnvironmentVariable("ProgramData", tempRoot);
            Environment.SetEnvironmentVariable("AppData", Path.Combine(tempRoot, "missing"));
            Environment.SetEnvironmentVariable("PATH", string.Empty);

            var resolver = new ApplicationResolver(
                Options.Create(new ApplicationLaunchOptions()),
                new FakeTrustedApplicationStore());

            var result = await resolver.ResolveAsync("visual");

            Assert.True(result.Found);
            Assert.True(result.RequiresConfirmation);
            Assert.True(result.IsAmbiguous);
            Assert.Contains("Which one did you mean?", result.Message);
            Assert.True(result.Candidates.Count >= 2);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProgramData", originalProgramData);
            Environment.SetEnvironmentVariable("AppData", originalAppData);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenStartMenuShortcutMatches_ReturnsStartMenuCandidate()
    {
        var tempRoot = CreateTempDirectory();
        var originalProgramData = Environment.GetEnvironmentVariable("ProgramData");
        var originalAppData = Environment.GetEnvironmentVariable("AppData");

        try
        {
            var startMenu = Path.Combine(tempRoot, "Microsoft", "Windows", "Start Menu", "Programs");
            Directory.CreateDirectory(startMenu);
            File.WriteAllText(Path.Combine(startMenu, "Paint.lnk"), "");
            Environment.SetEnvironmentVariable("ProgramData", tempRoot);
            Environment.SetEnvironmentVariable("AppData", Path.Combine(tempRoot, "missing"));

            var resolver = new ApplicationResolver(
                Options.Create(new ApplicationLaunchOptions()),
                new FakeTrustedApplicationStore());

            var result = await resolver.ResolveAsync("paint");

            Assert.True(result.Found);
            Assert.Contains(result.Candidates, candidate => candidate.Source == "StartMenu" && candidate.DisplayName == "Paint");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProgramData", originalProgramData);
            Environment.SetEnvironmentVariable("AppData", originalAppData);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenSameAppHasUserAndMachineShortcuts_CollapsesToSingleCandidate()
    {
        var tempRoot = CreateTempDirectory();
        var tempAppData = CreateTempDirectory();
        var originalProgramData = Environment.GetEnvironmentVariable("ProgramData");
        var originalAppData = Environment.GetEnvironmentVariable("AppData");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            var machineStartMenu = Path.Combine(tempRoot, "Microsoft", "Windows", "Start Menu", "Programs", "Steam");
            var userStartMenu = Path.Combine(tempAppData, "Microsoft", "Windows", "Start Menu", "Programs", "Steam");
            Directory.CreateDirectory(machineStartMenu);
            Directory.CreateDirectory(userStartMenu);
            File.WriteAllText(Path.Combine(machineStartMenu, "Steam.lnk"), "");
            File.WriteAllText(Path.Combine(userStartMenu, "Steam.lnk"), "");
            Environment.SetEnvironmentVariable("ProgramData", tempRoot);
            Environment.SetEnvironmentVariable("AppData", tempAppData);
            Environment.SetEnvironmentVariable("PATH", string.Empty);

            var resolver = new ApplicationResolver(
                Options.Create(new ApplicationLaunchOptions()),
                new FakeTrustedApplicationStore());

            var result = await resolver.ResolveAsync("steam");

            Assert.True(result.Found);
            Assert.False(result.IsAmbiguous);
            Assert.True(result.RequiresConfirmation);
            Assert.Single(result.Candidates);
            Assert.Equal("Steam", result.Candidates.Single().DisplayName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProgramData", originalProgramData);
            Environment.SetEnvironmentVariable("AppData", originalAppData);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
            Directory.Delete(tempAppData, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenOneCandidateIsClearlyBetter_DoesNotMarkAmbiguous()
    {
        var tempRoot = CreateTempDirectory();
        var originalProgramData = Environment.GetEnvironmentVariable("ProgramData");
        var originalAppData = Environment.GetEnvironmentVariable("AppData");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            var startMenu = Path.Combine(tempRoot, "Microsoft", "Windows", "Start Menu", "Programs");
            Directory.CreateDirectory(startMenu);
            File.WriteAllText(Path.Combine(startMenu, "Word.lnk"), "");
            File.WriteAllText(Path.Combine(startMenu, "WordPad.lnk"), "");
            Environment.SetEnvironmentVariable("ProgramData", tempRoot);
            Environment.SetEnvironmentVariable("AppData", Path.Combine(tempRoot, "missing"));
            Environment.SetEnvironmentVariable("PATH", string.Empty);

            var resolver = new ApplicationResolver(
                Options.Create(new ApplicationLaunchOptions()),
                new FakeTrustedApplicationStore());

            var result = await resolver.ResolveAsync("word");

            Assert.True(result.Found);
            Assert.False(result.IsAmbiguous);
            Assert.True(result.RequiresConfirmation);
            Assert.Equal("Word", result.Candidates.First().DisplayName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProgramData", originalProgramData);
            Environment.SetEnvironmentVariable("AppData", originalAppData);
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenPathExecutableMatches_ReturnsPathCandidate()
    {
        var tempRoot = CreateTempDirectory();
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            File.WriteAllText(Path.Combine(tempRoot, "mspaint.exe"), "");
            Environment.SetEnvironmentVariable("PATH", tempRoot);

            var resolver = new ApplicationResolver(
                Options.Create(new ApplicationLaunchOptions()),
                new FakeTrustedApplicationStore());

            var result = await resolver.ResolveAsync("paint");

            Assert.True(result.Found);
            Assert.Contains(result.Candidates, candidate => candidate.Source == "PATH" && candidate.DisplayName == "Paint");
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_WhenNoCandidateMatches_ReturnsNotFound()
    {
        var originalProgramData = Environment.GetEnvironmentVariable("ProgramData");
        var originalAppData = Environment.GetEnvironmentVariable("AppData");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("ProgramData", null);
            Environment.SetEnvironmentVariable("AppData", null);
            Environment.SetEnvironmentVariable("PATH", string.Empty);
            var resolver = new ApplicationResolver(
                Options.Create(new ApplicationLaunchOptions()),
                new FakeTrustedApplicationStore());

            var result = await resolver.ResolveAsync("somefakeapp123");

            Assert.False(result.Found);
            Assert.Empty(result.Candidates);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ProgramData", originalProgramData);
            Environment.SetEnvironmentVariable("AppData", originalAppData);
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
