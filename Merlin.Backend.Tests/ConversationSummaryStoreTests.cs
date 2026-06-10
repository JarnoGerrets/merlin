using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class ConversationSummaryStoreTests
{
    [Fact]
    public void Constructor_WhenFileIsMissing_CreatesEmptyStoreFile()
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
    public void SaveSummary_PersistsSummary()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            var summary = store.SaveSummary(CreateSummary("Merlin Backend Development", ["merlin", "backend"]));

            var reloaded = CreateStore(storePath);
            var saved = Assert.Single(reloaded.GetAll());

            Assert.Equal(summary.SummaryId, saved.SummaryId);
            Assert.Equal("Merlin Backend Development", saved.Title);
            Assert.Contains("backend", saved.Tags);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void Constructor_WhenJsonIsCorrupt_RecoversWithEmptyUnhealthyStore()
    {
        var storePath = CreateTempStorePath();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);
            File.WriteAllText(storePath, "{ not valid json");

            var store = CreateStore(storePath);

            Assert.Empty(store.GetAll());
            Assert.False(store.IsHealthy);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void GetRecentSummaries_ReturnsMostRecentFirst()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            store.SaveSummary(CreateSummary("Older", ["merlin"]));
            Thread.Sleep(5);
            store.SaveSummary(CreateSummary("Newer", ["backend"]));

            var recent = store.GetRecentSummaries(1);

            var summary = Assert.Single(recent);
            Assert.Equal("Newer", summary.Title);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void SearchSummaries_FindsByTitle()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            store.SaveSummary(CreateSummary("Merlin Backend Development", ["merlin"]));

            var results = store.SearchSummaries("backend");

            Assert.Single(results);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void SearchSummaries_FindsByTag()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            store.SaveSummary(CreateSummary("AI Work", ["local-ai"]));

            var results = store.SearchSummaries("local-ai");

            Assert.Single(results);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    [Fact]
    public void SearchSummaries_FindsBySummaryText()
    {
        var storePath = CreateTempStorePath();

        try
        {
            var store = CreateStore(storePath);
            store.SaveSummary(new ConversationSummary
            {
                Title = "Development",
                SummaryText = "Worked on ApplicationResolver and intent parsing.",
                Tags = ["merlin"],
                MessageCount = 3
            });

            var results = store.SearchSummaries("ApplicationResolver");

            Assert.Single(results);
        }
        finally
        {
            DeleteTempStore(storePath);
        }
    }

    private static ConversationSummaryStore CreateStore(string storePath)
    {
        return new ConversationSummaryStore(storePath, NullLogger<ConversationSummaryStore>.Instance);
    }

    private static ConversationSummary CreateSummary(
        string title,
        IReadOnlyCollection<string> tags)
    {
        return new ConversationSummary
        {
            Title = title,
            SummaryText = "Worked on Merlin backend and LocalAI integration.",
            Tags = tags,
            MessageCount = 5
        };
    }

    private static string CreateTempStorePath()
    {
        return Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "conversation-summaries.json");
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
