using Merlin.Backend.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Merlin.Backend.Infrastructure.TrustedRegistry;

public sealed class TrustedRegistryMigratorHostedService : IHostedService
{
    private readonly IDbContextFactory<TrustedRegistryDbContext> _dbContextFactory;
    private readonly ILogger<TrustedRegistryMigratorHostedService> _logger;
    private readonly TrustedRegistryLegacyJsonImporter _legacyJsonImporter;
    private readonly TrustedRegistryOptions _options;
    private readonly TrustedRegistryDbPathResolver _pathResolver;

    public TrustedRegistryMigratorHostedService(
        IDbContextFactory<TrustedRegistryDbContext> dbContextFactory,
        IOptions<TrustedRegistryOptions> options,
        ILogger<TrustedRegistryMigratorHostedService> logger,
        TrustedRegistryDbPathResolver pathResolver,
        TrustedRegistryLegacyJsonImporter legacyJsonImporter)
    {
        _dbContextFactory = dbContextFactory;
        _options = options.Value;
        _logger = logger;
        _pathResolver = pathResolver;
        _legacyJsonImporter = legacyJsonImporter;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Trusted registry is disabled; skipping migrations.");
            return;
        }

        var dbPath = _pathResolver.ResolveDatabasePath();
        _logger.LogInformation("Applying trusted registry database migrations. Path: {DatabasePath}", dbPath);

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
            await _legacyJsonImporter.ImportAsync(cancellationToken);

            _logger.LogInformation("Trusted registry database migrations completed.");
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Trusted registry database migration failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
