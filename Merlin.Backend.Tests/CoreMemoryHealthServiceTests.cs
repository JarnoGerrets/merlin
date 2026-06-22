using Merlin.Backend.Core.Memory.Services;
using Merlin.Backend.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Merlin.Backend.Tests;

public sealed class CoreMemoryHealthServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenDatabaseIsQueryable_ReturnsHealthy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MerlinDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new MerlinDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var service = new CoreMemoryHealthService(db, NullLogger<CoreMemoryHealthService>.Instance);

        var result = await service.CheckAsync();

        Assert.True(result.IsHealthy);
        Assert.True(result.DatabaseAvailable);
        Assert.True(result.CanQueryMemory);
        Assert.True(result.CanQueryProfileFacts);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public async Task CheckAsync_WhenDatabaseCannotOpen_ReturnsUnhealthy()
    {
        var options = new DbContextOptionsBuilder<MerlinDbContext>()
            .UseSqlite("Data Source=Z:\\Merlin\\missing\\core-memory.db")
            .Options;
        await using var db = new MerlinDbContext(options);
        var service = new CoreMemoryHealthService(db, NullLogger<CoreMemoryHealthService>.Instance);

        var result = await service.CheckAsync();

        Assert.False(result.IsHealthy);
        Assert.False(result.DatabaseAvailable);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }
}
