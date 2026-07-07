---
type: dashboard
status: active
---

# Current Work Dashboard

## Purpose

This dashboard summarizes what is active, what is next, what is blocked, and what agents should not touch unless explicitly asked.

## Active Work

| Work | Status | Next Action | Related Plan | Prompt Bundle |
| --- | --- | --- | --- | --- |
| Vault operating system | active | Use new run/writeback/bundle rules on future tasks. | [[Implementation Plan Lifecycle]] | [[PB-0007 Documentation Bundle]] |
| Browser control hardening | partial | Stabilize lifecycle/safety before site-specific expansion. | [[Browser Control Phases 2-5 Plan]] | [[PB-0003 Browser Workspace Bundle]] |
| Motion control hardening | partial | Add diagnostics/safety-aware pointer click policy before site profiles. | [[Motion Control Profile Layer Plan]] | [[PB-0004 Motion Control Bundle]] |
| Modular runtime refactor | active | Implement first controlled `app.open` vertical slice. | [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]] | [[PB-0010 Refactor Bundle]] |

## Next Safe Tasks

| Task | Ready? | Why | Related Feature | Prompt Bundle |
| --- | --- | --- | --- | --- |
| Run AskClarification PR10.4 live UX validation | yes | Implementation is complete but not live-verified. | [[Voice Interruption System]] | [[PB-0008 Investigation Bundle]] |
| Fix BargeIn idle-capture test failures | yes | Four known BargeIn idle-capture tests remain red. | [[Voice Interruption System]] | [[PB-0009 Bugfix Bundle]] |
| Fix correction regeneration test failures | yes | Known correction dispatcher tests remain red. | [[Correction Layer]] | [[PB-0009 Bugfix Bundle]] |
| Harden browser close/reset | yes | Browser state can remain stale after close. | [[Browser Workspace]] | [[PB-0003 Browser Workspace Bundle]] |
| Add raw motion click safety adapter | yes | Native motion clicks bypass page safety today. | [[Browser Pinch Click]], [[Safety and Confirmation]] | [[PB-0004 Motion Control Bundle]] |

## Blocked / Do Not Build Yet

| Feature | Blocked By | Reason | Related Roadmap |
| --- | --- | --- | --- |
| Control Profile DB | correction stability, safety-aware raw click policy | Learned profiles need safer foundations. | [[Browser Roadmap]] |
| Full site-control learning | Control Profile DB | Needs motion/control correction loop and durable profile storage. | [[Browser Roadmap]] |
| Spotify Widget | auth/widget foundation | Future feature; do not build unless explicitly requested. | [[UI and Widgets Roadmap]] |

## Blocked By No-Go

| Task | Blocker | Required Prerequisite | Suggested Next Prompt |
| --- | --- | --- | --- |

## Newly Derived Work

Only list active, ready, or blocked derived work. Do not turn this dashboard into an archive.

| Derived Work | Type | Status | Origin Run | Plan | Prompt | Next Action |
| --- | --- | --- | --- | --- | --- | --- |
| DW-2026-07-07-022 | bugfix | ready | [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]] | [[PLAN-2026-07-07-022 BargeIn Idle Capture Test Failures Plan]] | [[PROMPT-2026-07-07-022 Fix BargeIn Idle Capture Test Failures]] | Fix known BargeIn idle-capture tests. |
| DW-2026-07-07-023 | bugfix | ready | [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]] | [[PLAN-2026-07-07-023 Correction Regeneration Test Failures Plan]] | [[PROMPT-2026-07-07-023 Fix Correction Regeneration Test Failures]] | Fix known correction regeneration tests. |

## Recently Completed

| Date | Run | Feature | Result |
| --- | --- | --- | --- |
| 2026-07-07 | [[RUN-2026-07-07-015 Kernel Contracts Shadow Bridge]] | Architecture | Added read-only kernel contracts and shadow trace bridge. |
| 2026-07-07 | [[RUN-2026-07-07-014 Merlin Next Skeleton And Runtime Modes]] | Architecture | Added inert `Merlin.Backend/Next` skeleton and default Legacy runtime mode options. |
| 2026-07-07 | [[RUN-2026-07-07-012 Feature-Owned Settings Migration]] | Architecture | Split backend settings into feature-owned files with compatible load order. |
| 2026-07-07 | [[RUN-2026-07-07-011 AskClarification PR10.4 Closure Review]] | Voice | Marked PR10.4 implementation-complete and created live validation checklist. |
| 2026-07-07 | [[RUN-2026-07-07-010 AskClarification PR10.4e Full Recomposition Ownership]] | Voice | Added executable pending clarification response recomposition owner. |
| 2026-07-07 | [[RUN-2026-07-07-009 AskClarification PR10.4d Stale Handling Watchdog]] | Voice | Added owner-aware stale handling watchdog. |
| 2026-07-07 | [[RUN-2026-07-07-008 AskClarification PR10.4c Awaiting State Timeout]] | Voice | Added awaiting clarification state and timeout recovery. |
| 2026-07-07 | [[RUN-2026-07-07-007 Derived Work Planning Layer]] | Vault | Added durable derived-work plan/prompt materialization rules. |
| 2026-07-07 | [[RUN-2026-07-07-006 AskClarification PR10.4b Pending Owner]] | Voice | Added pending unclear-interruption clarification owner. |
| 2026-07-07 | [[RUN-2026-07-07-004 Go No-Go Rules]] | Vault | Added strict no-go stop-before-runtime-change rules. |
| 2026-07-07 | [[RUN-2026-07-07-001 Vault Agent Operating System]] | Vault | Added operating-system structure. |

## High-Risk Areas

| Area | Risk | Required Prompt Bundle / Extension |
| --- | --- | --- |
| Native browser input | unsafe clicks, DPI/focus/z-order issues | [[PB-0003 Browser Workspace Bundle]] |
| Motion click/scroll | raw actions can bypass safety | [[PB-0004 Motion Control Bundle]] |
| Voice interruption/correction | current tests failing in related areas | [[PB-0005 Voice Pipeline Bundle]], [[PB-0009 Bugfix Bundle]] |
| Memory | runtime state can be confused with durable memory | [[PB-0006 Memory Bundle]] |

## Notes

Update this dashboard after meaningful implementation or planning changes.
