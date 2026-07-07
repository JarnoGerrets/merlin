---
type: adr
status: accepted
tags:
  - merlin
  - adr
  - decision
---

# ADR-0003 Motion Profiles Over One Global Motion Mode

## Status

accepted

## Context

Dashboard UI motion and browser pointer motion need different consumers and sensitivities.

## Decision

Use Motion Profiles selected by Active Surface, with one active motion profile at a time.

## Consequences

Future app/site profiles can plug into the profile layer without duplicating pinch logic.

## Related Notes

- [[Motion Control Profile Layer]]
- [[Motion Architecture]]
