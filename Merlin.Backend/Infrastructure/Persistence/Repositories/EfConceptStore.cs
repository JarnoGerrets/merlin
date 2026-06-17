using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.Persistence.Repositories;

public sealed class EfConceptStore : IConceptStore
{
    private readonly MerlinDbContext _db;
    private readonly ILogger<EfConceptStore> _logger;

    public EfConceptStore(MerlinDbContext db, ILogger<EfConceptStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ConceptRecord> GetOrCreateConceptAsync(
        string name,
        string? conceptType = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        var existing = await _db.Concepts.FirstOrDefaultAsync(
            concept => concept.Name.ToLower() == normalized,
            cancellationToken);

        if (existing is not null)
        {
            return ToRecord(existing);
        }

        var entity = new ConceptEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            ConceptType = conceptType,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Concepts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created concept {ConceptName}.", entity.Name);
        return ToRecord(entity);
    }

    public async Task<ConceptRecord?> GetConceptByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        var entity = await _db.Concepts.AsNoTracking().FirstOrDefaultAsync(
            concept => concept.Name.ToLower() == normalized,
            cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<ConceptRecord>> SearchConceptsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        var pattern = $"%{query.Trim()}%";
        return await _db.Concepts.AsNoTracking()
            .Where(concept => EF.Functions.Like(concept.Name, pattern))
            .OrderBy(concept => concept.Name)
            .Take(safeLimit)
            .Select(concept => ToRecord(concept))
            .ToListAsync(cancellationToken);
    }

    public async Task LinkMemoryToConceptAsync(
        string memoryId,
        string conceptId,
        double weight,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.MemoryConcepts.FirstOrDefaultAsync(
            link => link.MemoryId == memoryId && link.ConceptId == conceptId,
            cancellationToken);

        if (existing is null)
        {
            _db.MemoryConcepts.Add(new MemoryConceptEntity
            {
                MemoryId = memoryId,
                ConceptId = conceptId,
                Weight = weight
            });
        }
        else
        {
            existing.Weight = weight;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertConceptEdgeAsync(
        string fromConceptId,
        string toConceptId,
        string relationType,
        double weight,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.ConceptEdges.FirstOrDefaultAsync(
            edge => edge.FromConceptId == fromConceptId &&
                    edge.ToConceptId == toConceptId &&
                    edge.RelationType == relationType,
            cancellationToken);

        if (existing is null)
        {
            _db.ConceptEdges.Add(new ConceptEdgeEntity
            {
                FromConceptId = fromConceptId,
                ToConceptId = toConceptId,
                RelationType = relationType,
                Weight = weight
            });
        }
        else
        {
            existing.Weight = weight;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Upserted concept edge {FromConceptId} -{RelationType}-> {ToConceptId}.", fromConceptId, relationType, toConceptId);
    }

    public async Task<IReadOnlyList<ConceptEdgeRecord>> GetOutgoingEdgesAsync(
        string conceptId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return await _db.ConceptEdges.AsNoTracking()
            .Where(edge => edge.FromConceptId == conceptId)
            .OrderByDescending(edge => edge.Weight)
            .Take(safeLimit)
            .Select(edge => ToRecord(edge))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConceptEdgeRecord>> GetIncomingEdgesAsync(
        string conceptId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return await _db.ConceptEdges.AsNoTracking()
            .Where(edge => edge.ToConceptId == conceptId)
            .OrderByDescending(edge => edge.Weight)
            .Take(safeLimit)
            .Select(edge => ToRecord(edge))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConceptRecord>> ListConceptsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);
        return await _db.Concepts.AsNoTracking()
            .OrderBy(concept => concept.Name)
            .Take(safeLimit)
            .Select(concept => ToRecord(concept))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ConceptRecord>> GetConceptsForMemoryAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        return await _db.MemoryConcepts.AsNoTracking()
            .Where(link => link.MemoryId == memoryId)
            .Include(link => link.Concept)
            .OrderByDescending(link => link.Weight)
            .Select(link => ToRecord(link.Concept))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryRecord>> GetMemoriesForConceptAsync(
        string conceptId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return await _db.MemoryConcepts.AsNoTracking()
            .Where(link => link.ConceptId == conceptId)
            .Include(link => link.Memory)
            .OrderByDescending(link => link.Weight)
            .ThenByDescending(link => link.Memory.Importance)
            .Take(safeLimit)
            .Select(link => ToMemoryRecord(link.Memory))
            .ToListAsync(cancellationToken);
    }

    private static string NormalizeName(string name) => name.Trim().ToLowerInvariant();

    private static ConceptRecord ToRecord(ConceptEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        ConceptType = entity.ConceptType,
        ParentConceptId = entity.ParentConceptId,
        CreatedAt = entity.CreatedAt
    };

    private static ConceptEdgeRecord ToRecord(ConceptEdgeEntity entity) => new()
    {
        FromConceptId = entity.FromConceptId,
        ToConceptId = entity.ToConceptId,
        RelationType = entity.RelationType,
        Weight = entity.Weight
    };

    private static MemoryRecord ToMemoryRecord(MemoryEntity entity) => new()
    {
        Id = entity.Id,
        MemoryType = entity.MemoryType,
        Title = entity.Title,
        Content = entity.Content,
        Summary = entity.Summary,
        Project = entity.Project,
        Topic = entity.Topic,
        Importance = entity.Importance,
        Confidence = entity.Confidence,
        UserConfirmed = entity.UserConfirmed,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        LastAccessedAt = entity.LastAccessedAt,
        ExpiresAt = entity.ExpiresAt,
        Source = entity.Source,
        SourceConversationId = entity.SourceConversationId,
        SourceTurnId = entity.SourceTurnId
    };
}
