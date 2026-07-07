using Merlin.Backend.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class MerlinSettingsConfigurationTests
{
    [Fact]
    public void UseMerlinConfiguration_LoadsFeatureOwnedSettingsWithDevelopmentOverrides()
    {
        var configuration = new ConfigurationManager();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Development,
            ContentRootPath = FindBackendRoot()
        };

        configuration.UseMerlinConfiguration(environment, []);

        Assert.Equal("Information", configuration["Logging:LogLevel:Default"]);
        Assert.Equal("WebRtcApm", configuration["BargeIn:AecProvider"]);
        Assert.Equal("25", configuration["BargeIn:AnalysisQueueCapacityFrames"]);
        Assert.Equal("medium.en", configuration["Voice:WhisperModelSize"]);
        Assert.Equal("chatterbox", configuration["Tts:Provider"]);
        Assert.Equal("https://www.youtube.com", configuration["WebDestinations:KnownDestinations:youtube"]);
        Assert.Equal("C:\\Users\\jarno\\anaconda3\\envs\\merlin-vision\\python.exe", configuration["Vision:PythonExecutable"]);
        Assert.True(configuration.GetSection("CapabilityDomains").GetChildren().Any());
    }

    [Fact]
    public void UseMerlinConfiguration_LoadsRequiredSectionsFromFeatureOwnedSettings()
    {
        var configuration = new ConfigurationManager();
        var environment = new TestHostEnvironment
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = FindBackendRoot()
        };

        configuration.UseMerlinConfiguration(environment, []);

        var requiredSections = new[]
        {
            "ApplicationLaunch",
            "MerlinDatabase",
            "CoreMemory",
            "TrustedRegistry",
            "LocalAI",
            "Llm",
            "StreamingResponses",
            "WebSearch",
            "AcknowledgementSpeech",
            "ResponsiveFeedback",
            "InterruptionHandling",
            "VoiceInput",
            "ChatLog",
            "BrowserWorkspace",
            "WebDestinations",
            "GpuScheduling",
            "SpeechPresence",
            "BargeIn",
            "Voice",
            "Piper",
            "Tts",
            "CapabilityDomains",
            "Vision"
        };

        foreach (var section in requiredSections)
        {
            Assert.True(configuration.GetSection(section).Exists(), $"Missing configuration section '{section}'.");
        }
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Merlin.Backend");
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Merlin.Backend from test output directory.");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "Merlin.Backend.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
