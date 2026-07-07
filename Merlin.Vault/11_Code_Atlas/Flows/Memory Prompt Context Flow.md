---
type: flow
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Memory Prompt Context Flow

## Summary

Memory services build PromptBlock context from conversation state, user profile facts, retrieval, and prompt compilation stores.

## Current Flow

1. conversation/user text
2. MemoryOrchestrator
3. retrieval/fact services
4. PromptCompiler.CompileAsync
5. PromptBlock list
6. AI prompt context

## Mermaid Diagram

```mermaid
flowchart LR
    N0[conversation/user text] --> N1[MemoryOrchestrator]
    N1[MemoryOrchestrator] --> N2[retrieval/fact services]
    N2[retrieval/fact services] --> N3[PromptCompiler.CompileAsync]
    N3[PromptCompiler.CompileAsync] --> N4[PromptBlock list]
    N4[PromptBlock list] --> N5[AI prompt context]
```

## Related Feature And Architecture Notes

- [[Memory System]]
- [[PromptCompiler]]

## Known Fragility

- Cross-process flows require lifecycle cleanup and explicit logging.
- If the active surface is stale, routing and profile selection can target the wrong consumer.
