using Merlin.Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Merlin.Backend.Core.Memory.Services;

public interface ICoreMemoryHealthService
{
    Task<CoreMemoryHealthStatus> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class CoreMemoryHealthStatus
{
    public bool IsHealthy { get; init; }

    public bool DatabaseAvailable { get; init; }

    public bool CanQueryMemory { get; init; }

    public bool CanQueryProfileFacts { get; init; }

    public string? FailureReason { get; init; }
}

public sealed class CoreMemoryHealthService : ICoreMemoryHealthService
{
    private readonly MerlinDbContext _db;
    private readonly ILogger<CoreMemoryHealthService> _logger;

    public CoreMemoryHealthService(MerlinDbContext db, ILogger<CoreMemoryHealthService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CoreMemoryHealthStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Memories.AsNoTracking().Select(memory => memory.Id).Take(1).ToListAsync(cancellationToken);
            await _db.UserProfileFacts.AsNoTracking().Select(fact => fact.Id).Take(1).ToListAsync(cancellationToken);

            return new CoreMemoryHealthStatus
            {
                IsHealthy = true,
                DatabaseAvailable = true,
                CanQueryMemory = true,
                CanQueryProfileFacts = true
            };
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Core Memory health check failed.");
            return new CoreMemoryHealthStatus
            {
                IsHealthy = false,
                DatabaseAvailable = false,
                FailureReason = exception.Message
            };
        }
    }
}
