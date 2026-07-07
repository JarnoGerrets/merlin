---
type: adr
status: current
area: cross-cutting
tags:
  - merlin
  - decision
---

# ADR-0004 Runtime State Is Not Memory

Runtime state such as active surface, playback status, browser host bounds, and motion profile state must remain runtime state. Do not persist it as user memory unless it is explicitly user-declared durable preference/fact.
