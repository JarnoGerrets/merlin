using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Stores;

public interface IPromptCompilationStore
{
    Task SavePromptCompilationAsync(PromptCompilationRecord promptCompilation, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptCompilationRecord>> GetPromptCompilationsForTurnAsync(string turnId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PromptCompilationRecord>> ListRecentPromptCompilationsAsync(int limit = 20, CancellationToken cancellationToken = default);
}
