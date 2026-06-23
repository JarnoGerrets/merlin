using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Repositories;

public sealed class EfTrustedApplicationStore : ITrustedApplicationStore
{
    private readonly IDbContextFactory<TrustedRegistryDbContext> _dbContextFactory;

    public EfTrustedApplicationStore(IDbContextFactory<TrustedRegistryDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyCollection<TrustedApplicationMapping> GetAll()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.TrustedAppMappings
            .AsNoTracking()
            .Where(entity => entity.Status == TrustedRegistryStatuses.Active)
            .OrderBy(entity => entity.NormalizedAlias)
            .Select(entity => ToModel(entity))
            .ToArray();
    }

    public TrustedApplicationMapping? FindByAlias(string alias)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var normalizedAlias = TrustedRegistryNormalizers.NormalizeApplicationAlias(alias);
        var entity = db.TrustedAppMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedAlias == normalizedAlias);

        if (entity is null)
        {
            return null;
        }

        entity.LastUsedAtUtc = DateTimeOffset.UtcNow;
        entity.UseCount += 1;
        db.SaveChanges();

        return ToModel(entity);
    }

    public void SaveMapping(string alias, ApplicationCandidate candidate)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var normalizedAlias = TrustedRegistryNormalizers.NormalizeApplicationAlias(alias);
        var entity = db.TrustedAppMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedAlias == normalizedAlias);

        if (entity is null)
        {
            entity = new TrustedAppMappingEntity
            {
                CreatedAtUtc = now,
                Status = TrustedRegistryStatuses.Active
            };
            db.TrustedAppMappings.Add(entity);
        }

        entity.Alias = normalizedAlias;
        entity.NormalizedAlias = normalizedAlias;
        entity.DisplayName = candidate.DisplayName;
        entity.ExecutablePath = candidate.ExecutablePath;
        entity.Source = candidate.Source;
        entity.Confidence = candidate.Confidence;
        entity.LastUsedAtUtc = now;
        entity.UseCount = Math.Max(1, entity.UseCount);

        db.SaveChanges();
    }

    private static TrustedApplicationMapping ToModel(TrustedAppMappingEntity entity)
    {
        return new TrustedApplicationMapping
        {
            Alias = entity.Alias,
            DisplayName = entity.DisplayName,
            ExecutablePath = entity.ExecutablePath,
            Source = entity.Source,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastUsedAtUtc = entity.LastUsedAtUtc
        };
    }
}
