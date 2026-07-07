---
type: adr
status: proposed
tags:
  - merlin
  - adr
  - modular-runtime
  - strangler
---

# ADR-0007 Modular Runtime Strangler Refactor

## Status

Proposed.

## Context

Merlin's backend has grown by feature accretion. Important behavior now spans command routing, active surface context, browser workspace, motion/vision, voice, interruption, TTS, memory, web search, app launch, and safety.

A direct in-place refactor would be high risk because many systems are live-stateful and only partially covered by automated tests.

The desired future structure is:

```text
Merlin.Host
Merlin.Kernel
Merlin.Modules.*
Merlin.Adapters.*
```

## Decision

Use a strangler migration.

Build the new modular runtime beside the existing backend first, initially under a `Next` structure inside `Merlin.Backend`.

Support runtime modes:

- `Legacy`
- `Shadow`
- `Hybrid`
- `NextFirst`
- `NextOnly`

Migrate by vertical capability slices instead of moving all layers at once.

Do not split into many separate C# projects until boundaries have been proven inside the current backend.

## Consequences

Positive:

- current Merlin can keep working during migration;
- shadow traces can compare old and new routing;
- risky systems like voice and browser can move late;
- new features can eventually register modules instead of editing central routers;
- rollback is possible with config.

Negative:

- temporary duplication will exist;
- cutover state must be tracked carefully;
- tests and trace logs become important;
- agents must avoid creating permanent parallel subsystems.

## Related Notes

- [[Modular Runtime Architecture]]
- [[Strangler Migration Architecture]]
- [[Modular Runtime Refactor Master Plan]]
