using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class TrustedCommandStoreTests
{
    [Fact]
    public void SaveMapping_PersistsTrustedCommand()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);

            SavePaintMapping(store, "open paint");

            var reloaded = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);
            var mapping = reloaded.FindByCommand("open paint");

            Assert.NotNull(mapping);
            Assert.Equal("open_application", mapping.Intent);
            Assert.Equal("open paint", mapping.NormalizedCommand);
            Assert.Equal("Open Application", mapping.ToolName);
            Assert.Equal("mspaint.exe", mapping.Target);
            Assert.Equal("Paint", mapping.DisplayName);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Theory]
    [InlineData("OPEN PAINT")]
    [InlineData("  open   paint  ")]
    [InlineData("open paint?")]
    [InlineData("open paint!")]
    public void FindByCommand_UsesLightNormalization(string command)
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);
            SavePaintMapping(store, "open paint");

            var mapping = store.FindByCommand(command);

            Assert.NotNull(mapping);
            Assert.Equal("open paint", mapping.NormalizedOriginalCommand);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void FindByCommand_WhenMappingIsUsed_IncrementsUseCountAndUpdatesLastUsedAtUtc()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);
            SavePaintMapping(store, "open paint");
            var first = store.FindByCommand("open paint");

            Thread.Sleep(5);
            var second = store.FindByCommand("open paint");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(first.UseCount + 1, second.UseCount);
            Assert.True(second.LastUsedAtUtc > first.LastUsedAtUtc);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void Load_WhenJsonIsCorrupt_StartsEmpty()
    {
        var storePath = CreateTempStorePath();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            File.WriteAllText(storePath, "{ this is not valid json");

            var store = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);

            Assert.Empty(store.GetAll());
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void FindByCommand_DoesNotUseBroadFuzzyMatching()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);
            SavePaintMapping(store, "open visual studio code");

            Assert.Null(store.FindByCommand("open vs"));
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void SaveMapping_AllowsExplicitOpenVsMappingOnlyWhenSaved()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = new TrustedCommandStore(storePath, NullLogger<TrustedCommandStore>.Instance);
            store.SaveMapping(new TrustedCommandMapping
            {
                OriginalCommand = "open vs",
                Intent = "open_application",
                NormalizedCommand = "open vs",
                ToolName = "Open Application",
                Target = "code",
                DisplayName = "VS Code",
                UseCount = 1
            });

            Assert.NotNull(store.FindByCommand("open vs"));
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    private static void SavePaintMapping(TrustedCommandStore store, string originalCommand)
    {
        store.SaveMapping(new TrustedCommandMapping
        {
            OriginalCommand = originalCommand,
            Intent = "open_application",
            NormalizedCommand = "open paint",
            ToolName = "Open Application",
            Target = "mspaint.exe",
            DisplayName = "Paint",
            UseCount = 1
        });
    }

    private static string CreateTempStorePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "trusted-commands.json");
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
