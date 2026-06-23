using Merlin.Backend.Infrastructure.TrustedRegistry.Entities;
using Merlin.Backend.Models;
using Merlin.Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Repositories;

public sealed class EfTrustedCommandStore : ITrustedCommandStore
{
    // Trusted command mappings are quarantined. They are retained for direct
    // diagnostics/import review only and are not active routing by default.
    // Prefer trusted_app_mappings and trusted_url_mappings for operational trust.
    private readonly IDbContextFactory<TrustedRegistryDbContext> _dbContextFactory;

    public EfTrustedCommandStore(IDbContextFactory<TrustedRegistryDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IReadOnlyCollection<TrustedCommandMapping> GetAll()
    {
        using var db = _dbContextFactory.CreateDbContext();
        return db.TrustedCommandMappings
            .AsNoTracking()
            .Where(entity => entity.Status == TrustedRegistryStatuses.Active)
            .OrderByDescending(entity => entity.UseCount)
            .ThenBy(entity => entity.NormalizedOriginalCommand)
            .Select(entity => ToModel(entity))
            .ToArray();
    }

    public TrustedCommandMapping? FindByCommand(string command)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var normalizedCommand = TrustedRegistryNormalizers.NormalizeCommand(command);
        var entity = db.TrustedCommandMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedOriginalCommand == normalizedCommand);

        if (entity is null)
        {
            return null;
        }

        entity.LastUsedAtUtc = DateTimeOffset.UtcNow;
        entity.UseCount += 1;
        db.SaveChanges();

        return ToModel(entity);
    }

    public void SaveMapping(TrustedCommandMapping mapping)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var normalizedOriginalCommand = TrustedRegistryNormalizers.NormalizeCommand(mapping.OriginalCommand);
        var entity = db.TrustedCommandMappings.FirstOrDefault(item =>
            item.Status == TrustedRegistryStatuses.Active
            && item.NormalizedOriginalCommand == normalizedOriginalCommand);

        if (entity is null)
        {
            entity = new TrustedCommandMappingEntity
            {
                CreatedAtUtc = now,
                Status = TrustedRegistryStatuses.Active
            };
            db.TrustedCommandMappings.Add(entity);
        }

        entity.OriginalCommand = mapping.OriginalCommand.Trim();
        entity.NormalizedOriginalCommand = normalizedOriginalCommand;
        entity.Intent = mapping.Intent;
        entity.NormalizedCommand = mapping.NormalizedCommand.Trim();
        entity.ToolName = mapping.ToolName;
        entity.Target = mapping.Target;
        entity.DisplayName = mapping.DisplayName;
        entity.LastUsedAtUtc = now;
        entity.UseCount = Math.Max(1, Math.Max(entity.UseCount, mapping.UseCount));

        db.SaveChanges();
    }

    private static TrustedCommandMapping ToModel(TrustedCommandMappingEntity entity)
    {
        return new TrustedCommandMapping
        {
            OriginalCommand = entity.OriginalCommand,
            NormalizedOriginalCommand = entity.NormalizedOriginalCommand,
            Intent = entity.Intent,
            NormalizedCommand = entity.NormalizedCommand,
            ToolName = entity.ToolName,
            Target = entity.Target,
            DisplayName = entity.DisplayName,
            CreatedAtUtc = entity.CreatedAtUtc,
            LastUsedAtUtc = entity.LastUsedAtUtc,
            UseCount = entity.UseCount
        };
    }
}
