---
type: feature
status: implemented
area: backend
tags:
  - merlin
  - feature
  - status/implemented
---

# Memory System

## Summary

Persistent and prompt-integrated memory system.

## Status

implemented

## What Exists Today

- Core memory models/services/stores exist.
- EF persistence exists.
- Memory search, prompt compiler, user facts, topic closing exist.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Core/Memory/Services/MemoryOrchestrator.cs` | Orchestration | Retrieval/write coordination. |
| `Merlin.Backend/Core/Memory/Stores/*` | Interfaces | Persistence contracts. |
| `Merlin.Backend/Infrastructure/Persistence/*` | EF | SQLite entities/repositories. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Memory participates in conversation context and prompt compilation.

## Planned Behavior

Future memory changes should use existing schema-aware plans.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Prompt/token budgeting and quality remain ongoing concerns.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Do not rebuild memory storage.
