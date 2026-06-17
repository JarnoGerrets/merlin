using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.Persistence.Repositories;

public sealed class EfMemoryStore : IMemoryStore
{
    private const int MaxSearchLimit = 100;
    private readonly MerlinDbContext _db;
    private readonly ILogger<EfMemoryStore> _logger;

    public EfMemoryStore(MerlinDbContext db, ILogger<EfMemoryStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveMemoryAsync(MemoryRecord memory, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Memories.FirstOrDefaultAsync(entity => entity.Id == memory.Id, cancellationToken);
        if (existing is null)
        {
            _db.Memories.Add(ToEntity(memory with
            {
                CreatedAt = memory.CreatedAt == default ? DateTimeOffset.UtcNow : memory.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow
            }));
        }
        else
        {
            existing.MemoryType = memory.MemoryType;
            existing.Title = memory.Title;
            existing.Content = memory.Content;
            existing.Summary = memory.Summary;
            existing.Project = memory.Project;
            existing.Topic = memory.Topic;
            existing.Importance = memory.Importance;
            existing.Confidence = memory.Confidence;
            existing.UserConfirmed = memory.UserConfirmed;
            existing.LastAccessedAt = memory.LastAccessedAt;
            existing.ExpiresAt = memory.ExpiresAt;
            existing.Source = memory.Source;
            existing.SourceConversationId = memory.SourceConversationId;
            existing.SourceTurnId = memory.SourceTurnId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Saved memory {MemoryId} of type {MemoryType}.", memory.Id, memory.MemoryType);
    }

    public async Task<MemoryRecord?> GetMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Memories.AsNoTracking().FirstOrDefaultAsync(memory => memory.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<MemorySearchResult>> SearchMemoriesAsync(
        MemorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var limit = Math.Clamp(request.Limit, 1, MaxSearchLimit);
        var queryText = request.Query?.Trim();
        var conceptIds = request.ConceptIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        var memoryTypes = request.MemoryTypes.Where(type => !string.IsNullOrWhiteSpace(type)).Distinct().ToArray();

        IQueryable<MemoryEntity> query = _db.Memories.AsNoTracking().Include(memory => memory.MemoryConcepts);

        if (!request.IncludeExpired)
        {
            query = query.Where(memory => memory.ExpiresAt == null || memory.ExpiresAt > now);
        }

        if (memoryTypes.Length > 0)
        {
            query = query.Where(memory => memoryTypes.Contains(memory.MemoryType));
        }

        if (!string.IsNullOrWhiteSpace(request.Project))
        {
            query = query.Where(memory => memory.Project == request.Project);
        }

        if (!string.IsNullOrWhiteSpace(request.Topic))
        {
            query = query.Where(memory => memory.Topic == request.Topic);
        }

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            var pattern = $"%{queryText}%";
            query = query.Where(memory =>
                (memory.Title != null && EF.Functions.Like(memory.Title, pattern)) ||
                EF.Functions.Like(memory.Content, pattern) ||
                (memory.Summary != null && EF.Functions.Like(memory.Summary, pattern)));
        }

        if (conceptIds.Length > 0)
        {
            query = query.Where(memory => memory.MemoryConcepts.Any(link => conceptIds.Contains(link.ConceptId)));
        }

        var entities = await query
            .OrderByDescending(memory => memory.Importance)
            .ThenByDescending(memory => memory.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var results = entities
            .Select(entity => ToSearchResult(entity, queryText, conceptIds, request))
            .OrderByDescending(result => result.Score)
            .ToList();

        _logger.LogDebug("Memory search returned {ResultCount} results.", results.Count);
        return results;
    }

    public async Task UpdateLastAccessedAsync(
        IReadOnlyCollection<string> memoryIds,
        DateTimeOffset accessedAt,
        CancellationToken cancellationToken = default)
    {
        var ids = memoryIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var memories = await _db.Memories.Where(memory => ids.Contains(memory.Id)).ToListAsync(cancellationToken);
        foreach (var memory in memories)
        {
            memory.LastAccessedAt = accessedAt;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        var memory = await _db.Memories.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (memory is null)
        {
            return;
        }

        _db.Memories.Remove(memory);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static MemoryEntity ToEntity(MemoryRecord record) => new()
    {
        Id = record.Id,
        MemoryType = record.MemoryType,
        Title = record.Title,
        Content = record.Content,
        Summary = record.Summary,
        Project = record.Project,
        Topic = record.Topic,
        Importance = record.Importance,
        Confidence = record.Confidence,
        UserConfirmed = record.UserConfirmed,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
        LastAccessedAt = record.LastAccessedAt,
        ExpiresAt = record.ExpiresAt,
        Source = record.Source,
        SourceConversationId = record.SourceConversationId,
        SourceTurnId = record.SourceTurnId
    };

    private static MemoryRecord ToRecord(MemoryEntity entity) => new()
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

    private static MemorySearchResult ToSearchResult(
        MemoryEntity entity,
        string? queryText,
        IReadOnlyCollection<string> conceptIds,
        MemorySearchRequest request)
    {
        var score = entity.Importance;
        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(queryText))
        {
            if (Contains(entity.Title, queryText))
            {
                score += 0.30;
                reasons.Add("query:title");
            }

            if (Contains(entity.Summary, queryText))
            {
                score += 0.20;
                reasons.Add("query:summary");
            }

            if (Contains(entity.Content, queryText))
            {
                score += 0.10;
                reasons.Add("query:content");
            }
        }

        if (conceptIds.Count > 0 && entity.MemoryConcepts.Any(link => conceptIds.Contains(link.ConceptId)))
        {
            score += 0.25;
            reasons.Add("concept:direct");
        }

        if (!string.IsNullOrWhiteSpace(request.Project) && string.Equals(entity.Project, request.Project, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.10;
            reasons.Add("project:match");
        }

        return new MemorySearchResult
        {
            Memory = ToRecord(entity),
            Score = score,
            MatchReason = reasons.Count == 0 ? "filter:match" : string.Join(",", reasons)
        };
    }

    private static bool Contains(string? value, string query) =>
        value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
}
