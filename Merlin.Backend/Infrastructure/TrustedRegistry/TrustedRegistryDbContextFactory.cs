using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Merlin.Backend.Infrastructure.TrustedRegistry;

public sealed class TrustedRegistryDbContextFactory : IDesignTimeDbContextFactory<TrustedRegistryDbContext>
{
    public TrustedRegistryDbContext CreateDbContext(string[] args)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath = Path.Combine(appData, "Merlin", "db", "trusted_registry.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var optionsBuilder = new DbContextOptionsBuilder<TrustedRegistryDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new TrustedRegistryDbContext(optionsBuilder.Options);
    }
}
