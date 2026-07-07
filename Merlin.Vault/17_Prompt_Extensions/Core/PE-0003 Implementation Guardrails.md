---
type: prompt-extension
id: PE-0003
status: active
applies_to:
  - all systems
required_for:
  - implementation
  - bugfix
  - refactor
---

# PE-0003 Implementation Guardrails

## Rules

1. Implement only the requested task or phase.
2. Do not sneak in future phases.
3. Preserve existing behavior unless the task explicitly changes it.
4. Keep changes narrow and reversible.
5. Prefer adapters/wrappers before large rewrites when stabilizing architecture.
6. Do not bypass safety, confirmation, cancellation, or interruption systems.
7. If architecture changes, update architecture notes and code atlas.
