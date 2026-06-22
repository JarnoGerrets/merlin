using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.Persistence.Repositories;

public sealed class EfUserProfileFactStore : IUserProfileFactStore
{
    private readonly MerlinDbContext _db;
    private readonly ILogger<EfUserProfileFactStore> _logger;

    public EfUserProfileFactStore(MerlinDbContext db, ILogger<EfUserProfileFactStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserProfileFact>> GetActiveFactsAsync(
        string profileId,
        CancellationToken cancellationToken = default)
    {
        return await ActiveFacts(profileId)
            .OrderBy(fact => fact.Category)
            .ThenByDescending(fact => fact.Priority)
            .ThenByDescending(fact => fact.UpdatedAt)
            .Select(fact => ToRecord(fact))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfileFact>> GetActiveFactsByCategoryAsync(
        string profileId,
        string category,
        CancellationToken cancellationToken = default)
    {
        return await ActiveFacts(profileId)
            .Where(fact => fact.Category == category)
            .OrderByDescending(fact => fact.Priority)
            .ThenByDescending(fact => fact.UpdatedAt)
            .Select(fact => ToRecord(fact))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserProfileFact?> GetActiveFactByKeyAsync(
        string profileId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var entity = await ActiveFacts(profileId)
            .FirstOrDefaultAsync(fact => fact.Key == key, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserProfileFact?> GetFactAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.UserProfileFacts.AsNoTracking()
            .FirstOrDefaultAsync(fact => fact.Id == id, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<UserProfileFact> SaveFactAsync(
        UserProfileFact fact,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.UserProfileFacts
            .FirstOrDefaultAsync(entity => entity.Id == fact.Id, cancellationToken);

        if (existing is null)
        {
            var now = DateTimeOffset.UtcNow;
            _db.UserProfileFacts.Add(ToEntity(fact with
            {
                CreatedAt = fact.CreatedAt == default ? now : fact.CreatedAt,
                UpdatedAt = fact.UpdatedAt == default ? now : fact.UpdatedAt
            }));
        }
        else
        {
            existing.ProfileId = fact.ProfileId;
            existing.Key = fact.Key;
            existing.Category = fact.Category;
            existing.Value = fact.Value;
            existing.DisplayText = fact.DisplayText;
            existing.Priority = fact.Priority;
            existing.Confidence = fact.Confidence;
            existing.Status = fact.Status;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.LastConfirmedAt = fact.LastConfirmedAt;
            existing.SourceType = fact.SourceType;
            existing.SourceMemoryId = fact.SourceMemoryId;
            existing.SupersedesFactId = fact.SupersedesFactId;
            existing.MetadataJson = fact.MetadataJson;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Saved user profile fact {FactId} for {ProfileId}:{Key}.", fact.Id, fact.ProfileId, fact.Key);
        return (await GetFactAsync(fact.Id, cancellationToken))!;
    }

    public async Task SupersedeFactAsync(
        string oldFactId,
        string supersededByFactId,
        CancellationToken cancellationToken = default)
    {
        var oldFact = await _db.UserProfileFacts
            .FirstOrDefaultAsync(fact => fact.Id == oldFactId, cancellationToken);

        if (oldFact is null)
        {
            return;
        }

        oldFact.Status = UserProfileFactStatuses.Superseded;
        oldFact.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountActiveFactsAsync(string profileId, CancellationToken cancellationToken = default) =>
        ActiveFacts(profileId).CountAsync(cancellationToken);

    private IQueryable<UserProfileFactEntity> ActiveFacts(string profileId) =>
        _db.UserProfileFacts.AsNoTracking()
            .Where(fact => fact.ProfileId == profileId && fact.Status == UserProfileFactStatuses.Active);

    private static UserProfileFactEntity ToEntity(UserProfileFact fact) => new()
    {
        Id = fact.Id,
        ProfileId = fact.ProfileId,
        Key = fact.Key,
        Category = fact.Category,
        Value = fact.Value,
        DisplayText = fact.DisplayText,
        Priority = fact.Priority,
        Confidence = fact.Confidence,
        Status = fact.Status,
        CreatedAt = fact.CreatedAt,
        UpdatedAt = fact.UpdatedAt,
        LastConfirmedAt = fact.LastConfirmedAt,
        SourceType = fact.SourceType,
        SourceMemoryId = fact.SourceMemoryId,
        SupersedesFactId = fact.SupersedesFactId,
        MetadataJson = fact.MetadataJson
    };

    private static UserProfileFact ToRecord(UserProfileFactEntity entity) => new()
    {
        Id = entity.Id,
        ProfileId = entity.ProfileId,
        Key = entity.Key,
        Category = entity.Category,
        Value = entity.Value,
        DisplayText = entity.DisplayText,
        Priority = entity.Priority,
        Confidence = entity.Confidence,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        LastConfirmedAt = entity.LastConfirmedAt,
        SourceType = entity.SourceType,
        SourceMemoryId = entity.SourceMemoryId,
        SupersedesFactId = entity.SupersedesFactId,
        MetadataJson = entity.MetadataJson
    };
}
