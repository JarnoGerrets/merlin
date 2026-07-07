---
type: source-material
origin: Merlin.ToDo
source_path: Merlin.ToDo/done/merlin-memory-layer-phases/00_README.md
classification: implementation-plan
related_features:
  - Memory System
status: implemented
imported_to_vault: true
---

# Merlin Brain-Like Memory Layer Phases

## Purpose

This folder breaks the Merlin brain-like memory layer into safe implementation phases for the coding agent.

The persistence foundation already exists. Do **not** rebuild it.

The database, EF Core entities, migrations, repositories/stores, seed concepts, conversation/topic/turn persistence, and prompt compilation logging foundation have already been implemented and verified.

This phase pack starts from the assumption that the following are available in the Merlin backend:

- EF Core SQLite persistence using `%APPDATA%/Merlin/db/merlin_memory.db`.
- Store interfaces for memories, concepts, conversations, assistant turns, and prompt compilations.
- EF-backed repository/store implementations.
- Seed concepts and starter concept edges.
- Runtime turn state persistence.
- Prompt compilation log persistence.
- Smoke tests for the persistence foundation.

The goal of these phases is to implement the actual **brain-like memory behavior** on top of that foundation.

Merlin must not become a generic chat-history forwarder. The intended architecture is:

```text
User message
↓
Local concept extraction
↓
Current topic tracking
↓
Associative memory retrieval
↓
Prompt compiler with strict token budget
↓
DeepInfra receives only a compact relevant context packet
↓
Assistant response
↓
Memory writer updates local memory
```

Core principle:

```text
Remember broadly locally.
Retrieve associatively.
Send narrowly to DeepInfra.
```

---

## Source of truth

The source design document is:

```text
merlin_brain_like_memory_system.md
```

This phase pack is a practical implementation breakdown of that design, adjusted for the persistence work that is already complete.

---

## Very important constraints

The agent must follow these rules throughout all phases:

1. Do not redo the SQLite/EF Core persistence foundation.
2. Do not replace EF Core.
3. Do not move the database into the repository.
4. Keep using `%APPDATA%/Merlin/db/merlin_memory.db`.
5. Keep memory intelligence separate from persistence infrastructure.
6. Do not implement interruption behavior in these phases.
7. Do not implement voice barge-in in these phases.
8. Do not implement embeddings yet.
9. Do not implement a full dashboard yet.
10. Do not use unnecessary packages.
11. Do not introduce object-mapping packages.
12. Keep concerns separated.
13. Keep interfaces in Core/Application-level folders.
14. Keep EF-specific implementations in Infrastructure/Persistence.
15. Every phase must build and test before moving to the next phase.

---

## Recommended execution order

Run these phases in this order:

```text
01_phase_1_current_conversation_memory.md
02_phase_2_explicit_memory_writer.md
03_phase_3_medium_memory_topic_closing.md
04_phase_4_associative_retrieval_mvp.md
05_phase_5_prompt_compiler_token_budget.md
06_phase_6_memory_orchestrator_deepinfra_integration.md
07_phase_7_audit_debug_visibility.md
```

Do **not** implement all phases in one uncontrolled run.

Recommended safe batches:

```text
Run 1: Phase 1 only
Run 2: Phase 2 only
Run 3: Phase 3 only
Run 4: Phase 4 only
Run 5: Phase 5 only
Run 6: Phase 6 only
Run 7: Phase 7 only
```

Phase 5 and Phase 6 are the most sensitive because they affect what gets sent to DeepInfra.

---

## High-level phase goals

### Phase 1 — Current conversation memory

Merlin tracks the active topic, active concepts, current goal, recent summary, and whether a user message continues or starts a new topic.

No long-term memory writing yet.
No prompt compiler yet.
No DeepInfra integration yet.

### Phase 2 — Explicit memory writer

Merlin detects explicit remember/save/note/from-now-on requests and stores confirmed long-term memories locally with concepts.

### Phase 3 — Medium memory and topic closing

Merlin can close an active topic and create a medium-term episode memory summary.

### Phase 4 — Associative retrieval MVP

Merlin retrieves relevant memories using keyword search, concept matching, one-hop graph activation, scoring, and human-readable retrieval reasons.

### Phase 5 — Prompt compiler and token budget

Merlin compiles a compact DeepInfra context packet from current topic state and retrieved memories. The current user message must always be exact. Prompt compilations must be logged.

### Phase 6 — Memory orchestrator and DeepInfra integration

Merlin’s real conversation pipeline starts using the memory layer before and after DeepInfra calls.

### Phase 7 — Audit/debug visibility

Add dev-only APIs or commands to inspect active topic state, stored memories, concepts, retrieval results, and prompt compilation logs.

---

## Global acceptance criteria

The memory layer is considered ready for the next major workstream when Merlin can:

1. Track the active topic locally.
2. Detect basic topic continuation vs topic switch.
3. Save explicit remember requests as confirmed long-term memories.
4. Close a topic into a medium-term episode memory.
5. Attach concepts to memories.
6. Retrieve memories by keywords and concepts.
7. Expand concepts through one-hop graph edges.
8. Return retrieval scores and reasons.
9. Compile a compact prompt context for DeepInfra.
10. Log estimated tokens and included memory IDs.
11. Avoid sending full raw conversation history by default.
12. Expose enough debug visibility to inspect what happened.

---

## Mandatory verification after every phase

Run:

```powershell
dotnet build .\Merlin.Backend\Merlin.Backend.csproj
```

Run relevant tests:

```powershell
dotnet test .\Merlin.Backend.Tests\Merlin.Backend.Tests.csproj
```

If tests become too broad or slow, run a focused filter for the phase-specific tests.

Before handing back, report:

- Files changed.
- What was implemented.
- What was intentionally not implemented.
- Build command run and result.
- Test command run and result.
- Any known limitations.
- Suggested next phase.

---

## Final note to the agent

Do not be clever. Build simple, observable, testable pieces.

This system will become central to Merlin’s cost control and conversational intelligence. Reliability and inspectability matter more than impressive abstractions.
