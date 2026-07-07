---
type: current-state
status: planned
area: cross-cutting
tags:
  - merlin
  - modular-runtime
  - refactor-readiness
---

# Current Refactor Readiness

## Summary

Merlin is ready for planning and preparation work for the modular runtime refactor.

Merlin is not ready for a big-bang migration.

## Ready Now

| Area | Readiness | Safe Action |
| --- | --- | --- |
| Vault process | ready | Use `AGENT.md`, plans, prompts, Go/No-Go, writeback. |
| Settings split | implemented | Feature-owned files preserve section names and load order. |
| Runtime skeleton | implemented | Inert `Next` folders and runtime mode options exist. |
| Shadow tracing | implemented | Read-only bridge logs disabled execution traces only. |
| First safe vertical slice | ready | Use `app.open` only. |

## Not Ready Yet

| Area | Why Not Ready | Required Before Work |
| --- | --- | --- |
| Browser module migration | Safety, surface, IPC, stale snapshot, native input concerns. | Surface registry, adapter boundaries, safety policy ownership. |
| Voice module migration | Barge-in/interruption/TTS timing fragility. | Shadow harness, tests, stable pending ownership. |
| CommandRouter removal | Too much behavior still depends on it. | Strangler pipeline and per-capability cutover. |
| Separate C# projects | Boundaries not proven yet. | Build inside `Merlin.Backend/Next` first. |

## Recommended Next Step

Implement:

```text
PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan
```
