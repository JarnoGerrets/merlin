using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class LongTermMemoryStoreTests
{
    [Fact]
    public void Constructor_WhenFileIsMissing_CreatesEmptyStore()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);

            Assert.True(File.Exists(storePath));
            Assert.Empty(store.GetAll());
            Assert.True(store.IsHealthy);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void SaveMemory_PersistsMemory()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            var memory = store.SaveMemory(CreateMemory("project", "frontend", "Merlin uses Godot frontend."));

            var reloaded = CreateStore(storePath);
            var saved = Assert.Single(reloaded.GetAll());

            Assert.Equal(memory.MemoryId, saved.MemoryId);
            Assert.Equal("project", saved.Category);
            Assert.Equal("frontend", saved.Key);
            Assert.Equal("Merlin uses Godot frontend.", saved.Value);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void UpdateMemory_UpdatesExistingMemory()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            var memory = store.SaveMemory(CreateMemory("project", "frontend", "Merlin uses Godot."));

            var updated = store.UpdateMemory(new MemoryRecord
            {
                MemoryId = memory.MemoryId,
                Category = "project",
                Key = "frontend",
                Value = "Merlin uses Godot frontend.",
                Source = "test",
                Confidence = 0.9
            });

            Assert.Equal(memory.MemoryId, updated.MemoryId);
            Assert.Equal("Merlin uses Godot frontend.", Assert.Single(store.GetAll()).Value);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void DeleteMemory_RemovesMemory()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            var memory = store.SaveMemory(CreateMemory("project", "frontend", "Merlin uses Godot."));

            var removed = store.DeleteMemory(memory.MemoryId);

            Assert.True(removed);
            Assert.Empty(store.GetAll());
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void Search_FindsMemoryByText()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            store.SaveMemory(CreateMemory("project", "frontend", "Merlin uses Godot frontend."));

            var results = store.Search("Godot");

            Assert.Single(results);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void MergeMemory_WhenDuplicateCategoryAndKeyExist_UpdatesExistingRecord()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            var first = store.MergeMemory(CreateMemory("project", "frontend", "Merlin uses Godot."));
            var second = store.MergeMemory(CreateMemory("project", "frontend", "Merlin uses Godot frontend."));

            Assert.Equal(first.MemoryId, second.MemoryId);
            var memory = Assert.Single(store.GetAll());
            Assert.Equal("Merlin uses Godot frontend.", memory.Value);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void Constructor_WhenJsonIsCorrupt_RecoversWithoutThrowing()
    {
        var storePath = CreateTempStorePath();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            File.WriteAllText(storePath, "{ bad json");

            var store = CreateStore(storePath);

            Assert.Empty(store.GetAll());
            Assert.False(store.IsHealthy);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    private static MemoryRecord CreateMemory(string category, string key, string value)
    {
        return new MemoryRecord
        {
            Category = category,
            Key = key,
            Value = value,
            Source = "test",
            Confidence = 0.9
        };
    }

    private static LongTermMemoryStore CreateStore(string storePath)
    {
        return new LongTermMemoryStore(storePath, NullLogger<LongTermMemoryStore>.Instance);
    }

    private static string CreateTempStorePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "long-term-memory.json");
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
