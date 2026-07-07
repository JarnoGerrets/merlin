---
type: implementation-plan-index
status: draft
tags:
  - merlin
  - architecture-refactor
  - modular-runtime
---

# Architecture Refactor Implementation Plans

## Purpose

This folder contains the staged implementation plans for the Merlin modular runtime refactor.

The refactor must be executed as a strangler migration. Do not implement as a big-bang rewrite.

## Plan Sequence

| Order | Plan | Status | Ready? | Purpose |
| --- | --- | --- | --- | --- |
| 1 | [[PLAN-2026-07-07-010 Modular Runtime Refactor Master Plan]] | ready | no | Governance and sequencing. |
| 2 | [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]] | implemented | no | Split settings safely before runtime refactor. |
| 3 | [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]] | implemented | no | Added inert `Next` skeleton and runtime mode flags. |
| 4 | [[PLAN-2026-07-07-013 Kernel Contracts Shadow Bridge Plan]] | implemented | no | Added kernel contracts and read-only shadow bridge. |
| 5 | [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]] | ready | yes | First safe hybrid capability. |
| 6 | [[PLAN-2026-07-07-015 Capability Routing And Module Registration Plan]] | future | no | Module-owned capability registry. |
| 7 | [[PLAN-2026-07-07-016 Dynamic Surface Registry Plan]] | future | no | Dynamic surfaces and surface capabilities. |
| 8 | [[PLAN-2026-07-07-017 CommandRouter Strangler Pipeline Plan]] | future | no | Split router behind stable facade. |
| 9 | [[PLAN-2026-07-07-018 Adapter Boundary Migration Plan]] | future | no | Ports/adapters for provider and IPC boundaries. |
| 10 | [[PLAN-2026-07-07-019 Browser Module Migration Plan]] | future | no | Browser module migration after surfaces/safety are ready. |
| 11 | [[PLAN-2026-07-07-020 Voice Module Migration Plan]] | future | no | Voice migration late due high state/timing risk. |
| 12 | [[PLAN-2026-07-07-021 Validation Regression Harness Plan]] | ready | yes | Cross-cutting validation and trace comparison. |

## Safe Starting Point

Start with:

- [[PLAN-2026-07-07-011 Feature-Owned Settings Migration Plan]]

Then:

- [[PLAN-2026-07-07-012 Merlin Next Skeleton And Runtime Modes Plan]]

Current next executable child plan:

- [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]]

Do not start with browser or voice migration.
