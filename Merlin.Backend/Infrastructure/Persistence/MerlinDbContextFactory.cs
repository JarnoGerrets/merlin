using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Merlin.Backend.Infrastructure.Persistence;

public sealed class MerlinDbContextFactory : IDesignTimeDbContextFactory<MerlinDbContext>
{
    public MerlinDbContext CreateDbContext(string[] args)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dbPath = System.IO.Path.Combine(appData, "Merlin", "db", "merlin_memory.db");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);

        var optionsBuilder = new DbContextOptionsBuilder<MerlinDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new MerlinDbContext(optionsBuilder.Options);
    }
}
