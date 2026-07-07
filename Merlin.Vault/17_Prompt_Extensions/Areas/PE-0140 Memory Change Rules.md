---
type: prompt-extension
id: PE-0140
status: active
applies_to:
  - memory
required_for:
  - memory change
---

# PE-0140 Memory Change Rules

## Required Reading

- [[Memory System]]
- [[Memory Architecture]]
- [[Memory State Ownership]]

## Rules

1. Runtime state is not memory.
2. Do not store transient active surface/motion state as long-term memory.
3. Preserve fail-closed memory behavior if implemented.
4. Preserve user preference overwrite semantics.
5. Add/update memory tests.
