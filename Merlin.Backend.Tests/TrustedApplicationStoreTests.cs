using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class TrustedApplicationStoreTests
{
    [Fact]
    public void SaveMapping_PersistsMappingToJsonStore()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedApplicationStore(storePath, NullLogger<TrustedApplicationStore>.Instance);

            store.SaveMapping("paint", new ApplicationCandidate
            {
                DisplayName = "Paint",
                ExecutablePath = "mspaint.exe",
                Source = "StartMenu",
                Confidence = 1
            });

            var reloaded = new TrustedApplicationStore(storePath, NullLogger<TrustedApplicationStore>.Instance);
            var mapping = reloaded.FindByAlias("paint");

            Assert.NotNull(mapping);
            Assert.Equal("Paint", mapping.DisplayName);
            Assert.Equal("mspaint.exe", mapping.ExecutablePath);
            Assert.Equal("StartMenu", mapping.Source);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void SaveMapping_WhenAliasAlreadyExists_ReplacesMapping()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedApplicationStore(storePath, NullLogger<TrustedApplicationStore>.Instance);

            store.SaveMapping("paint", new ApplicationCandidate
            {
                DisplayName = "Paint",
                ExecutablePath = "old.exe",
                Source = "PATH",
                Confidence = 1
            });
            store.SaveMapping("paint", new ApplicationCandidate
            {
                DisplayName = "Paint",
                ExecutablePath = "mspaint.exe",
                Source = "StartMenu",
                Confidence = 1
            });

            var mappings = store.GetAll();

            Assert.Single(mappings);
            Assert.Equal("mspaint.exe", mappings.Single().ExecutablePath);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    private static string CreateTempStorePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "trusted-applications.json");
    }

    private static void DeleteTempStore(string storePath)
    {
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
