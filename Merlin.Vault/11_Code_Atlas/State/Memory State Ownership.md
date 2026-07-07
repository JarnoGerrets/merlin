---
type: state
status: current
area: cross-cutting
tags:
  - merlin
  - code-atlas
---

# Memory State Ownership

| State | Owner | Readers | Writers | Lifetime | Reset Conditions | Risks |
| --- | --- | --- | --- | --- | --- | --- |
| Profile facts/concepts/conversations | EF stores/Core Memory services | PromptCompiler, MemoryOrchestrator | MemoryWriter, stores | database lifetime | memory lifecycle/status | Prompt context invisibility |

## Related Notes

- [[Current System Map]]
- [[Code Atlas Index]]
