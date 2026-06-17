using Merlin.Backend.Core.Memory.Models;

namespace Merlin.Backend.Core.Memory.Stores;

public interface IConceptStore
{
    Task<ConceptRecord> GetOrCreateConceptAsync(string name, string? conceptType = null, CancellationToken cancellationToken = default);
    Task<ConceptRecord?> GetConceptByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConceptRecord>> SearchConceptsAsync(string query, int limit = 10, CancellationToken cancellationToken = default);
    Task LinkMemoryToConceptAsync(string memoryId, string conceptId, double weight, CancellationToken cancellationToken = default);
    Task UpsertConceptEdgeAsync(string fromConceptId, string toConceptId, string relationType, double weight, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConceptEdgeRecord>> GetOutgoingEdgesAsync(string conceptId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConceptEdgeRecord>> GetIncomingEdgesAsync(string conceptId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConceptRecord>> ListConceptsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConceptRecord>> GetConceptsForMemoryAsync(string memoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryRecord>> GetMemoriesForConceptAsync(string conceptId, int limit = 50, CancellationToken cancellationToken = default);
}
