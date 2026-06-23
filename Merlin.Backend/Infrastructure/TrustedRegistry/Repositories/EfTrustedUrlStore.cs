using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Repositories;

public sealed class EfTrustedUrlStore : ITrustedUrlStore
{
    private readonly IDbContextFactory<TrustedRegistryDbContext> _dbContextFactory;

    public EfTrustedUrlStore(IDbContextFactory<TrustedRegistryDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyCollection<TrustedUrlMapping> GetAll()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.TrustedUrlMappings
            .AsNoTracking()
            .Where(entity => entity.Status == TrustedRegistryStatuses.Active)
            .OrderByDescending(entity => entity.UseCount)
            .ThenBy(entity => entity.NormalizedAlias)
            .Select(entity => ToModel(entity))
            .ToArray();
    }

    public TrustedUrlMapping? FindByAlias(string alias)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var normalizedAlias = TrustedRegistryNormalizers.NormalizeUrlAlias(alias);
        var entity = db.TrustedUrlMappings.FirstOrDefault(item =>
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

    public void SaveMapping(string alias, string url, string displayName)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var normalizedAlias = TrustedRegistryNormalizers.NormalizeUrlAlias(alias);
        var entity = db.TrustedUrlMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedAlias == normalizedAlias);

        if (entity is null)
        {
            entity = new TrustedUrlMappingEntity
            {
                CreatedAtUtc = now,
                Status = TrustedRegistryStatuses.Active
            };
            db.TrustedUrlMappings.Add(entity);
        }

        entity.Alias = normalizedAlias;
        entity.NormalizedAlias = normalizedAlias;
        entity.Url = url;
        entity.DisplayName = displayName;
        entity.LastUsedAtUtc = now;
        entity.UseCount = Math.Max(1, entity.UseCount);

        db.SaveChanges();
    }

    public TrustedUrlMapping? UpdateMapping(string alias, string url, string displayName)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var normalizedAlias = TrustedRegistryNormalizers.NormalizeUrlAlias(alias);
        var entity = db.TrustedUrlMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedAlias == normalizedAlias);

        if (entity is null)
        {
            return null;
        }

        entity.Url = url;
        entity.DisplayName = displayName;
        entity.LastUsedAtUtc = DateTimeOffset.UtcNow;
        db.SaveChanges();

        return ToModel(entity);
    }

    public bool DeleteMapping(string alias)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var normalizedAlias = TrustedRegistryNormalizers.NormalizeUrlAlias(alias);
        var entity = db.TrustedUrlMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedAlias == normalizedAlias);

        if (entity is null)
        {
            return false;
        }

        db.TrustedUrlMappings.Remove(entity);
        db.SaveChanges();
        return true;
    }

    private static TrustedUrlMapping ToModel(TrustedUrlMappingEntity entity)
    {
        return new TrustedUrlMapping
        {
            Alias = entity.Alias,
            Url = entity.Url,
            DisplayName = entity.DisplayName,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastUsedAtUtc = entity.LastUsedAtUtc,
            UseCount = entity.UseCount
        };
    }
}
