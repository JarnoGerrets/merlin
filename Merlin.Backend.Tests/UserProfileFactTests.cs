using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Infrastructure.Persistence;
using Merlin.Backend.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class UserProfileFactTests
{
    [Fact]
    public async Task EfUserProfileFactStore_SavesRetrievesSupersedesAndCountsActiveFacts()
    {
        await using var fixture = await ProfileFactFixture.CreateAsync();
        var first = await fixture.Store.SaveFactAsync(CreateFact("response.length.default", "short"));
        var second = await fixture.Store.SaveFactAsync(CreateFact("response.tone.default", "direct"));

        var active = await fixture.Store.GetActiveFactsAsync(UserProfileDefaults.ProfileId);
        var byKey = await fixture.Store.GetActiveFactByKeyAsync(UserProfileDefaults.ProfileId, "response.length.default");

        Assert.Equal(2, active.Count);
        Assert.Equal(first.Id, byKey!.Id);
        Assert.Equal(2, await fixture.Store.CountActiveFactsAsync(UserProfileDefaults.ProfileId));

        await fixture.Store.SupersedeFactAsync(first.Id, second.Id);

        Assert.Null(await fixture.Store.GetActiveFactByKeyAsync(UserProfileDefaults.ProfileId, "response.length.default"));
        Assert.Equal(1, await fixture.Store.CountActiveFactsAsync(UserProfileDefaults.ProfileId));
    }

    [Theory]
    [InlineData("I want short responses", "response.length.default", "short")]
    [InlineData("I prefer medium to long responses", "response.length.default", "medium_to_long")]
    [InlineData("I want you to be concise", "response.style.conciseness", "concise")]
    [InlineData("I want critical feedback", "response.criticism.default", "critical")]
    [InlineData("I don't want object mapping packages", "coding.dependencies.object_mapping", "avoid")]
    [InlineData("Merlin should fail closed if memory is unavailable", "merlin.runtime.memory_required", "fail_closed")]
    [InlineData("Save that I do not want fallback braindead mode", "merlin.runtime.memory_required", "fail_closed")]
    public void UserProfileFactDetector_DetectsExplicitPreferences(string input, string expectedKey, string expectedValue)
    {
        var detector = new UserProfileFactDetector();

        var detected = detector.TryDetect(input, out var candidate);

        Assert.True(detected);
        Assert.Equal(expectedKey, candidate.Key);
        Assert.Equal(expectedValue, candidate.Value);
    }

    [Fact]
    public void UserProfileFactDetector_IgnoresOrdinaryConversation()
    {
        var detector = new UserProfileFactDetector();

        var detected = detector.TryDetect("Could you explain how SQLite indexing works?", out _);

        Assert.False(detected);
    }

    [Fact]
    public async Task UserProfileFactService_UpsertCreatesDedupesAndSupersedesSameKey()
    {
        await using var fixture = await ProfileFactFixture.CreateAsync();
        var service = new UserProfileFactService(fixture.Store);

        var created = await service.UpsertAsync(Candidate("short", "Jarno prefers short responses by default."));
        var duplicate = await service.UpsertAsync(Candidate("short", "Jarno prefers short responses by default."));
        var updated = await service.UpsertAsync(Candidate("medium_to_long", "Jarno prefers medium-to-long responses by default."));

        Assert.True(created.Created);
        Assert.True(duplicate.NoOpDuplicate);
        Assert.True(updated.Updated);
        Assert.NotNull(updated.SupersededFact);
        Assert.Contains("instead of short", updated.AcknowledgementText);
        Assert.Equal(1, await fixture.Store.CountActiveFactsAsync(UserProfileDefaults.ProfileId));

        var active = await fixture.Store.GetActiveFactByKeyAsync(UserProfileDefaults.ProfileId, "response.length.default");
        var superseded = await fixture.Store.GetFactAsync(created.ActiveFact.Id);

        Assert.Equal("medium_to_long", active!.Value);
        Assert.Equal(UserProfileFactStatuses.Superseded, superseded!.Status);
    }

    private static ProfileFactCandidate Candidate(string value, string displayText) => new()
    {
        Key = "response.length.default",
        Category = "response_preferences",
        Value = value,
        DisplayText = displayText
    };

    private static UserProfileFact CreateFact(string key, string value)
    {
        var now = DateTimeOffset.UtcNow;
        return new UserProfileFact
        {
            Id = Guid.NewGuid().ToString("N"),
            Key = key,
            Category = "response_preferences",
            Value = value,
            DisplayText = value,
            Status = UserProfileFactStatuses.Active,
            SourceType = UserProfileFactSourceTypes.ExplicitUserInstruction,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed class ProfileFactFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private ProfileFactFixture(SqliteConnection connection, MerlinDbContext db)
        {
            _connection = connection;
            Db = db;
            Store = new EfUserProfileFactStore(db, NullLogger<EfUserProfileFactStore>.Instance);
        }

        public MerlinDbContext Db { get; }
        public EfUserProfileFactStore Store { get; }

        public static async Task<ProfileFactFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<MerlinDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new MerlinDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new ProfileFactFixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
