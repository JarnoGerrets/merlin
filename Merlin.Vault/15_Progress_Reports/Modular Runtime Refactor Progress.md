---
type: progress-report
status: active
area: cross-cutting
tags:
  - merlin
  - modular-runtime
  - progress
---

# Modular Runtime Refactor Progress

## Current Status

Active.

The refactor has completed feature-owned settings, inert `Merlin.Next` skeleton/runtime modes, and the first read-only shadow bridge. The next safe runtime step is the first controlled vertical slice.

Master plan review completed in [[RUN-2026-07-07-013 Modular Runtime Master Plan Review]]: direct implementation of the master plan is No-Go because it is governance-only and `ready_for_agent: false`. After [[RUN-2026-07-07-015 Kernel Contracts Shadow Bridge]], the next executable child plan is [[PLAN-2026-07-07-014 First Vertical Slice Apps AppOpen Plan]].

## Migration Stage

| Stage | Status | Notes |
| --- | --- | --- |
| Documentation pack | drafted | Copy docs into vault before agent execution. |
| Feature-owned settings | implemented | Implemented in [[RUN-2026-07-07-012 Feature-Owned Settings Migration]]. |
| Next skeleton | implemented | Implemented in [[RUN-2026-07-07-014 Merlin Next Skeleton And Runtime Modes]]. |
| Kernel contracts/shadow bridge | implemented | Implemented in [[RUN-2026-07-07-015 Kernel Contracts Shadow Bridge]]. |
| First hybrid capability | not started | `app.open` candidate. |
| Capability registry | future | Requires initial vertical slice. |
| Surface registry | future | Needed before browser/media/site modules. |
| Browser module | future | High risk, migrate later. |
| Voice module | future | Highest risk, migrate late. |

## Cutover Table

| Capability / Feature | Legacy Path | Next Path | Owner Mode | Status |
| --- | --- | --- | --- | --- |
| Settings | active | feature-owned files | Legacy | Plan 011 implemented; section names preserved. |
| runtime mode config | active | inert options only | Legacy | Plan 012 implemented; default `MerlinNext.Enabled=false`, `Mode=Legacy`. |
| shadow request trace | active | read-only trace only | Shadow | Plan 013 implemented; no capabilities execute. |
| `app.open` | active | planned | Legacy | Plan 014. |
| `app.focus` | active | future | Legacy | After app.open. |
| `url.open` | active | future | Legacy | Early candidate. |
| `web.search` | active/partial | future | Legacy | Web module. |
| `browser.media.pause` | active/partial | future | Legacy | Needs surfaces. |
| `browser.page.click` | active | future | Legacy | Safety-critical. |
| `voice.stop_speaking` | active | future | Legacy | Voice module late. |
| interruption clarification | active | future | Legacy | High risk. |

## Next Safe Task

Use:

```text
Merlin.Vault/08_Implementation_Prompts/PROMPT-2026-07-07-014 Implement First Vertical Slice Apps AppOpen.md
```

## Risks

| Risk | Status | Mitigation |
| --- | --- | --- |
| Big-bang rewrite | active risk | Use child plans only. |
| Double execution in hybrid mode | future risk | Add ownership guards and tests. |
| Voice timing regression | future risk | Voice migration late, focused tests/manual validation. |
| Browser safety bypass | future risk | Surface + safety policy before browser cutover. |
| Config load-order drift | mitigated | Settings README and `MerlinSettingsConfigurationTests`. |

## Latest Validation

| Date | Run | Result |
| --- | --- | --- |
| 2026-07-07 | [[RUN-2026-07-07-015 Kernel Contracts Shadow Bridge]] | Backend build passed; focused Next shadow tests passed 11/11; full backend test suite has 9 known pre-existing failures in correction and BargeIn areas. |
| 2026-07-07 | [[RUN-2026-07-07-014 Merlin Next Skeleton And Runtime Modes]] | Backend build passed; focused MerlinNext/settings tests passed 6/6; full backend test suite has 9 known pre-existing failures in correction and BargeIn areas. |
| 2026-07-07 | [[RUN-2026-07-07-013 Modular Runtime Master Plan Review]] | Backend build passed; settings configuration tests passed 2/2; full backend test suite has 10 pre-existing/separate failures in correction, BargeIn, and one pending clarification timeout that passed in isolation. |
