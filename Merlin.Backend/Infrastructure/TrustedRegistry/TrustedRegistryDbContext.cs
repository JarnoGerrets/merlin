using System.Globalization;
using Merlin.Backend.Infrastructure.TrustedRegistry.Configurations;
using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Merlin.Backend.Infrastructure.TrustedRegistry;

public sealed class TrustedRegistryDbContext : DbContext
{
    public TrustedRegistryDbContext(DbContextOptions<TrustedRegistryDbContext> options)
        : base(options)
    {
    }

    public DbSet<TrustedAppMappingEntity> TrustedAppMappings => Set<TrustedAppMappingEntity>();

    public DbSet<TrustedCommandMappingEntity> TrustedCommandMappings => Set<TrustedCommandMappingEntity>();

    public DbSet<TrustedUrlMappingEntity> TrustedUrlMappings => Set<TrustedUrlMappingEntity>();

    public DbSet<TrustedRegistryEventEntity> TrustedRegistryEvents => Set<TrustedRegistryEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TrustedAppMappingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TrustedCommandMappingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TrustedUrlMappingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TrustedRegistryEventEntityConfiguration());
        ConfigureDateTimeOffsetStorage(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureDateTimeOffsetStorage(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, string>(
            value => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            value => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, string?>(
            value => value.HasValue ? value.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) : null,
            value => value == null ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDateTimeOffsetConverter);
                }
            }
        }
    }
}
