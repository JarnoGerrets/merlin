using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Stores;

public interface ITurnStateStore
{
    Task CreateTurnAsync(AssistantTurnRecord turn, CancellationToken cancellationToken = default);
    Task<AssistantTurnRecord?> GetTurnAsync(string turnId, CancellationToken cancellationToken = default);
    Task UpdateGeneratedTextAsync(string turnId, string generatedTextSoFar, CancellationToken cancellationToken = default);
    Task UpdateSpokenTextAsync(string turnId, string spokenTextSoFar, CancellationToken cancellationToken = default);
    Task UpdateStateAsync(string turnId, string state, CancellationToken cancellationToken = default);
    Task MarkInterruptedAsync(string turnId, string reason, string interruptedByUserMessage, CancellationToken cancellationToken = default);
    Task MarkCompletedAsync(string turnId, CancellationToken cancellationToken = default);
}
