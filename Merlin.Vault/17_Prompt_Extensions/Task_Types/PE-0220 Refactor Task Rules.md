---
type: prompt-extension
id: PE-0220
status: active
applies_to:
  - refactor
required_for:
  - refactor
---

# PE-0220 Refactor Task Rules

## Rules

1. Preserve externally visible behavior unless explicitly changed.
2. Run regression tests.
3. Update code atlas for moved ownership.
4. Update architecture notes if boundaries changed.
5. Avoid mixing refactor with feature expansion.
