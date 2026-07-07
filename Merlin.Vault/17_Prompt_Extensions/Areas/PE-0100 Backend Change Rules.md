---
type: prompt-extension
id: PE-0100
status: active
applies_to:
  - backend
required_for:
  - backend change
---

# PE-0100 Backend Change Rules

## Required Reading

- [[Backend Architecture]]
- [[Command Routing Architecture]]
- affected backend code atlas notes
- affected tests

## Rules

1. Respect DI registrations in `Program.cs`.
2. Keep services testable with interfaces/fakes where current architecture supports it.
3. Avoid moving logic into `CommandRouter` when a dedicated service/normalizer should own it.
4. Preserve cancellation tokens where used.
5. Preserve structured logging style.
6. Add/update backend tests for behavior changes.
