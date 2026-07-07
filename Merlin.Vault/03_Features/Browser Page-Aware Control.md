---
type: feature
status: partial
area: browserhost
tags:
  - merlin
  - feature
  - status/partial
---

# Browser Page-Aware Control

## Summary

Uses page snapshots and DOM scripts to click/search/control visible browser page elements.

## Status

partial

## What Exists Today

- Page snapshot script exists.
- Click visible element command exists.
- Search field script exists.
- CommonActionScript includes media/common controls and Dutch `overslaan` support.
- BrowserPageSafetyGuard exists.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.BrowserHost/PageSnapshotScript.cs` | Snapshot extractor | Captures elements and metadata. |
| `Merlin.BrowserHost/ClickElementScript.cs` | DOM click | Dispatches pointer/mouse/click events. |
| `Merlin.BrowserHost/CommonActionScript.cs` | Generic page actions | pause/play/skip/fullscreen/cookies. |
| `Merlin.Backend/Services/BrowserWorkspace/Snapshot/*` | Models | Backend representation. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Page-aware commands can inspect or act on DOM state, subject to safety.

## Planned Behavior

Learned site profiles should store selectors and corrections after full motion control exists.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Dynamic sites change metadata quickly.
- Accessibility labels are language-dependent.
- Wrong-click correction workflow is not mature.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Improve generic snapshot scoring before site DB.
