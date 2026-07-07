---
type: roadmap
status: mixed
area: cross-cutting
tags:
  - merlin
  - roadmap
---

# Memory Roadmap

## Scope

Current conversation, long/medium memory, retrieval, prompt compilation, profile facts, and memory safety boundaries.

## Dependency-Ordered Items

| Item | Status | Depends on | Blocks | Ready? | Why / why not | Relevant feature notes | Relevant code atlas notes | Next safe action |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Keep memory orchestrator stable | implemented | memory stores/services | model prompt context | yes | Memory orchestration and prompt compiler exist. | [[Memory System]] | [[MemoryOrchestrator]], [[PromptCompiler]], [[Memory Prompt Context Flow]] | Update atlas when prompt blocks change. |
| Improve memory debug visibility | partial | debug endpoints and prompt compiler | safer memory changes | yes | Debug service exists but not every flow is exposed in vault. | [[Memory System]] | [[PromptCompiler]], [[Memory State Ownership]] | Add debug notes when runtime changes. |
| Keep runtime state separate from memory | implemented | ADR-0004 | correction/profile safety | yes | Decision recorded in vault. | [[Memory System]], [[Correction Layer]] | [[Memory State Ownership]], [[CommandRouter]] | Do not store transient ActiveSurface/playback as memory. |
| Learned control/profile memory | future | Control Profile DB | site/app profiles | no | Not current memory feature. | [[Control Profile DB]], [[Site Control Profiles]] | [[MemoryOrchestrator]], [[BrowserWorkspaceService]] | Wait for safety design. |
