---
type: prompt-extension
id: PE-0180
status: active
applies_to:
  - active surface
required_for:
  - active surface change
---

# PE-0180 Active Surface Change Rules

## Required Reading

- [[Active Surface Layer]]
- [[Active Surface Architecture]]
- [[ActiveSurfaceService]]
- [[Active Surface Flow]]

## Rules

1. ActiveSurface tells Merlin where the user is operating.
2. Do not make LiveUtteranceGate app-specific.
3. Do not let external/future surfaces override Merlin-owned surfaces without priority rules.
4. ActiveSurface routing does not bypass safety.
5. Runtime active surface is not long-term memory.
