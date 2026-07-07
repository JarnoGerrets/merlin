---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin_sqlite_efcore_persistence_phases/06_phase_6_verification_handoff_to_memory_layer.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Phase 6 - Verification, Documentation, and Handoff to the Memory Layer

## Objective

Verify that the SQLite + EF Core persistence foundation is complete, stable, and ready for the full brain-like memory layer implementation.

This phase is about proving the foundation works and documenting exactly how the next agent should use it.

At the end of this phase, the agent should be able to move to the larger Merlin memory architecture Markdown file and start implementing memory behavior on top of this persistence foundation.

## What should exist before Phase 6 starts

The following should already exist:

- EF Core SQLite packages
- AppData database path resolver
- DbContext registration
- startup migration service
- EF entities and configurations
- initial migration
- AppData database file
- store interfaces
- EF repository implementations
- seed concepts and seed edges
- basic memory search
- runtime conversation state
- assistant turn tracking
- prompt compilation logging

If any of those are missing, do not fake Phase 6. Go back to the relevant phase.

## Verification checklist

### 1. Build check

Run the backend build.

```bash
dotnet build
```

If the solution has multiple projects, run from the solution root if appropriate.

Expected:

```text
Build succeeded.
```

Do not accept a foundation that only works partially or requires commented-out code.

### 2. Startup check

Start Merlin.Backend normally.

Expected logs should show:

```text
Applying Merlin database migrations
Database path: <AppData>/Merlin/db/merlin_memory.db
Merlin database migrations completed
Concept seed completed
```

The exact wording can differ, but logs must reveal:

- DB path
- migration success
- seed success

### 3. AppData location check

Verify that the database exists under AppData.

Expected Windows-equivalent path:

```text
C:\Users\<username>\AppData\Roaming\Merlin\db\merlin_memory.db
```

Important:

```text
There must not be a merlin_memory.db file inside the source code repository unless it is explicitly a test artifact ignored by git.
```

### 4. Schema check

Inspect SQLite using a DB browser or command-line tool.

Required tables:

```text
memories
concepts
memory_concepts
concept_edges
conversations
conversation_topics
assistant_turns
prompt_compilations
__EFMigrationsHistory
```

If FTS was added, also expect:

```text
memories_fts
```

### 5. Seed check

Verify core concepts exist:

```text
Merlin
memory
current conversation
medium memory
long-term memory
associative retrieval
SQLite
EF Core
DeepInfra
prompt compiler
interruption
assistant turn
```

Verify some concept edges exist:

```text
SQLite --used_for--> memory
prompt compiler --used_for--> token reduction
assistant turn --part_of--> conversation state
correction --related_to--> interruption
```

### 6. Repository smoke check

Run or create a smoke test/dev command that does this:

1. Save a memory.
2. Get or create concepts.
3. Link memory to concepts.
4. Search by text.
5. Search by concept.
6. Update last accessed.
7. Delete test memory if needed.

Example memory:

```text
Title: Merlin SQLite EF persistence smoke test
Content: Merlin stores local memory in SQLite under AppData using EF Core.
MemoryType: project_decision
Project: Merlin
Importance: 0.9
UserConfirmed: true
Concepts: Merlin, SQLite, EF Core, memory
```

Expected:

```text
Text search for "SQLite memory" returns the smoke memory.
Concept search for "SQLite" returns the smoke memory.
```

### 7. Conversation/turn smoke check

Run or create a smoke flow:

```text
Create/get active conversation
Start topic: "Persistence foundation test"
Start assistant turn with original user message
Append generated text
Append spoken text
Mark turn interrupted
Save correction prompt compilation
Read back turn and prompt compilation
```

Expected readback:

```text
Turn state: interrupted
Original user message: present
GeneratedTextSoFar: present
SpokenTextSoFar: present
InterruptedByUserMessage: present
PromptCompilation: present
EstimatedInputTokens: present
```

This proves the future interruption system has the data it needs.

## Documentation to create

Create or update a persistence README in the codebase.

Suggested file:

```text
Merlin.Backend/Infrastructure/Persistence/README.md
```

It should include:

- database choice: SQLite
- ORM choice: EF Core
- database location: AppData/Merlin/db/merlin_memory.db
- how the path is resolved
- how migrations are applied
- how to add a migration
- how to run smoke checks
- which repositories exist
- which services should use the repositories
- warning not to store DB in source repo

## Required handoff note for the memory-layer agent

Create a handoff note file.

Suggested file:

```text
Merlin.Backend/Core/Memory/MEMORY_LAYER_HANDOFF.md
```

Content should tell the next agent:

```text
The SQLite + EF Core persistence foundation is ready.
Do not replace it.
Use the store interfaces.
Do not inject MerlinDbContext directly into memory behavior services.
Build the brain-like memory architecture on top of these stores.
```

Include available interfaces:

```text
IMemoryStore
IConceptStore
IConversationStateStore
ITurnStateStore
IPromptCompilationStore
IMemorySearchService
IConceptExtractionService
IConversationRuntimeState
IAssistantTurnTracker
IPromptCompilationLogger
ITokenEstimator
```

Only list interfaces that actually exist.

## Handoff to memory layer

Once this foundation is complete, the next implementation should move to the larger memory architecture specification.

The next layer should implement:

- CurrentConversationMemory
- TopicBoundaryDetector
- MediumMemoryStore behavior
- LongTermMemoryStore behavior
- MemoryWriter
- MemoryPromoter
- AssociativeRetriever
- ConceptGraph traversal
- MemoryCompiler
- DeepInfra prompt budget enforcement
- explicit remember handling
- memory dashboard later

## Handoff to interruption behavior later

The interruption/barge-in spec should come after the basic memory/context layer.

The persistence foundation already prepares interruption by storing:

- assistant turn IDs
- original user message
- generated text so far
- spoken text so far
- interruption reason
- interrupted-by user message
- prompt compilations

This will allow the interruption system to build DeepInfra-aware correction prompts like:

```text
The assistant was answering the original request and started in the wrong database direction. The user interrupted with "No, SQLite." Discard PostgreSQL and regenerate using SQLite.
```

## Git ignore check

Ensure DB files are not accidentally committed.

Add or verify `.gitignore` entries if needed:

```gitignore
*.db
*.db-shm
*.db-wal
```

Be careful: if the project has legitimate test DB files, adjust accordingly.

Because the real DB is under AppData, it should not be in the repo anyway.

## Performance sanity check

Do not benchmark obsessively, but verify no obvious bad behavior:

- app startup should not hang on migrations
- seed should be idempotent and fast
- memory search should return within a negligible time for small datasets
- no huge graph is loaded accidentally
- repository reads use `AsNoTracking()` where appropriate
- prompt compilations are not logged to console at info level

Expected local DB overhead should be tiny compared to DeepInfra and TTS.

## Common failure modes to fix before handoff

### DB file appears in project folder

Fix path resolver/configuration.

### Migrations do not work from CLI

Fix `MerlinDbContextFactory`.

### App starts but no tables exist

Fix migration hosted service registration.

### Seed concepts duplicate every startup

Fix concept normalization/idempotent seeding.

### Memory search returns too much

Add limit enforcement and filters.

### Runtime state stores every token individually

Batch generated/spoken text updates.

### Memory services inject DbContext directly

Move that access behind store interfaces.

### EF entities leak into Core layer

Map to core records instead.

## Final acceptance criteria

Phase 6 is complete when:

- Backend builds successfully.
- Backend starts successfully.
- Database file is created under AppData/Merlin/db.
- Migrations apply automatically.
- Core tables exist.
- Seed concepts and seed edges exist.
- Memory insert/link/search smoke check passes.
- Conversation/turn/prompt logging smoke check passes.
- Persistence README exists.
- Memory-layer handoff note exists.
- `.db` files are ignored or outside repository.
- The next agent can start the brain-like memory layer without changing the database foundation.

## Suggested final agent message after Phase 6

```text
Phase 6 complete. The SQLite + EF Core persistence foundation is verified and documented. Merlin stores its local database under AppData/Merlin/db/merlin_memory.db, migrations apply on startup, seed concepts exist, repository interfaces are available, smoke checks pass, and the memory-layer handoff note is ready. The next step is to implement the brain-like memory layer using the existing stores.
```
