---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin_sqlite_efcore_persistence_phases/00_README.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Merlin SQLite + EF Core Persistence Foundation

## Purpose

This folder contains a phased implementation plan for preparing Merlin's local SQLite persistence foundation before building the full brain-like memory system and interruption/barge-in system.

The goal is to give the agent a precise, ordered path so it does not waste time debating database choices, storage location, folder structure, migration style, or repository boundaries.

This foundation must be completed before the agent moves to the larger memory-layer specification.

## Important architectural decision

Merlin will use:

- **SQLite** as the local database engine.
- **EF Core** as the main persistence abstraction and migration tool.
- A local database file stored outside the source code repository.
- The Windows AppData location:

```text
%APPDATA%/Merlin/db/merlin_memory.db
```

In .NET this should be resolved with:

```csharp
Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
```

Then append:

```text
Merlin/db/merlin_memory.db
```

Do not hardcode the literal string `%APPDATA%` as a directory name.

## Correction about EF Core and performance

Use EF Core because it improves:

- maintainability
- schema migrations
- entity relationships
- repository implementation speed
- future schema evolution
- developer clarity

Do **not** justify EF Core by claiming it is faster than raw SQLite. Raw `Microsoft.Data.Sqlite` queries can be faster in tight loops. However, Merlin's memory database overhead will be tiny compared to DeepInfra calls, transcription, TTS, and prompt generation. EF Core is the better choice here because the schema will evolve and the project benefits more from maintainability than micro-optimization.

The agent must still write efficient EF Core code:

- use `AsNoTracking()` for read-only queries
- avoid loading large graphs accidentally
- use indexes
- limit query results
- keep transactions small
- avoid saving every token individually
- batch updates where reasonable

## Relationship to the larger memory system

This folder does **not** implement the full brain-like memory system yet.

It prepares the local persistence layer that the memory system will use.

After these phases are complete, the agent should move to the larger memory architecture Markdown file and build:

- CurrentConversationMemory
- MediumMemory
- LongTermMemory
- ConceptGraph
- AssociativeRetriever
- MemoryCompiler
- MemoryPromoter
- interruption-aware correction prompts

## Recommended phase order

Complete these phases one by one:

1. `01_phase_1_appdata_efcore_foundation.md`
2. `02_phase_2_entities_schema_migrations.md`
3. `03_phase_3_store_interfaces_and_repositories.md`
4. `04_phase_4_search_seed_and_concept_primitives.md`
5. `05_phase_5_runtime_turn_state_and_prompt_logging.md`
6. `06_phase_6_verification_handoff_to_memory_layer.md`

The agent should not start Phase 2 until Phase 1 builds and runs. The agent should not start Phase 3 until the first migration exists and the database file is created in AppData.

## Non-negotiable rules

- Do not store `merlin_memory.db` inside the codebase.
- Do not replace SQLite with PostgreSQL, LiteDB, SQL Server, MongoDB, or another database.
- Do not replace EF Core with raw SQL everywhere unless explicitly instructed later.
- Do not create a giant generic `ChatHistory` table and call it memory.
- Do not send full conversation history to DeepInfra.
- Do not make memory code depend directly on concrete EF repositories when interfaces are expected.
- Do not store incomplete/interrupted assistant answers as confirmed long-term memory.
- Do not build voice interruption first. Persistence and state foundation come first.

## Expected final folder structure after these phases

The exact project layout may vary slightly depending on the existing Merlin solution, but the target structure should look roughly like this:

```text
Merlin.Backend/
├── Core/
│   └── Memory/
│       ├── Models/
│       ├── Stores/
│       └── Search/
├── Infrastructure/
│   └── Persistence/
│       ├── MerlinDbContext.cs
│       ├── MerlinDbOptions.cs
│       ├── MerlinDbPathResolver.cs
│       ├── MerlinDbMigratorHostedService.cs
│       ├── Entities/
│       ├── Configurations/
│       ├── Repositories/
│       └── Seeding/
└── appsettings.json
```

## Expected database location

On Windows, the database should end up at a path equivalent to:

```text
C:\Users\<username>\AppData\Roaming\Merlin\db\merlin_memory.db
```

The implementation should create the directory if it does not exist.

## Expected completion signal

At the end of Phase 6, the agent should be able to report:

```text
SQLite + EF Core persistence foundation is ready.
Database file is created under AppData/Merlin/db.
Migrations apply on startup.
Core memory, concept, conversation, turn, and prompt compilation tables exist.
Repositories are registered behind interfaces.
Seed concepts exist.
Smoke tests/dev checks can insert and query memory.
The system is ready for the brain-like memory layer implementation.
```
