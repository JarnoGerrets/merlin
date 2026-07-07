---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin_sqlite_efcore_persistence_phases/03_phase_3_store_interfaces_and_repositories.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 3 - Store Interfaces and EF Repository Implementations

## Objective

Create clean store interfaces and EF Core repository implementations so future memory logic does not depend directly on `MerlinDbContext`.

This phase creates the persistence API that the memory layer and interruption system will use.

The key idea:

```text
Memory behavior should talk to interfaces.
Persistence implementation should use EF Core internally.
```

## Why this phase matters

Without repository interfaces, later agents may inject `MerlinDbContext` everywhere. That would make the memory system harder to test, harder to refactor, and easier to accidentally over-query.

The future memory system should use services like:

- `IMemoryStore`
- `IConceptStore`
- `IConversationStateStore`
- `ITurnStateStore`
- `IPromptCompilationStore`

It should not directly write EF queries all over the project.

## Folder structure

Create core interfaces and models:

```text
Merlin.Backend/Core/Memory/
├── Models/
│   ├── MemoryRecord.cs
│   ├── ConceptRecord.cs
│   ├── ConceptEdgeRecord.cs
│   ├── ConversationRecord.cs
│   ├── ConversationTopicRecord.cs
│   ├── AssistantTurnRecord.cs
│   ├── PromptCompilationRecord.cs
│   ├── MemorySearchRequest.cs
│   └── MemorySearchResult.cs
└── Stores/
    ├── IMemoryStore.cs
    ├── IConceptStore.cs
    ├── IConversationStateStore.cs
    ├── ITurnStateStore.cs
    └── IPromptCompilationStore.cs
```

Create EF implementations:

```text
Merlin.Backend/Infrastructure/Persistence/Repositories/
├── EfMemoryStore.cs
├── EfConceptStore.cs
├── EfConversationStateStore.cs
├── EfTurnStateStore.cs
└── EfPromptCompilationStore.cs
```

## Model design principle

Core models should be simple records or classes with no EF attributes.

The Infrastructure layer maps between:

```text
Core model records <-> EF entity classes
```

Do not expose EF entities outside Infrastructure.

## IMemoryStore

Required capabilities:

```csharp
public interface IMemoryStore
{
    Task SaveMemoryAsync(MemoryRecord memory, CancellationToken cancellationToken = default);

    Task<MemoryRecord?> GetMemoryAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemorySearchResult>> SearchMemoriesAsync(
        MemorySearchRequest request,
        CancellationToken cancellationToken = default);

    Task UpdateLastAccessedAsync(
        IReadOnlyCollection<string> memoryIds,
        DateTimeOffset accessedAt,
        CancellationToken cancellationToken = default);

    Task DeleteMemoryAsync(string id, CancellationToken cancellationToken = default);
}
```

`SaveMemoryAsync` should upsert if that fits the project's style. If the project prefers separate create/update methods, use those, but be explicit.

## MemorySearchRequest

Suggested shape:

```csharp
public sealed record MemorySearchRequest
{
    public string? Query { get; init; }
    public IReadOnlyCollection<string> ConceptIds { get; init; } = [];
    public IReadOnlyCollection<string> MemoryTypes { get; init; } = [];
    public string? Project { get; init; }
    public string? Topic { get; init; }
    public bool IncludeExpired { get; init; }
    public int Limit { get; init; } = 10;
}
```

The store should apply a safe maximum limit even if the caller asks for too many results.

Example hard cap:

```text
max 100 results
```

The actual MemoryCompiler later will usually ask for much fewer.

## MemorySearchResult

Suggested shape:

```csharp
public sealed record MemorySearchResult
{
    public required MemoryRecord Memory { get; init; }
    public double Score { get; init; }
    public string MatchReason { get; init; } = "unknown";
}
```

At this phase, scoring can be simple. Later the AssociativeRetriever can implement better scoring.

## IConceptStore

Required capabilities:

```csharp
public interface IConceptStore
{
    Task<ConceptRecord> GetOrCreateConceptAsync(
        string name,
        string? conceptType = null,
        CancellationToken cancellationToken = default);

    Task<ConceptRecord?> GetConceptByNameAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConceptRecord>> SearchConceptsAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task LinkMemoryToConceptAsync(
        string memoryId,
        string conceptId,
        double weight,
        CancellationToken cancellationToken = default);

    Task UpsertConceptEdgeAsync(
        string fromConceptId,
        string toConceptId,
        string relationType,
        double weight,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConceptEdgeRecord>> GetOutgoingEdgesAsync(
        string conceptId,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
```

## IConversationStateStore

Required capabilities:

```csharp
public interface IConversationStateStore
{
    Task<ConversationRecord> GetOrCreateActiveConversationAsync(
        CancellationToken cancellationToken = default);

    Task UpdateActiveTopicAsync(
        string conversationId,
        string? activeTopic,
        CancellationToken cancellationToken = default);

    Task<ConversationTopicRecord> StartTopicAsync(
        string conversationId,
        string title,
        CancellationToken cancellationToken = default);

    Task EndTopicAsync(
        string topicId,
        string status,
        string? summary,
        CancellationToken cancellationToken = default);

    Task<ConversationTopicRecord?> GetActiveTopicAsync(
        string conversationId,
        CancellationToken cancellationToken = default);
}
```

For now, `GetOrCreateActiveConversationAsync` can create a single active local conversation if none exists. Later the UI/session system may pass conversation IDs explicitly.

## ITurnStateStore

Required capabilities:

```csharp
public interface ITurnStateStore
{
    Task CreateTurnAsync(AssistantTurnRecord turn, CancellationToken cancellationToken = default);

    Task<AssistantTurnRecord?> GetTurnAsync(string turnId, CancellationToken cancellationToken = default);

    Task UpdateGeneratedTextAsync(
        string turnId,
        string generatedTextSoFar,
        CancellationToken cancellationToken = default);

    Task UpdateSpokenTextAsync(
        string turnId,
        string spokenTextSoFar,
        CancellationToken cancellationToken = default);

    Task UpdateStateAsync(
        string turnId,
        string state,
        CancellationToken cancellationToken = default);

    Task MarkInterruptedAsync(
        string turnId,
        string reason,
        string interruptedByUserMessage,
        CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(string turnId, CancellationToken cancellationToken = default);
}
```

This is critical preparation for future interruption behavior.

## IPromptCompilationStore

Required capabilities:

```csharp
public interface IPromptCompilationStore
{
    Task SavePromptCompilationAsync(
        PromptCompilationRecord promptCompilation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptCompilationRecord>> GetPromptCompilationsForTurnAsync(
        string turnId,
        CancellationToken cancellationToken = default);
}
```

This allows Merlin to audit what was sent to DeepInfra and why.

## EF repository implementation rules

All EF read queries that do not update entities should use:

```csharp
.AsNoTracking()
```

All methods should accept and pass a `CancellationToken`.

Do not silently swallow exceptions. Log useful context, but do not log full memory content unless necessary and safe.

Do not create a singleton DbContext. Use EF Core's normal scoped DbContext registration.

Repository classes can be scoped.

Recommended registration:

```csharp
builder.Services.AddScoped<IMemoryStore, EfMemoryStore>();
builder.Services.AddScoped<IConceptStore, EfConceptStore>();
builder.Services.AddScoped<IConversationStateStore, EfConversationStateStore>();
builder.Services.AddScoped<ITurnStateStore, EfTurnStateStore>();
builder.Services.AddScoped<IPromptCompilationStore, EfPromptCompilationStore>();
```

If the existing app has long-lived services that need these stores, inject `IServiceScopeFactory` there rather than making DbContext singleton.

## Mapping strategy

Create small private mapping methods in each repository or a dedicated mapper class.

Example:

```csharp
private static MemoryRecord ToRecord(MemoryEntity entity)
```

```csharp
private static MemoryEntity ToEntity(MemoryRecord record)
```

No AutoMapper package. The user prefers avoiding packages for simple object mapping.

## Search implementation in this phase

Search can be basic for now.

`SearchMemoriesAsync` should support:

- filter by memory types
- filter by project
- filter by topic
- filter expired memories unless `IncludeExpired` is true
- simple query matching against title/content/summary using `Contains` or `EF.Functions.Like`
- concept filtering through `memory_concepts`
- limit results

Simple score can be computed after retrieval:

```text
importance + concept match bonus + query match bonus + recency light bonus
```

Do not overbuild scoring in this phase.

## Upsert behavior

When saving memory:

- if ID exists, update mutable fields
- if ID does not exist, insert
- always update `UpdatedAt`
- preserve `CreatedAt` for existing records

When linking memory to concept:

- if link exists, update weight
- otherwise insert

When upserting concept edge:

- if edge exists, update weight
- otherwise insert

## Prompt compilation JSON fields

`IncludedMemoryIdsJson` and `IncludedConceptIdsJson` can store JSON arrays as strings.

Example:

```json
["memory-1", "memory-2"]
```

Do not add a JSON package if the built-in `System.Text.Json` is enough.

## Logging requirements

Log at debug/info level:

- memory saved
- memory searched with result count
- concept created
- concept edge upserted
- turn created
- turn interrupted
- prompt compilation saved

Avoid logging full prompt text at info level. Prompt text may be large. For debugging, log prompt ID and token estimate.

## What not to do in Phase 3

Do not:

- implement MemoryCompiler logic yet
- call DeepInfra
- generate TTS
- implement voice interruption
- create a memory dashboard
- add embeddings
- build complex graph spreading activation
- add FTS unless Phase 4 is being started

## Phase 3 acceptance criteria

Phase 3 is complete when:

- All store interfaces exist.
- Core model records exist.
- EF repository implementations exist.
- Repositories are registered in DI.
- Basic CRUD/search works for memories.
- Concepts can be created and linked.
- Conversations/topics can be created and updated.
- Assistant turns can be created, updated, interrupted, and completed.
- Prompt compilations can be saved and queried.
- The backend builds.
- No direct DbContext usage is required by future memory services.

## Suggested final agent message after Phase 3

```text
Phase 3 complete. Store interfaces and EF Core repository implementations are available for memory, concepts, conversations, turns, and prompt compilations. Future memory services can now use these abstractions without touching DbContext directly.
```
