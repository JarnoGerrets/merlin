using Merlin.Backend.Core.Memory.Models;
using Merlin.Backend.Core.Memory.Stores;

namespace Merlin.Backend.Core.Memory.Services;

public sealed record MemoryDebugDto(
    string Id,
    string MemoryType,
    string? Title,
    string? Preview,
    double Importance,
    double Confidence,
    bool UserConfirmed,
    IReadOnlyList<string> Concepts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ConceptDebugDto(
    string Id,
    string Name,
    string? ConceptType,
    string? ParentConceptId);

public sealed class MemoryDebugService
{
    private readonly AssociativeRetriever _retriever;
    private readonly CurrentConversationMemoryService _currentConversation;
    private readonly IConceptStore _conceptStore;
    private readonly IMemoryStore _memoryStore;
    private readonly IPromptCompilationStore _promptStore;
    private readonly PromptCompiler _promptCompiler;

    public MemoryDebugService(
        CurrentConversationMemoryService currentConversation,
        IMemoryStore memoryStore,
        IConceptStore conceptStore,
        IPromptCompilationStore promptStore,
        AssociativeRetriever retriever,
        PromptCompiler promptCompiler)
    {
        _currentConversation = currentConversation;
        _memoryStore = memoryStore;
        _conceptStore = conceptStore;
        _promptStore = promptStore;
        _retriever = retriever;
        _promptCompiler = promptCompiler;
    }

    public Task<CurrentConversationState> GetCurrentStateAsync(CancellationToken cancellationToken = default) =>
        _currentConversation.GetOrCreateCurrentStateAsync(cancellationToken);

    public async Task<IReadOnlyList<MemoryDebugDto>> ListMemoriesAsync(
        string? type,
        string? query,
        string? concept,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<string> conceptIds = [];
        if (!string.IsNullOrWhiteSpace(concept))
        {
            var conceptRecord = await _conceptStore.GetConceptByNameAsync(concept, cancellationToken);
            conceptIds = conceptRecord is null ? [] : [conceptRecord.Id];
        }

        var results = await _memoryStore.SearchMemoriesAsync(new MemorySearchRequest
        {
            Query = query,
            MemoryTypes = string.IsNullOrWhiteSpace(type) ? [] : [type],
            ConceptIds = conceptIds,
            Limit = Math.Clamp(limit, 1, 100),
            IncludeExpired = true
        }, cancellationToken);

        var dtos = new List<MemoryDebugDto>();
        foreach (var memory in results.Select(result => result.Memory))
        {
            var concepts = await _conceptStore.GetConceptsForMemoryAsync(memory.Id, cancellationToken);
            dtos.Add(ToDto(memory, concepts.Select(item => item.Name).ToList()));
        }

        return dtos;
    }

    public async Task<object?> GetMemoryDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var memory = await _memoryStore.GetMemoryAsync(id, cancellationToken);
        if (memory is null)
        {
            return null;
        }

        var concepts = await _conceptStore.GetConceptsForMemoryAsync(id, cancellationToken);
        return new
        {
            memory.Id,
            memory.MemoryType,
            memory.Title,
            memory.Content,
            memory.Summary,
            memory.Project,
            memory.Topic,
            memory.Importance,
            memory.Confidence,
            memory.UserConfirmed,
            memory.SourceConversationId,
            memory.SourceTurnId,
            memory.CreatedAt,
            memory.UpdatedAt,
            Concepts = concepts
        };
    }

    public async Task<IReadOnlyList<ConceptDebugDto>> ListConceptsAsync(int limit, CancellationToken cancellationToken = default)
    {
        var concepts = await _conceptStore.ListConceptsAsync(limit, cancellationToken);
        return concepts.Select(concept => new ConceptDebugDto(concept.Id, concept.Name, concept.ConceptType, concept.ParentConceptId)).ToList();
    }

    public async Task<object?> GetConceptDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var concepts = await _conceptStore.ListConceptsAsync(500, cancellationToken);
        var concept = concepts.FirstOrDefault(item => item.Id == id);
        if (concept is null)
        {
            return null;
        }

        var outgoing = await _conceptStore.GetOutgoingEdgesAsync(id, 50, cancellationToken);
        var incoming = await _conceptStore.GetIncomingEdgesAsync(id, 50, cancellationToken);
        var memories = await _conceptStore.GetMemoriesForConceptAsync(id, 50, cancellationToken);
        return new
        {
            Concept = concept,
            OutgoingEdges = outgoing,
            IncomingEdges = incoming,
            Memories = memories.Select(memory => new { memory.Id, memory.MemoryType, memory.Title, memory.Summary, memory.Importance })
        };
    }

    public Task<IReadOnlyList<RetrievedMemory>> RetrieveAsync(string query, int maxResults, CancellationToken cancellationToken = default) =>
        _retriever.RetrieveAsync(new MemoryRetrievalRequest { Query = query, MaxResults = maxResults }, cancellationToken);

    public async Task<PromptCompileResult> CompilePromptAsync(string message, int maxInputTokens, CancellationToken cancellationToken = default)
    {
        var memories = await _retriever.RetrieveAsync(new MemoryRetrievalRequest
        {
            Query = message,
            MaxResults = 8
        }, cancellationToken);

        return await _promptCompiler.CompileAsync(new PromptCompileRequest
        {
            CurrentUserMessage = message,
            PromptType = "debug_compile",
            MaxInputTokens = maxInputTokens,
            RetrievedMemories = memories
        }, cancellationToken);
    }

    public Task<IReadOnlyList<PromptCompilationRecord>> ListPromptCompilationsAsync(int limit, CancellationToken cancellationToken = default) =>
        _promptStore.ListRecentPromptCompilationsAsync(limit, cancellationToken);

    public Task DeleteMemoryAsync(string id, CancellationToken cancellationToken = default) =>
        _memoryStore.DeleteMemoryAsync(id, cancellationToken);

    private static MemoryDebugDto ToDto(MemoryRecord memory, IReadOnlyList<string> concepts)
    {
        var preview = memory.Summary ?? memory.Content;
        if (preview.Length > 180)
        {
            preview = preview[..177] + "...";
        }

        return new MemoryDebugDto(
            memory.Id,
            memory.MemoryType,
            memory.Title,
            preview,
            memory.Importance,
            memory.Confidence,
            memory.UserConfirmed,
            concepts,
            memory.CreatedAt,
            memory.UpdatedAt);
    }
}
