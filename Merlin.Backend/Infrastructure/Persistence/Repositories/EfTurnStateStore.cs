using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.Persistence.Repositories;

public sealed class EfTurnStateStore : ITurnStateStore
{
    private readonly MerlinDbContext _db;
    private readonly ILogger<EfTurnStateStore> _logger;

    public EfTurnStateStore(MerlinDbContext db, ILogger<EfTurnStateStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CreateTurnAsync(AssistantTurnRecord turn, CancellationToken cancellationToken = default)
    {
        _db.AssistantTurns.Add(new AssistantTurnEntity
        {
            Id = turn.Id,
            ConversationId = turn.ConversationId,
            TopicId = turn.TopicId,
            OriginalUserMessage = turn.OriginalUserMessage,
            GeneratedTextSoFar = turn.GeneratedTextSoFar,
            SpokenTextSoFar = turn.SpokenTextSoFar,
            State = turn.State,
            InterruptionReason = turn.InterruptionReason,
            InterruptedByUserMessage = turn.InterruptedByUserMessage,
            CreatedAt = turn.CreatedAt,
            UpdatedAt = turn.UpdatedAt,
            CompletedAt = turn.CompletedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Created assistant turn {TurnId}.", turn.Id);
    }

    public async Task<AssistantTurnRecord?> GetTurnAsync(string turnId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.AssistantTurns.AsNoTracking().FirstOrDefaultAsync(turn => turn.Id == turnId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public Task UpdateGeneratedTextAsync(string turnId, string generatedTextSoFar, CancellationToken cancellationToken = default) =>
        UpdateTurnAsync(turnId, turn => turn.GeneratedTextSoFar = generatedTextSoFar, cancellationToken);

    public Task UpdateSpokenTextAsync(string turnId, string spokenTextSoFar, CancellationToken cancellationToken = default) =>
        UpdateTurnAsync(turnId, turn => turn.SpokenTextSoFar = spokenTextSoFar, cancellationToken);

    public Task UpdateStateAsync(string turnId, string state, CancellationToken cancellationToken = default) =>
        UpdateTurnAsync(turnId, turn => turn.State = state, cancellationToken);

    public async Task MarkInterruptedAsync(
        string turnId,
        string reason,
        string interruptedByUserMessage,
        CancellationToken cancellationToken = default)
    {
        await UpdateTurnAsync(turnId, turn =>
        {
            turn.State = "interrupted";
            turn.InterruptionReason = reason;
            turn.InterruptedByUserMessage = interruptedByUserMessage;
            turn.CompletedAt = null;
        }, cancellationToken);
        _logger.LogInformation("Assistant turn {TurnId} marked interrupted. Reason: {Reason}.", turnId, reason);
    }

    public Task MarkCompletedAsync(string turnId, CancellationToken cancellationToken = default) =>
        UpdateTurnAsync(turnId, turn =>
        {
            if (turn.State == "interrupted")
            {
                throw new InvalidOperationException("Interrupted turns cannot be marked completed.");
            }

            turn.State = "completed";
            turn.CompletedAt = DateTimeOffset.UtcNow;
        }, cancellationToken);

    private async Task UpdateTurnAsync(
        string turnId,
        Action<AssistantTurnEntity> update,
        CancellationToken cancellationToken)
    {
        var turn = await _db.AssistantTurns.FirstOrDefaultAsync(entity => entity.Id == turnId, cancellationToken)
            ?? throw new InvalidOperationException($"Assistant turn '{turnId}' was not found.");

        update(turn);
        turn.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static AssistantTurnRecord ToRecord(AssistantTurnEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        TopicId = entity.TopicId,
        OriginalUserMessage = entity.OriginalUserMessage,
        GeneratedTextSoFar = entity.GeneratedTextSoFar,
        SpokenTextSoFar = entity.SpokenTextSoFar,
        State = entity.State,
        InterruptionReason = entity.InterruptionReason,
        InterruptedByUserMessage = entity.InterruptedByUserMessage,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
        CompletedAt = entity.CompletedAt
    };
}
