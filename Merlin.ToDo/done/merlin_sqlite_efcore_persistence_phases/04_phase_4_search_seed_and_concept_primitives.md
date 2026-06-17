# Phase 4 - Search, Seed Data, and Concept Primitives

## Objective

Add the first practical search and concept primitives on top of the EF Core persistence foundation.

This phase should make Merlin's database useful for the future brain-like memory system without yet implementing the complete MemoryCompiler or AssociativeRetriever.

The goal is to prepare:

- seed concepts
- basic concept graph edges
- simple memory search
- optional SQLite FTS5 preparation
- stable primitives for later associative retrieval

## Why this phase matters

The memory system is supposed to behave like a human filing cabinet.

When the user says:

```text
SQLite
```

Merlin should eventually activate concepts like:

```text
local database
memory storage
Merlin persistence
EF Core
prompt compiler
```

This requires a concept layer before the full memory layer starts.

## Seed concepts

Create a seeding service:

```text
Merlin.Backend/Infrastructure/Persistence/Seeding/MerlinConceptSeeder.cs
```

It should run after migrations on startup.

Recommended approach:

- idempotent seeding
- use `IConceptStore` if available
- or use `MerlinDbContext` directly if this is part of persistence startup
- do not duplicate concepts
- do not overwrite user-modified concepts unnecessarily

## Required seed concepts

Seed at least these concepts:

```text
Merlin
memory
current conversation
medium memory
long-term memory
associative retrieval
concept graph
filing cabinet
SQLite
EF Core
DeepInfra
prompt compiler
token reduction
conversation state
assistant turn
interruption
correction
voice
TTS
STT
local tool
tool preference
user preference
project decision
```

Use lower-case normalization for lookup if needed, but preserve display names if the UI may show them later.

## Concept normalization

Add a small concept name normalizer.

Target behavior:

```text
"SQLite" -> lookup key "sqlite"
"DeepInfra" -> lookup key "deepinfra"
"Long-Term Memory" -> lookup key "long-term memory"
```

If the current schema only has `Name`, then enforce uniqueness case-insensitively in code. If needed later, add a `NormalizedName` column, but do not overcomplicate unless necessary.

Recommended improvement if migration is acceptable:

```text
ConceptEntity.Name
ConceptEntity.NormalizedName
```

Unique index on `NormalizedName`.

If adding this column, create a new EF migration.

## Seed concept edges

Seed useful relations:

```text
current conversation --part_of--> memory
medium memory --part_of--> memory
long-term memory --part_of--> memory
associative retrieval --used_for--> memory
concept graph --used_for--> associative retrieval
filing cabinet --example_of--> associative retrieval
SQLite --used_for--> memory
EF Core --used_for--> SQLite
DeepInfra --used_for--> reasoning
prompt compiler --used_for--> token reduction
conversation state --used_for--> interruption
assistant turn --part_of--> conversation state
correction --related_to--> interruption
voice --related_to--> interruption
TTS --related_to--> voice
STT --related_to--> voice
user preference --is_a--> long-term memory
project decision --is_a--> long-term memory
tool preference --is_a--> long-term memory
```

These edges are not meant to be perfect. They are a starter graph.

## Basic memory search service

Create a search service on top of stores.

Suggested interface:

```text
Merlin.Backend/Core/Memory/Search/IMemorySearchService.cs
```

Suggested shape:

```csharp
public interface IMemorySearchService
{
    Task<IReadOnlyList<MemorySearchResult>> SearchAsync(
        MemorySearchRequest request,
        CancellationToken cancellationToken = default);
}
```

Implementation can initially delegate mostly to `IMemoryStore.SearchMemoriesAsync`.

Later, the real AssociativeRetriever can replace/extend this.

## Search scoring MVP

For now, search scoring should be simple and explainable.

Potential scoring:

```text
base = memory.Importance
+ 0.30 if query matched title
+ 0.20 if query matched summary
+ 0.10 if query matched content
+ 0.25 for direct concept match
+ 0.10 for project match
- 0.20 if memory is old and low importance
```

Do not obsess over perfect scoring in this phase.

The important thing is that results include a `MatchReason` so later debugging is easier.

Examples:

```text
query:title
query:summary
concept:sqlite
concept:memory
project:merlin
```

## Optional FTS5 support

SQLite FTS5 can improve keyword search.

EF Core does not natively make FTS5 feel as clean as normal tables, so this should be done carefully.

Only add FTS5 if it does not destabilize the phase.

If adding FTS5, use an EF migration with raw SQL:

```csharp
migrationBuilder.Sql(@"
CREATE VIRTUAL TABLE IF NOT EXISTS memories_fts USING fts5(
    memory_id UNINDEXED,
    title,
    content,
    summary,
    project,
    topic
);
");
```

Then keep it synced from repository methods when saving/updating/deleting memories.

For MVP, repository-managed sync is acceptable.

Do not create complicated triggers unless the agent is confident and tests them.

## Repository FTS sync behavior if FTS is added

When a memory is saved:

1. Insert/update the normal `memories` table through EF.
2. Remove old FTS row for that memory ID.
3. Insert new FTS row.

When a memory is deleted:

1. Delete from `memories`.
2. Delete from `memories_fts`.

Because EF does not directly model the FTS virtual table, use `Database.ExecuteSqlRawAsync` carefully.

## If FTS is not added yet

This is acceptable.

The phase can still be complete with:

- normal EF indexes
- `LIKE` search
- concept linking
- seed concepts
- seed edges

FTS can be deferred to the full memory implementation if necessary.

## Concept extraction primitive

Create a very simple local concept extractor.

Suggested interface:

```csharp
public interface IConceptExtractionService
{
    IReadOnlyList<string> ExtractConceptNames(string text);
}
```

MVP behavior:

- lowercase input
- match known seed concepts by phrase
- optionally split simple keywords
- ignore very short words
- return distinct concept names

Example:

Input:

```text
Should Merlin use SQLite for the memory system before DeepInfra interruption handling?
```

Output:

```text
Merlin
SQLite
memory
DeepInfra
interruption
```

Do not call DeepInfra for concept extraction in this phase.

## Why concept extraction should be local

The point of Merlin's memory system is to reduce cloud token usage.

If every local memory operation calls DeepInfra, the system defeats itself.

Local concept extraction can be simple at first and improved later.

## Seed smoke test

Add a dev-only smoke operation that proves:

1. seed concepts exist
2. a memory can be saved
3. concepts can be extracted from text
4. memory can be linked to concepts
5. memory can be found by query
6. memory can be found by concept

Possible dev endpoint:

```text
POST /dev/memory/smoke-test
```

Or a dev-menu command if the project avoids dev HTTP endpoints.

Do not expose this in production if not appropriate.

## Example smoke memory

```text
Title: Merlin memory SQLite decision
Content: Merlin should use SQLite under AppData as the local memory database foundation.
Concepts: Merlin, memory, SQLite, EF Core
MemoryType: project_decision
Importance: 0.9
UserConfirmed: true
```

Expected search behavior:

Query:

```text
SQLite memory
```

Should return that memory.

Concept:

```text
SQLite
```

Should return that memory.

## Logging requirements

Log:

- seed start/end
- concept created/skipped
- edge created/skipped
- search query and result count
- smoke test success/failure

Do not log full large memory bodies at info level.

## What not to do in Phase 4

Do not:

- call DeepInfra for search
- add embeddings
- build full spreading activation
- build MemoryCompiler
- build UI dashboard
- implement voice interruption
- store raw chat history as memory
- make seed concepts enormous

## Phase 4 acceptance criteria

Phase 4 is complete when:

- Seed concept service exists.
- Core concepts are seeded idempotently.
- Core concept edges are seeded idempotently.
- Basic concept extraction service exists.
- Basic memory search service exists.
- Memory search can use text and concept filters.
- Optional FTS either works or is explicitly deferred.
- Smoke test proves insert/link/search works.
- The code builds.
- The database still lives under AppData.

## Suggested final agent message after Phase 4

```text
Phase 4 complete. Merlin now has seeded memory/concept primitives, basic local concept extraction, and searchable memory records. The system can insert a memory, link concepts, and retrieve it by text or concept without calling DeepInfra.
```
