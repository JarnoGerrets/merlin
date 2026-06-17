using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.Persistence.Repositories;

public sealed class EfConversationStateStore : IConversationStateStore
{
    private readonly MerlinDbContext _db;

    public EfConversationStateStore(MerlinDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationRecord> GetOrCreateActiveConversationAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _db.Conversations.AsNoTracking()
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .FirstOrDefaultAsync(conversation => conversation.Status == "active", cancellationToken);

        if (existing is not null)
        {
            return ToRecord(existing);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ConversationEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Conversations.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task UpdateActiveTopicAsync(string conversationId, string? activeTopic, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations.FirstOrDefaultAsync(entity => entity.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");

        conversation.ActiveTopic = activeTopic;
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationTopicRecord> StartTopicAsync(string conversationId, string title, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new ConversationTopicEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            ConversationId = conversationId,
            Title = title,
            Status = "active",
            StartedAt = now
        };

        _db.ConversationTopics.Add(entity);
        await UpdateConversationTopicAsync(conversationId, title, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task UpdateTopicSummaryAsync(string topicId, string? summary, CancellationToken cancellationToken = default)
    {
        var topic = await _db.ConversationTopics.FirstOrDefaultAsync(entity => entity.Id == topicId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation topic '{topicId}' was not found.");

        topic.Summary = summary;
        var conversation = await _db.Conversations.FirstOrDefaultAsync(entity => entity.Id == topic.ConversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EndTopicAsync(string topicId, string status, string? summary, CancellationToken cancellationToken = default)
    {
        var topic = await _db.ConversationTopics.FirstOrDefaultAsync(entity => entity.Id == topicId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation topic '{topicId}' was not found.");

        topic.Status = status;
        topic.Summary = summary;
        topic.EndedAt = DateTimeOffset.UtcNow;
        await UpdateConversationTopicAsync(topic.ConversationId, null, DateTimeOffset.UtcNow, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConversationTopicRecord?> GetActiveTopicAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.Conversations.AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == conversationId, cancellationToken);
        var query = _db.ConversationTopics.AsNoTracking()
            .Where(topic => topic.ConversationId == conversationId && topic.Status == "active");

        if (!string.IsNullOrWhiteSpace(conversation?.ActiveTopic))
        {
            var activeTopic = conversation.ActiveTopic;
            query = query.Where(topic => topic.Title == activeTopic);
        }

        var entity = await query
            .OrderByDescending(topic => topic.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<ConversationTopicRecord?> GetTopicAsync(string topicId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ConversationTopics.AsNoTracking()
            .FirstOrDefaultAsync(topic => topic.Id == topicId, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    private async Task UpdateConversationTopicAsync(
        string conversationId,
        string? activeTopic,
        DateTimeOffset updatedAt,
        CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations.FirstOrDefaultAsync(entity => entity.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");

        conversation.ActiveTopic = activeTopic;
        conversation.UpdatedAt = updatedAt;
    }

    private static ConversationRecord ToRecord(ConversationEntity entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        ActiveTopic = entity.ActiveTopic,
        Status = entity.Status,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        EndedAt = entity.EndedAt
    };

    private static ConversationTopicRecord ToRecord(ConversationTopicEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        Title = entity.Title,
        Summary = entity.Summary,
        Status = entity.Status,
        StartedAt = entity.StartedAt,
        EndedAt = entity.EndedAt
    };
}
