---
type: feature
status: implemented
area: cross-cutting
tags:
  - merlin
  - feature
  - status/implemented
---

# Browser Workspace

## Summary

In-Merlin browser using a separate BrowserHost/WebView2 process.

## Status

implemented

## What Exists Today

- BrowserHost/WebView2 process exists.
- BrowserWorkspaceService launches and controls it.
- Navigation, scroll, zoom, search, page snapshot, click visible element, common actions exist.
- Native browser pointer overlay exists.
- ActiveSurface updates exist.
- BrowserPageSafetyGuard exists.

## Code Map

| File | Role | Notes |
| --- | --- | --- |
| `Merlin.Backend/Services/BrowserWorkspace/BrowserWorkspaceService.cs` | Orchestration | Process lifecycle and commands. |
| `Merlin.BrowserHost/BrowserWorkspaceForm.cs` | Host window | WebView2 command loop. |
| `Merlin.BrowserHost/PageSnapshotScript.cs` | Snapshot | Extracts page elements. |
| `Merlin.Backend/Services/BrowserWorkspace/PageControl/Safety/BrowserPageSafetyGuard.cs` | Safety | Click/action checks. |

## Related Systems

- [[System Architecture Overview]]
- [[Command Routing Architecture]]
- [[Active Surface Architecture]]


## Dependencies

Dependencies are listed here and in [[Master Roadmap]]. Planned/future work must not start until dependencies are ready.

## Dependents

See linked roadmap notes.

## Current Behavior

Commands open/close browser, navigate sites, manipulate page, and show motion pointer.

## Planned Behavior

Site-specific profiles and corrections should come later through Control Profile DB.

## Non-Goals / Do Not Build Yet

Do not build app/site-specific V2 behavior unless the relevant roadmap item is explicitly requested and marked ready.

## Known Bugs / Fragility

- Host lifecycle/z-order can be fragile.
- Browser close may not fully restore Merlin UI state.
- Snapshots can stale quickly.
- Raw motion click safety gap.

## Tests

See [[Current Test Coverage]].

## Relevant Docs / Reports / Prompts

See [[07_Agent_Reports/Index|Agent Reports Index]] and [[08_Implementation_Prompts/Index|Implementation Prompts Index]].

## Next Actions

Stabilize close/reset and safety integration.
