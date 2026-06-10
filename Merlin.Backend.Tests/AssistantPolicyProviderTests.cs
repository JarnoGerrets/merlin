using Merlin.Backend.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class AssistantPolicyProviderTests
{
    [Fact]
    public void GetPolicyText_WhenPolicyFileExists_LoadsPolicyFile()
    {
        var root = CreateTempRoot();

        try
        {
            var configurationDirectory = Path.Combine(root, "Configuration");
            Directory.CreateDirectory(configurationDirectory);
            File.WriteAllText(Path.Combine(configurationDirectory, "merlin-constitution.md"), "CUSTOM MERLIN POLICY");

            var provider = new AssistantPolicyProvider(
                new FakeWebHostEnvironment(root),
                NullLogger<AssistantPolicyProvider>.Instance);

            Assert.Equal("CUSTOM MERLIN POLICY", provider.GetPolicyText());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetPolicyText_WhenPolicyFileIsMissing_UsesFallbackPolicy()
    {
        var root = CreateTempRoot();

        try
        {
            var provider = new AssistantPolicyProvider(
                new FakeWebHostEnvironment(root),
                NullLogger<AssistantPolicyProvider>.Instance);

            Assert.Contains("Merlin is a local desktop assistant", provider.GetPolicyText());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetPolicyText_WhenPolicyFileIsEmpty_UsesFallbackPolicy()
    {
        var root = CreateTempRoot();

        try
        {
            var configurationDirectory = Path.Combine(root, "Configuration");
            Directory.CreateDirectory(configurationDirectory);
            File.WriteAllText(Path.Combine(configurationDirectory, "merlin-constitution.md"), "   ");

            var provider = new AssistantPolicyProvider(
                new FakeWebHostEnvironment(root),
                NullLogger<AssistantPolicyProvider>.Instance);

            Assert.Contains("Merlin is a local desktop assistant", provider.GetPolicyText());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetPolicyText_WhenPolicyPathCannotBeRead_DoesNotCrashAndUsesFallbackPolicy()
    {
        var root = CreateTempRoot();

        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Configuration", "merlin-constitution.md"));

            var provider = new AssistantPolicyProvider(
                new FakeWebHostEnvironment(root),
                NullLogger<AssistantPolicyProvider>.Instance);

            Assert.Contains("Merlin is a local desktop assistant", provider.GetPolicyText());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string ApplicationName { get; set; } = "Merlin.Backend.Tests";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; }

        public string EnvironmentName { get; set; } = "Development";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
