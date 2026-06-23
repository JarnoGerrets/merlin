using System.Text.Json;
using Merlin.Backend.Configuration;
using Merlin.Backend.Infrastructure.TrustedRegistry;
using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Merlin.Backend.Infrastructure.TrustedRegistry.Repositories;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class TrustedRegistryTests
{
    [Fact]
    public async Task Migrate_CreatesTrustedRegistryTables_AndPersistsEntities()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();

        await using (var db = await fixture.Factory.CreateDbContextAsync())
        {
            db.TrustedAppMappings.Add(new TrustedAppMappingEntity
            {
                Alias = "paint",
                NormalizedAlias = "paint",
                DisplayName = "Paint",
                ExecutablePath = "mspaint.exe",
                Source = "StartMenu",
                Confidence = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUsedAtUtc = DateTimeOffset.UtcNow,
                UseCount = 1,
                Status = TrustedRegistryStatuses.Active
            });
            await db.SaveChangesAsync();
        }

        await using (var db = await fixture.Factory.CreateDbContextAsync())
        {
            Assert.Equal(1, await db.TrustedAppMappings.CountAsync());
            Assert.Empty(await db.TrustedCommandMappings.ToArrayAsync());
            Assert.Empty(await db.TrustedUrlMappings.ToArrayAsync());
            Assert.Empty(await db.TrustedRegistryEvents.ToArrayAsync());
        }
    }

    [Fact]
    public async Task EfTrustedApplicationStore_PersistsReplacesAndUpdatesUsage()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();
        var store = new EfTrustedApplicationStore(fixture.Factory);

        store.SaveMapping(" Paint ", new ApplicationCandidate
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

        var first = store.FindByAlias("PAINT");
        await Task.Delay(5);
        var second = store.FindByAlias("paint");

        Assert.Single(store.GetAll());
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("mspaint.exe", second.ExecutablePath);
        Assert.True(second.LastUsedAtUtc > first.LastUsedAtUtc);

        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(3, (await db.TrustedAppMappings.SingleAsync()).UseCount);
    }

    [Fact]
    public async Task EfTrustedCommandStore_UsesExactNormalizedMatching_AndUpdatesUsage()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();
        var store = new EfTrustedCommandStore(fixture.Factory);

        store.SaveMapping(CreateCommandMapping("open visual studio code"));

        Assert.Null(store.FindByCommand("open vs"));

        var first = store.FindByCommand("OPEN   VISUAL STUDIO CODE?");
        await Task.Delay(5);
        var second = store.FindByCommand("open visual studio code");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.UseCount + 1, second.UseCount);
        Assert.True(second.LastUsedAtUtc > first.LastUsedAtUtc);
    }

    [Fact]
    public async Task EfTrustedUrlStore_PersistsReplacesUpdatesAndDeletesMappings()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();
        var store = new EfTrustedUrlStore(fixture.Factory);

        store.SaveMapping("facebook website", "https://old.example", "Old");
        store.SaveMapping("facebook", "https://facebook.com", "facebook.com");

        var first = store.FindByAlias("facebook mapping");
        var updated = store.UpdateMapping("facebook", "https://m.facebook.com", "Facebook Mobile");

        Assert.Single(store.GetAll());
        Assert.NotNull(first);
        Assert.NotNull(updated);
        Assert.Equal("https://m.facebook.com", updated.Url);
        Assert.True(store.DeleteMapping("facebook"));
        Assert.Null(store.FindByAlias("facebook"));
    }

    [Fact]
    public async Task LegacyJsonImporter_ImportsIdempotently_AndDoesNotOverwriteExistingSqliteRows()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();
        var legacyDirectory = Path.Combine(fixture.DirectoryPath, "legacy");
        Directory.CreateDirectory(legacyDirectory);
        var appPath = Path.Combine(legacyDirectory, "trusted-applications.json");
        var commandPath = Path.Combine(legacyDirectory, "trusted-commands.json");
        var urlPath = Path.Combine(legacyDirectory, "trusted-browser-mappings.json");

        WriteJson(appPath, new
        {
            applications = new[]
            {
                new
                {
                    alias = "paint",
                    displayName = "Paint",
                    executablePath = "legacy-paint.exe",
                    source = "StartMenu",
                    createdAtUtc = DateTimeOffset.UtcNow,
                    lastUsedAtUtc = DateTimeOffset.UtcNow
                }
            }
        });
        WriteJson(commandPath, new
        {
            commands = new[]
            {
                new
                {
                    originalCommand = "open paint",
                    normalizedOriginalCommand = "open paint",
                    intent = "open_application",
                    normalizedCommand = "open paint",
                    toolName = "Open Application",
                    target = "legacy-paint.exe",
                    displayName = "Paint",
                    createdAtUtc = DateTimeOffset.UtcNow,
                    lastUsedAtUtc = DateTimeOffset.UtcNow,
                    useCount = 3
                }
            }
        });
        WriteJson(urlPath, new
        {
            urls = new[]
            {
                new
                {
                    alias = "facebook",
                    url = "https://facebook.com",
                    displayName = "facebook.com",
                    createdAtUtc = DateTimeOffset.UtcNow,
                    lastUsedAtUtc = DateTimeOffset.UtcNow,
                    useCount = 2
                }
            }
        });

        var appStore = new EfTrustedApplicationStore(fixture.Factory);
        appStore.SaveMapping("paint", new ApplicationCandidate
        {
            DisplayName = "Paint",
            ExecutablePath = "sqlite-paint.exe",
            Source = "Trusted",
            Confidence = 1
        });

        var importer = new TrustedRegistryLegacyJsonImporter(
            fixture.Factory,
            Options.Create(new TrustedRegistryOptions()),
            NullLogger<TrustedRegistryLegacyJsonImporter>.Instance,
            appPath,
            commandPath,
            urlPath);

        await importer.ImportAsync();
        await importer.ImportAsync();

        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await db.TrustedAppMappings.ToArrayAsync());
        Assert.Single(await db.TrustedCommandMappings.ToArrayAsync());
        Assert.Single(await db.TrustedUrlMappings.ToArrayAsync());
        Assert.Equal("sqlite-paint.exe", (await db.TrustedAppMappings.SingleAsync()).ExecutablePath);
        Assert.Equal(TrustedRegistryStatuses.Archived, (await db.TrustedCommandMappings.SingleAsync()).Status);
        Assert.NotEmpty(await db.TrustedRegistryEvents.ToArrayAsync());
    }

    [Fact]
    public async Task LegacyJsonImporter_WhenJsonIsCorrupt_HandlesItGracefully()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();
        var corruptPath = Path.Combine(fixture.DirectoryPath, "corrupt.json");
        await File.WriteAllTextAsync(corruptPath, "{ this is not json");

        var importer = new TrustedRegistryLegacyJsonImporter(
            fixture.Factory,
            Options.Create(new TrustedRegistryOptions()),
            NullLogger<TrustedRegistryLegacyJsonImporter>.Instance,
            corruptPath,
            corruptPath,
            corruptPath);

        await importer.ImportAsync();

        await using var db = await fixture.Factory.CreateDbContextAsync();
        Assert.Empty(await db.TrustedAppMappings.ToArrayAsync());
        Assert.Empty(await db.TrustedCommandMappings.ToArrayAsync());
        Assert.Empty(await db.TrustedUrlMappings.ToArrayAsync());
    }

    [Fact]
    public async Task TrustedCommandIntentParser_ReadsFromEfTrustedCommandStore()
    {
        using var fixture = TrustedRegistryFixture.Create();
        await fixture.MigrateAsync();
        var store = new EfTrustedCommandStore(fixture.Factory);
        store.SaveMapping(CreateCommandMapping("could you open paint for me"));
        var parser = new TrustedCommandIntentParser(store);

        var result = await parser.ParseAsync("could you open paint for me?");
        var fuzzyResult = await parser.ParseAsync("open paint");

        Assert.Equal("open_application", result.Intent);
        Assert.Equal("open paint", result.NormalizedCommand);
        Assert.Equal(nameof(TrustedCommandIntentParser), result.ParserUsed);
        Assert.Null(fuzzyResult.Intent);
    }

    private static TrustedCommandMapping CreateCommandMapping(string originalCommand)
    {
        return new TrustedCommandMapping
        {
            OriginalCommand = originalCommand,
            Intent = "open_application",
            NormalizedCommand = "open paint",
            ToolName = "Open Application",
            Target = "mspaint.exe",
            DisplayName = "Paint",
            UseCount = 1
        };
    }

    private static void WriteJson(string path, object value)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private sealed class TrustedRegistryFixture : IDisposable
    {
        private TrustedRegistryFixture(string directoryPath, TestTrustedRegistryDbContextFactory factory)
        {
            DirectoryPath = directoryPath;
            Factory = factory;
        }

        public string DirectoryPath { get; }

        public TestTrustedRegistryDbContextFactory Factory { get; }

        public static TrustedRegistryFixture Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            var dbPath = Path.Combine(directoryPath, "trusted_registry.db");
            return new TrustedRegistryFixture(directoryPath, new TestTrustedRegistryDbContextFactory(dbPath));
        }

        public async Task MigrateAsync()
        {
            await using var db = await Factory.CreateDbContextAsync();
            await db.Database.MigrateAsync();
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
    }

    private sealed class TestTrustedRegistryDbContextFactory : IDbContextFactory<TrustedRegistryDbContext>
    {
        private readonly DbContextOptions<TrustedRegistryDbContext> _options;

        public TestTrustedRegistryDbContextFactory(string dbPath)
        {
            _options = new DbContextOptionsBuilder<TrustedRegistryDbContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=False")
                .Options;
        }

        public TrustedRegistryDbContext CreateDbContext()
        {
            return new TrustedRegistryDbContext(_options);
        }

        public ValueTask<TrustedRegistryDbContext> CreateDbContextAsync()
        {
            return new ValueTask<TrustedRegistryDbContext>(CreateDbContext());
        }
    }
}
