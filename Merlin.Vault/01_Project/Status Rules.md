---
type: project
status: current
area: cross-cutting
tags:
  - merlin
  - status
---

# Status Rules

## implemented

Verified in actual code and supported by tests or manual validation notes.

## partial

Some code exists but behavior is incomplete, fragmented, not wired, or missing tests.

## planned

Designed in docs/prompts but not implemented yet.

## blocked

Cannot safely be implemented until named dependencies exist.

## future

Intentionally later; do not build unless explicitly requested.

## deprecated

Old approach replaced by another system.

## unknown

Could not verify from current repo inspection.

## Critical Rule

A plan file does not make a feature implemented. Only verified code + tests or manual validation can justify implemented.

## Related Operating Rules

- [[Agent Writeback Rules]]
- [[Prompt Extension Selection Guide]]
- [[PE-0002 Scope and Status Rules]]
