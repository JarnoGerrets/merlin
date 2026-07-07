---
type: feature
status: implemented
area: backend
tags:
  - merlin
  - feature
  - status/implemented
  - layer/backend
---

# Memory System

## Summary

SQLite/EF-backed memory layer with prompt compilation, facts, retrieval, and debug endpoints.

## Status

implemented

## Verified Against Code

Status verified: yes

Evidence:
- `Merlin.Backend/Core/Memory contains models/services/stores.`
- `Program.cs registers memory stores/services.`
- BrainLikeMemoryLayerTests.cs, CoreMemoryHealthServiceTests.cs, MemoryExtractionServiceTests.cs, LongTermMemoryStoreTests.cs exist.

## What Exists Today

Memory stores conversations, facts, concepts, retrieval results, and prompt blocks.

## Current Behavior

PromptCompiler assembles memory blocks and retrieval context; memory debug endpoints exist.

## Planned Behavior

Improve hygiene, correction integration, and UI visibility.

## Code Map

| File | Class / Function | Role | Notes |
| --- | --- | --- | --- |
| `Merlin.Backend/Core/Memory/Services/MemoryOrchestrator.cs` | MemoryOrchestrator | Memory flow | Coordinates memory services. |
| `Merlin.Backend/Core/Memory/Services/PromptCompiler.cs` | PromptCompiler | Prompt context | Produces PromptBlock list. |
| `Merlin.Backend/Core/Memory/Models/UserProfileFact.cs` | UserProfileFact | Profile facts | Persistent user facts. |

## Code Atlas

- [[MemoryOrchestrator]]
- [[PromptCompiler]]
- [[Memory Prompt Context Flow]]
- [[Memory State Ownership]]
- [[Memory Config]]

## Related Systems

- SQLite EF persistence
- [[Correction Layer]]
- future personalization

## Dependencies

- SQLite EF persistence

## Dependents

- [[Correction Layer]]
- future personalization

## Readiness

Ready for implementation: yes

Reason:
Implemented; improvements can be scoped.

Blocked by:
- No blocker for maintenance; new memory-learning features need policy.

Next safe action:
Keep runtime state separate from memory and add targeted hygiene tests.

## Non-Goals / Do Not Build Yet

- Do not store transient active surface/runtime state as memory.

## Known Bugs / Fragility

- Memory can influence responses invisibly if prompt block diagnostics are not checked.

## Tests

| Test File | Coverage | Gaps |
| --- | --- | --- |
| `Merlin.Backend.Tests/BrainLikeMemoryLayerTests.cs` | Memory integration | Runtime UI visibility is limited. |

## Relevant Implementation Plans

- None currently promoted for this feature.

## Relevant Reports

- See [[Agent Reports Index]] for cross-cutting reports.

## Relevant Prompts

- [[Implementation Prompts Index]]

## Source Material

- [[Imported Merlin.ToDo Index]] (17 imported source item(s) mapped to this feature).
## Open Questions

- Which runtime observations should be added after the next live validation?
