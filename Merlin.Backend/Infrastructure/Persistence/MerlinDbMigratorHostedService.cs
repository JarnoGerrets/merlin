using Microsoft.EntityFrameworkCore;
using Merlin.Backend.Infrastructure.Persistence.Seeding;

namespace Merlin.Backend.Infrastructure.Persistence;

public sealed class MerlinDbMigratorHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MerlinDbMigratorHostedService> _logger;
    private readonly MerlinDbPathResolver _pathResolver;

    public MerlinDbMigratorHostedService(
        IServiceProvider serviceProvider,
        ILogger<MerlinDbMigratorHostedService> logger,
        MerlinDbPathResolver pathResolver)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pathResolver = pathResolver;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dbPath = _pathResolver.ResolveDatabasePath();
        _logger.LogInformation("Applying Merlin database migrations. Path: {DatabasePath}", dbPath);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MerlinDbContext>();
            await db.Database.MigrateAsync(cancellationToken);

            _logger.LogInformation("Merlin database migrations completed.");

            var seeder = scope.ServiceProvider.GetRequiredService<MerlinConceptSeeder>();
            await seeder.SeedAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Merlin database migration failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
