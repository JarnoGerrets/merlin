using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;
using Merlin.Backend.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Infrastructure.Persistence.Repositories;

public sealed class EfPromptCompilationStore : IPromptCompilationStore
{
    private readonly MerlinDbContext _db;
    private readonly ILogger<EfPromptCompilationStore> _logger;

    public EfPromptCompilationStore(MerlinDbContext db, ILogger<EfPromptCompilationStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SavePromptCompilationAsync(
        PromptCompilationRecord promptCompilation,
        CancellationToken cancellationToken = default)
    {
        _db.PromptCompilations.Add(new PromptCompilationEntity
        {
            Id = promptCompilation.Id,
            ConversationId = promptCompilation.ConversationId,
            TurnId = promptCompilation.TurnId,
            PromptType = promptCompilation.PromptType,
            CompiledPrompt = promptCompilation.CompiledPrompt,
            EstimatedInputTokens = promptCompilation.EstimatedInputTokens,
            IncludedMemoryIdsJson = promptCompilation.IncludedMemoryIdsJson,
            IncludedConceptIdsJson = promptCompilation.IncludedConceptIdsJson,
            CreatedAt = promptCompilation.CreatedAt
        });

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogDebug(
            "Saved prompt compilation {PromptCompilationId}. PromptType: {PromptType}. EstimatedInputTokens: {EstimatedInputTokens}.",
            promptCompilation.Id,
            promptCompilation.PromptType,
            promptCompilation.EstimatedInputTokens);
    }

    public async Task<IReadOnlyList<PromptCompilationRecord>> GetPromptCompilationsForTurnAsync(
        string turnId,
        CancellationToken cancellationToken = default)
    {
        return await _db.PromptCompilations.AsNoTracking()
            .Where(prompt => prompt.TurnId == turnId)
            .OrderBy(prompt => prompt.CreatedAt)
            .Select(prompt => ToRecord(prompt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PromptCompilationRecord>> ListRecentPromptCompilationsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 100);
        return await _db.PromptCompilations.AsNoTracking()
            .OrderByDescending(prompt => prompt.CreatedAt)
            .Take(safeLimit)
            .Select(prompt => ToRecord(prompt))
            .ToListAsync(cancellationToken);
    }

    private static PromptCompilationRecord ToRecord(PromptCompilationEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        TurnId = entity.TurnId,
        PromptType = entity.PromptType,
        CompiledPrompt = entity.CompiledPrompt,
        EstimatedInputTokens = entity.EstimatedInputTokens,
        IncludedMemoryIdsJson = entity.IncludedMemoryIdsJson,
        IncludedConceptIdsJson = entity.IncludedConceptIdsJson,
        CreatedAt = entity.CreatedAt
    };
}
